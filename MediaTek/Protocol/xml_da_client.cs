// ============================================================================
// SakuraEDL - MediaTek XML DA 协议客户端
// MediaTek XML Download Agent Protocol Client (V6)
// ============================================================================
// 参考: mtkclient 项目 xml_cmd.py, xml_lib.py
// ============================================================================

using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Models;
using DaEntry = SakuraEDL.MediaTek.Models.DaEntry;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// XML DA 协议客户端 (V6)
    /// </summary>
    public class XmlDaClient : IDisposable
    {
        private SerialPort _port;
        private readonly Action<string> _log;
        private readonly Action<string> _logDetail;
        private readonly Action<double> _progressCallback;
        private bool _disposed;
        
        // 线程安全: 端口锁 (可从 BromClient 共享)
        private readonly SemaphoreSlim _portLock;
        private readonly bool _ownsPortLock;

        // XML 协议常量
        private const uint XML_MAGIC = 0xFEEEEEEF;
        private const int DEFAULT_TIMEOUT_MS = 30000;
        private const int MAX_BUFFER_SIZE = 65536;

        // XFlash 命令常量 (根据 mtkclient xflash_param.py 修正)
        // ═══════════════════════════════════════════════════════════════════════
        // mtkclient: BOOT_TO = 0x010008
        // ═══════════════════════════════════════════════════════════════════════
        private const uint CMD_BOOT_TO = 0x010008;

        // 数据类型 (根据 mtkclient xml_param.py / xflash_param.py 修正)
        // ═══════════════════════════════════════════════════════════════════════
        // mtkclient:
        //   DT_PROTOCOL_FLOW = 1
        //   DT_MESSAGE = 2
        // ═══════════════════════════════════════════════════════════════════════
        private enum DataType : uint
        {
            /// <summary>协议流数据 (命令/响应)</summary>
            ProtocolFlow = 1,       // DT_PROTOCOL_FLOW
            /// <summary>消息/日志数据</summary>
            Message = 2,            // DT_MESSAGE
            /// <summary>原始数据传输</summary>
            ProtocolRaw = 2         // 也是 2, 用于数据传输
        }

        // 连接状态
        public bool IsConnected { get; private set; }
        public MtkDeviceState State { get; private set; }

        public XmlDaClient(SerialPort port, Action<string> log = null, Action<string> logDetail = null, Action<double> progressCallback = null, SemaphoreSlim portLock = null)
        {
            _port = port;
            _log = log ?? delegate { };
            _logDetail = logDetail ?? _log;
            _progressCallback = progressCallback;
            State = MtkDeviceState.Da1Loaded;
            
            // 如果提供了外部锁则使用，否则创建自己的
            if (portLock != null)
            {
                _portLock = portLock;
                _ownsPortLock = false;
            }
            else
            {
                _portLock = new SemaphoreSlim(1, 1);
                _ownsPortLock = true;
            }
        }

        /// <summary>
        /// 设置串口 (用于重连后更新端口引用)
        /// </summary>
        public void SetPort(SerialPort port)
        {
            _port = port;
        }
        
        /// <summary>
        /// 获取端口锁
        /// </summary>
        public SemaphoreSlim GetPortLock() => _portLock;
        
        /// <summary>
        /// 检查端口是否有效
        /// </summary>
        public bool IsPortValid => _port != null && _port.IsOpen;
        
        /// <summary>
        /// 验证端口状态，如果无效则抛出异常
        /// </summary>
        private void EnsurePortValid()
        {
            if (_port == null)
                throw new ObjectDisposedException("SerialPort", "端口对象为空，可能已被释放");
            if (!_port.IsOpen)
                throw new InvalidOperationException("端口已关闭，设备可能已断开");
        }

        #region XML 协议核心

        /// <summary>
        /// 发送 XML 命令 (内部方法，不加锁)
        /// </summary>
        private async Task XSendInternalAsync(string xmlCmd, CancellationToken ct = default)
        {
            // 验证端口状态
            EnsurePortValid();
            
            // 注意：不再清空缓冲区，因为设备可能发送主动消息（如 CMD:DOWNLOAD-FILE）
            // 清空会导致丢失重要请求
            
            byte[] data = Encoding.UTF8.GetBytes(xmlCmd);
            
            // 构建头部: Magic (4) + DataType (4) + Length (4)
            byte[] header = new byte[12];
            MtkDataPacker.WriteUInt32LE(header, 0, XML_MAGIC);
            MtkDataPacker.WriteUInt32LE(header, 4, (uint)DataType.ProtocolFlow);
            MtkDataPacker.WriteUInt32LE(header, 8, (uint)data.Length);

            _port.Write(header, 0, 12);
            _port.Write(data, 0, data.Length);

            _logDetail($"[XML] 发送: {xmlCmd.Substring(0, Math.Min(100, xmlCmd.Length))}...");
        }

        /// <summary>
        /// 发送 XML 命令 (带线程安全)
        /// </summary>
        private async Task XSendAsync(string xmlCmd, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                await XSendInternalAsync(xmlCmd, ct);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 接收 XML 响应 (内部方法，不加锁)
        /// </summary>
        private async Task<string> XRecvInternalAsync(int timeoutMs = DEFAULT_TIMEOUT_MS, CancellationToken ct = default)
        {
            // 读取头部
            byte[] header = await ReadBytesInternalAsync(12, timeoutMs, ct);
            if (header == null)
            {
                _log("[XML] 读取头部超时");
                return null;
            }

            // 验证魔数
            uint magic = MtkDataPacker.UnpackUInt32LE(header, 0);
            if (magic != XML_MAGIC)
            {
                _log($"[XML] 魔数不匹配: 0x{magic:X8}, 期望: 0x{XML_MAGIC:X8}");
                _logDetail($"[XML] 头部数据: {BitConverter.ToString(header).Replace("-", " ")}");
                
                // 尝试同步: 在接下来的数据中查找魔数
                _log("[XML] 尝试重新同步协议流...");
                bool synced = await TryResyncAsync(timeoutMs, ct);
                if (!synced)
                {
                    _log("[XML] 协议同步失败");
                    return null;
                }
                
                // 重新读取头部
                header = await ReadBytesInternalAsync(12, timeoutMs, ct);
                if (header == null)
                    return null;
                
                magic = MtkDataPacker.UnpackUInt32LE(header, 0);
                if (magic != XML_MAGIC)
                {
                    _log("[XML] 同步后仍然无法找到有效魔数");
                    return null;
                }
                
                _log("[XML] ✓ 协议重新同步成功");
            }

            uint dataType = MtkDataPacker.UnpackUInt32LE(header, 4);
            uint length = MtkDataPacker.UnpackUInt32LE(header, 8);

            if (length == 0)
                return "";

            if (length > MAX_BUFFER_SIZE)
            {
                _log($"[XML] 数据过大: {length} (最大: {MAX_BUFFER_SIZE})");
                return null;
            }

            // 读取数据
            byte[] data = await ReadBytesInternalAsync((int)length, timeoutMs, ct);
            if (data == null)
            {
                _log("[XML] 读取数据超时");
                return null;
            }

            string response = Encoding.UTF8.GetString(data);
            _logDetail($"[XML] 收到: {response.Substring(0, Math.Min(100, response.Length))}...");

            return response;
        }

        /// <summary>
        /// 尝试重新同步 XML 协议流
        /// </summary>
        private async Task<bool> TryResyncAsync(int timeoutMs, CancellationToken ct)
        {
            // 读取最多 1KB 数据尝试找到魔数
            const int maxSearchBytes = 1024;
            byte[] searchBuffer = new byte[maxSearchBytes];
            int totalRead = 0;
            
            DateTime start = DateTime.Now;
            
            while (totalRead < maxSearchBytes && (DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (_port.BytesToRead > 0)
                {
                    int toRead = Math.Min(_port.BytesToRead, maxSearchBytes - totalRead);
                    int actualRead = _port.Read(searchBuffer, totalRead, toRead);
                    totalRead += actualRead;
                    
                    // 在已读取的数据中查找魔数
                    for (int i = 0; i <= totalRead - 4; i++)
                    {
                        uint candidate = MtkDataPacker.UnpackUInt32LE(searchBuffer, i);
                        if (candidate == XML_MAGIC)
                        {
                            _logDetail($"[XML] 在偏移 {i} 找到魔数");
                            // 丢弃魔数之前的数据
                            // 将魔数之后的数据放回缓冲区(如果可能)
                            return true;
                        }
                    }
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }
            
            return false;
        }

        /// <summary>
        /// 接收 XML 响应 (带线程安全)
        /// </summary>
        private async Task<string> XRecvAsync(int timeoutMs = DEFAULT_TIMEOUT_MS, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                return await XRecvInternalAsync(timeoutMs, ct);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 发送命令并等待响应 (带线程安全)
        /// </summary>
        private async Task<XmlDocument> SendCommandAsync(string xmlCmd, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                await XSendInternalAsync(xmlCmd, ct);
                
                string response = await XRecvInternalAsync(DEFAULT_TIMEOUT_MS, ct);
                if (string.IsNullOrEmpty(response))
                    return null;

                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(response);
                    return doc;
                }
                catch (Exception ex)
                {
                    _log($"[XML] 解析响应失败: {ex.Message}");
                    return null;
                }
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 发送原始数据 (内部方法，不加锁)
        /// ★ 修正: 根据 mtkclient xsend，使用 DT_PROTOCOL_FLOW (1) 而不是 ProtocolRaw (2)
        /// </summary>
        private async Task XSendRawInternalAsync(byte[] data, CancellationToken ct = default)
        {
            byte[] header = new byte[12];
            MtkDataPacker.WriteUInt32LE(header, 0, XML_MAGIC);
            MtkDataPacker.WriteUInt32LE(header, 4, (uint)DataType.ProtocolFlow);  // DT_PROTOCOL_FLOW = 1
            MtkDataPacker.WriteUInt32LE(header, 8, (uint)data.Length);

            _port.Write(header, 0, 12);
            _port.Write(data, 0, data.Length);
        }
        
        /// <summary>
        /// 发送 ACK (根据 mtkclient xml_lib.py ack())
        /// mtkclient: def ack(self): return self.xsend("OK")
        /// </summary>
        private async Task SendAckAsync(CancellationToken ct = default)
        {
            // mtkclient: xsend 对字符串会加 null 终止符
            // length = len(data) + 1, usbwrite(bytes(data, 'utf-8') + b"\x00")
            byte[] data = Encoding.UTF8.GetBytes("OK\0");
            
            byte[] header = new byte[12];
            MtkDataPacker.WriteUInt32LE(header, 0, XML_MAGIC);
            MtkDataPacker.WriteUInt32LE(header, 4, (uint)DataType.ProtocolFlow);
            MtkDataPacker.WriteUInt32LE(header, 8, (uint)data.Length);

            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(header, 0, 12);
                _port.Write(data, 0, data.Length);
            }
            finally
            {
                _portLock.Release();
            }
        }
        
        /// <summary>
        /// 发送 ACK 带数值 (根据 mtkclient xml_lib.py ack_value())
        /// mtkclient: def ack_value(self, length): return self.xsend(f"OK@{hex(length)}")
        /// </summary>
        private async Task SendAckValueAsync(uint length, CancellationToken ct = default)
        {
            string ackStr = $"OK@0x{length:X}\0";  // 加 null 终止符
            byte[] data = Encoding.UTF8.GetBytes(ackStr);
            
            byte[] header = new byte[12];
            MtkDataPacker.WriteUInt32LE(header, 0, XML_MAGIC);
            MtkDataPacker.WriteUInt32LE(header, 4, (uint)DataType.ProtocolFlow);
            MtkDataPacker.WriteUInt32LE(header, 8, (uint)data.Length);

            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(header, 0, 12);
                _port.Write(data, 0, data.Length);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 发送原始数据 (带线程安全)
        /// </summary>
        private async Task XSendRawAsync(byte[] data, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                await XSendRawInternalAsync(data, ct);
            }
            finally
            {
                _portLock.Release();
            }
        }

        #endregion

        #region DA 连接

        /// <summary>
        /// 等待 DA 启动就绪
        /// </summary>
        public async Task<bool> WaitForDaReadyAsync(int timeoutMs = 30000, CancellationToken ct = default)
        {
            _log("[XML] 等待 DA 就绪...");

            DateTime start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (ct.IsCancellationRequested)
                    return false;

                try
                {
                    // 检查是否收到 DA 的初始消息
                    string response = await XRecvAsync(2000, ct);
                    if (!string.IsNullOrEmpty(response))
                    {
                        _logDetail($"[XML] 收到: {response.Substring(0, Math.Min(100, response.Length))}...");
                        
                        if (response.Contains("CMD:START") || response.Contains("ready"))
                        {
                            // 发送 "OK" 确认 (ChimeraTool 协议要求)
                            await SendOkAsync(ct);
                            _log("[XML] ✓ DA 已就绪");
                            IsConnected = true;
                            return true;
                        }
                    }
                }
                catch
                {
                    // 继续等待
                }

                await Task.Delay(100, ct);
            }

            _log("[XML] DA 就绪超时");
            return false;
        }
        
        /// <summary>
        /// 发送 OK 确认消息
        /// </summary>
        private async Task SendOkAsync(CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                // 构建 OK 响应: Magic + DataType(1) + Length(2) + "OK"
                byte[] header = new byte[12];
                MtkDataPacker.WriteUInt32LE(header, 0, XML_MAGIC);
                MtkDataPacker.WriteUInt32LE(header, 4, 1);  // DataType = 1
                MtkDataPacker.WriteUInt32LE(header, 8, 2);  // Length = 2
                
                _port.Write(header, 0, 12);
                _port.Write(Encoding.ASCII.GetBytes("OK"), 0, 2);
                
                _logDetail("[XML] 发送 OK 确认");
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 获取 Connection Agent (检测设备启动来源)
        /// 返回: "brom" 或 "preloader"
        /// </summary>
        public async Task<string> GetConnectionAgentAsync(CancellationToken ct = default)
        {
            _log("[XML] 获取 Connection Agent...");

            await _portLock.WaitAsync(ct);
            try
            {
                // 发送 GET_CONNECTION_AGENT 命令 (0x01)
                byte[] cmdHeader = new byte[12];
                MtkDataPacker.WriteUInt32LE(cmdHeader, 0, XML_MAGIC);
                MtkDataPacker.WriteUInt32LE(cmdHeader, 4, (uint)DataType.ProtocolFlow);
                MtkDataPacker.WriteUInt32LE(cmdHeader, 8, 4);
                _port.Write(cmdHeader, 0, 12);

                byte[] cmdData = new byte[4];
                MtkDataPacker.WriteUInt32LE(cmdData, 0, 0x01);  // GET_CONNECTION_AGENT
                _port.Write(cmdData, 0, 4);

                // 使用正确的 XML 协议格式读取响应
                // 先读取 12 字节头部
                var header = await ReadBytesInternalAsync(12, 3000, ct);
                if (header == null || header.Length < 12)
                {
                    _log("[XML] 警告: Connection Agent 头部读取失败");
                    return "preloader";
                }
                
                uint magic = MtkDataPacker.UnpackUInt32LE(header, 0);
                uint dataType = MtkDataPacker.UnpackUInt32LE(header, 4);
                uint length = MtkDataPacker.UnpackUInt32LE(header, 8);
                
                if (magic != XML_MAGIC)
                {
                    // 不是 XML 格式，尝试直接解析
                    string rawStr = System.Text.Encoding.ASCII.GetString(header).ToLower();
                    _logDetail($"[XML] 非标准响应: {rawStr}");
                    if (rawStr.Contains("brom")) return "brom";
                    if (rawStr.Contains("preloader") || rawStr.Contains("pl")) return "preloader";
                    return "preloader";
                }
                
                // 读取数据部分
                if (length > 0 && length < 1024)
                {
                    var data = await ReadBytesInternalAsync((int)length, 2000, ct);
                    if (data != null && data.Length > 0)
                    {
                        string agent = System.Text.Encoding.ASCII.GetString(data)
                            .TrimEnd('\0', ' ', '\r', '\n')
                            .ToLower();
                        
                        _logDetail($"[XML] Connection Agent 数据: \"{agent}\"");
                        
                        if (agent.Contains("brom") && !agent.Contains("preloader"))
                        {
                            _log("[XML] ✓ Connection Agent: brom (从Boot ROM启动)");
                            return "brom";
                        }
                        else if (agent.Contains("preloader") || agent.Contains("pl"))
                        {
                            _log("[XML] ✓ Connection Agent: preloader (从Preloader启动)");
                            return "preloader";
                        }
                    }
                }

                // 默认返回 preloader (大多数设备从preloader启动)
                _log("[XML] 无法明确判断，默认假设: preloader");
                return "preloader";
            }
            catch (Exception ex)
            {
                _log($"[XML] 获取 Connection Agent 异常: {ex.Message}");
                _logDetail($"[XML] 堆栈: {ex.StackTrace}");
                return "preloader";
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 设置运行时参数 (ChimeraTool 在 CMD:START 后发送)
        /// </summary>
        public async Task<bool> SetRuntimeParametersAsync(CancellationToken ct = default)
        {
            _logDetail("[XML] 设置运行时参数...");
            
            try
            {
                // ChimeraTool 发送的命令
                string cmd = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                            "<da>" +
                            "<version>1.0</version>" +
                            "<command>CMD:SET-RUNTIME-PARAMETER</command>" +
                            "<arg>" +
                            "<checksum_level>NONE</checksum_level>" +
                            "<da_log_level>ERROR</da_log_level>" +
                            "<log_channel>UART</log_channel>" +
                            "<battery_exist>AUTO-DETECT</battery_exist>" +
                            "<system_os>LINUX</system_os>" +
                            "</arg>" +
                            "<adv>" +
                            "<initialize_dram>YES</initialize_dram>" +
                            "</adv>" +
                            "</da>";
                
                var response = await SendCommandAsync(cmd, ct);
                if (response != null)
                {
                    var statusNode = response.SelectSingleNode("//status");
                    if (statusNode != null)
                    {
                        string status = statusNode.InnerText.ToUpper();
                        if (status == "OK" || status == "SUCCESS")
                        {
                            _logDetail("[XML] ✓ 运行时参数设置成功");
                            return true;
                        }
                    }
                    
                    // 检查是否有消息节点
                    var msgNode = response.SelectSingleNode("//message");
                    if (msgNode != null)
                    {
                        _logDetail($"[XML] 运行时参数响应: {msgNode.InnerText}");
                    }
                    
                    return true; // 即使没有明确的OK，也认为成功
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logDetail($"[XML] 设置运行时参数异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// DA 握手
        /// </summary>
        public async Task<bool> DaHandshakeAsync(CancellationToken ct = default)
        {
            _log("[XML] DA 握手...");

            // 发送握手命令
            string handshakeCmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                  "<da><version>1.0</version><command>CMD:CONNECT</command></da>";
            
            var response = await SendCommandAsync(handshakeCmd, ct);
            if (response == null)
            {
                _log("[XML] 握手无响应");
                return false;
            }

            // 检查响应
            var statusNode = response.SelectSingleNode("//status");
            if (statusNode != null && statusNode.InnerText == "OK")
            {
                _log("[XML] ✓ DA 握手成功");
                IsConnected = true;
                return true;
            }

            _log("[XML] DA 握手失败");
            return false;
        }

        #endregion

        #region XFlash 命令 (Carbonara 漏洞利用)

        /// <summary>
        /// boot_to 命令 - 向指定地址写入数据
        /// 这是 Carbonara 漏洞的核心: 可以向 DA1 内存中的任意地址写入数据
        /// 
        /// 根据 mtkclient xflash_lib.py boot_to() 修正:
        /// 1. xsend(BOOT_TO) -> status()
        /// 2. send_param(addr, len) 
        /// 3. send_data(da)
        /// 4. sleep(timeout)
        /// 5. status() - 检查 0x434E5953 或 0x0
        /// </summary>
        public async Task<bool> BootToAsync(uint address, byte[] data, bool display = true, int timeoutMs = 500, CancellationToken ct = default)
        {
            if (display)
                _log($"[XFlash] boot_to: 地址=0x{address:X8}, 大小={data.Length}");

            await _portLock.WaitAsync(ct);
            try
            {
                // 1. 发送 BOOT_TO 命令 (xsend)
                byte[] cmdHeader = new byte[12];
                MtkDataPacker.WriteUInt32LE(cmdHeader, 0, XML_MAGIC);
                MtkDataPacker.WriteUInt32LE(cmdHeader, 4, (uint)DataType.ProtocolFlow);
                MtkDataPacker.WriteUInt32LE(cmdHeader, 8, 4);
                _port.Write(cmdHeader, 0, 12);

                byte[] cmdData = new byte[4];
                MtkDataPacker.WriteUInt32LE(cmdData, 0, CMD_BOOT_TO);
                _port.Write(cmdData, 0, 4);

                // ★ 修正: 读取完整的 status 响应 (12字节头 + 数据)
                uint status = await ReadStatusAsync(5000, ct);
                if (status != 0)
                {
                    _log($"[XFlash] boot_to 命令错误: 0x{status:X}");
                    return false;
                }

                // 2. 发送参数 (地址 + 长度, 各 8 字节 for 64-bit)
                // mtkclient: param = pack("<QQ", addr, len(da))
                byte[] param = new byte[16];
                WriteUInt64LE(param, 0, address);
                WriteUInt64LE(param, 8, (ulong)data.Length);

                byte[] paramHeader = new byte[12];
                MtkDataPacker.WriteUInt32LE(paramHeader, 0, XML_MAGIC);
                MtkDataPacker.WriteUInt32LE(paramHeader, 4, (uint)DataType.ProtocolFlow);
                MtkDataPacker.WriteUInt32LE(paramHeader, 8, (uint)param.Length);
                _port.Write(paramHeader, 0, 12);
                _port.Write(param, 0, param.Length);

                // 3. 发送数据 (send_data)
                if (!await SendDataInternalAsync(data, ct))
                {
                    _log("[XFlash] boot_to 数据发送失败");
                    return false;
                }

                // 4. 等待 (mtkclient: time.sleep(timeout))
                if (timeoutMs > 0)
                    await Task.Delay(timeoutMs, ct);

                // 5. 读取最终状态
                uint result = await ReadStatusAsync(5000, ct);
                
                // 0x434E5953 = "SYNC" 或 0x0 = 成功
                if (result == 0x434E5953 || result == 0)
                {
                    if (display)
                        _log("[XFlash] ✓ boot_to 成功");
                    return true;
                }

                _log($"[XFlash] boot_to 失败: 0x{result:X}");
                return false;
            }
            catch (Exception ex)
            {
                _log($"[XFlash] boot_to 异常: {ex.Message}");
                return false;
            }
            finally
            {
                _portLock.Release();
            }
        }
        
        /// <summary>
        /// 读取 XFlash 状态响应 (根据 mtkclient xflash_lib.py status() 修正)
        /// 格式: [magic(4)][dataType(4)][length(4)][data(length)]
        /// </summary>
        private async Task<uint> ReadStatusAsync(int timeoutMs, CancellationToken ct)
        {
            // 读取 12 字节头
            var header = await ReadBytesInternalAsync(12, timeoutMs, ct);
            if (header == null || header.Length < 12)
            {
                _log("[XFlash] 状态读取失败: 头部不完整");
                return 0xFFFFFFFF;
            }
            
            uint magic = MtkDataPacker.UnpackUInt32LE(header, 0);
            if (magic != XML_MAGIC)
            {
                _log($"[XFlash] 状态错误: Magic 不匹配 0x{magic:X8}");
                return 0xFFFFFFFF;
            }
            
            uint length = MtkDataPacker.UnpackUInt32LE(header, 8);
            if (length == 0)
                return 0;
                
            // 读取数据部分
            var data = await ReadBytesInternalAsync((int)length, timeoutMs, ct);
            if (data == null || data.Length < length)
            {
                _log($"[XFlash] 状态读取失败: 数据不完整 ({data?.Length ?? 0}/{length})");
                return 0xFFFFFFFF;
            }
            
            // 根据长度解析状态
            if (length == 2)
            {
                return MtkDataPacker.UnpackUInt16LE(data, 0);
            }
            else if (length == 4)
            {
                uint status = MtkDataPacker.UnpackUInt32LE(data, 0);
                // 特殊情况: 如果返回 MAGIC，视为成功
                if (status == XML_MAGIC)
                    return 0;
                return status;
            }
            else if (length >= 4)
            {
                return MtkDataPacker.UnpackUInt32LE(data, 0);
            }
            
            return 0xFFFFFFFF;
        }

        /// <summary>
        /// 执行 Carbonara 漏洞利用
        /// </summary>
        public async Task<bool> ExecuteCarbonaraAsync(
            uint da1Address, 
            int hashOffset, 
            byte[] newHash, 
            uint da2Address, 
            byte[] patchedDa2, 
            CancellationToken ct = default)
        {
            _log("[Carbonara] 开始运行时漏洞利用...");

            // 1. 第一次 boot_to: 将新哈希写入 DA1 内存中的哈希位置
            uint hashWriteAddress = da1Address + (uint)hashOffset;
            _log($"[Carbonara] 写入新哈希到 0x{hashWriteAddress:X8}");

            if (!await BootToAsync(hashWriteAddress, newHash, display: true, timeoutMs: 100, ct: ct))
            {
                _log("[Carbonara] 哈希写入失败");
                return false;
            }

            _log("[Carbonara] ✓ 哈希写入成功");

            // 2. 第二次 boot_to: 上传修补后的 DA2
            _log($"[Carbonara] 上传修补后的 DA2 到 0x{da2Address:X8}");

            if (!await BootToAsync(da2Address, patchedDa2, display: true, timeoutMs: 500, ct: ct))
            {
                _log("[Carbonara] DA2 上传失败");
                return false;
            }

            _log("[Carbonara] ✓ Stage2 上传成功");
            
            // 3. 执行 SLA 授权 (如果需要)
            await Task.Delay(100, ct);  // 等待 DA2 初始化
            
            _log("[Carbonara] 检查 SLA 状态...");
            bool slaRequired = await CheckSlaStatusAsync(ct);
            
            if (slaRequired)
            {
                _log("[Carbonara] SLA 状态: 启用");
                _log("[Carbonara] 执行 SLA 授权...");
                
                if (!await ExecuteSlaAuthAsync(ct))
                {
                    _log("[Carbonara] ⚠ SLA 授权失败，但继续尝试");
                    // 不返回失败，继续尝试
                }
                else
                {
                    _log("[Carbonara] ✓ SLA 授权成功");
                }
            }
            else
            {
                _log("[Carbonara] SLA 状态: 禁用 (无需授权)");
            }
            
            _log("[Carbonara] ✓ 运行时漏洞利用成功！");
            State = MtkDeviceState.Da2Loaded;
            IsConnected = true;

            return true;
        }
        
        /// <summary>
        /// 检查 SLA 状态
        /// </summary>
        private async Task<bool> CheckSlaStatusAsync(CancellationToken ct = default)
        {
            try
            {
                // 发送 SLA 状态检查命令
                string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                            "<da><version>1.0</version>" +
                            "<command>CMD:GET-SLA</command>" +
                            "</da>";
                
                var response = await SendCommandAsync(cmd, ct);
                if (response != null)
                {
                    var statusNode = response.SelectSingleNode("//sla") ?? 
                                    response.SelectSingleNode("//status");
                    if (statusNode != null)
                    {
                        string status = statusNode.InnerText.ToUpper();
                        return status == "ENABLED" || status == "1" || status == "TRUE";
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 执行 SLA 授权
        /// </summary>
        private async Task<bool> ExecuteSlaAuthAsync(CancellationToken ct = default)
        {
            try
            {
                // SLA 授权流程:
                // 1. 发送 SLA-CHALLENGE 请求
                // 2. 收到 challenge 数据
                // 3. 使用 SLA 证书签名
                // 4. 发送签名响应
                
                string challengeCmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                     "<da><version>1.0</version>" +
                                     "<command>CMD:SLA-CHALLENGE</command>" +
                                     "</da>";
                
                var challengeResponse = await SendCommandAsync(challengeCmd, ct);
                if (challengeResponse == null)
                {
                    _logDetail("[SLA] 无法获取 challenge");
                    return false;
                }
                
                // 解析 challenge
                var challengeNode = challengeResponse.SelectSingleNode("//challenge");
                if (challengeNode == null)
                {
                    // 可能设备不需要 SLA 或已授权
                    var statusNode = challengeResponse.SelectSingleNode("//status");
                    if (statusNode != null && statusNode.InnerText.Contains("OK"))
                    {
                        return true;
                    }
                    _logDetail("[SLA] 无 challenge 数据");
                    return false;
                }
                
                // 获取 challenge 数据
                string challengeHex = challengeNode.InnerText;
                byte[] challenge = HexToBytes(challengeHex);
                
                _logDetail($"[SLA] 收到 challenge: {challenge.Length} 字节");
                
                // 使用内置 SLA 证书签名 (占位实现)
                // 实际需要根据设备类型加载正确的 SLA 证书
                byte[] signature = await MtkSlaAuth.SignChallengeAsync(challenge, ct);
                
                if (signature == null || signature.Length == 0)
                {
                    _logDetail("[SLA] 签名生成失败");
                    return false;
                }
                
                _logDetail($"[SLA] 生成签名: {signature.Length} 字节");
                _progressCallback?.Invoke(50);
                
                // 发送签名响应
                string signatureHex = BytesToHex(signature);
                string authCmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                                "<da><version>1.0</version>" +
                                "<command>CMD:SLA-AUTH</command>" +
                                "<arg>" +
                                $"<signature>{signatureHex}</signature>" +
                                "</arg>" +
                                "</da>";
                
                var authResponse = await SendCommandAsync(authCmd, ct);
                _progressCallback?.Invoke(100);
                
                if (authResponse != null)
                {
                    var resultNode = authResponse.SelectSingleNode("//status") ??
                                    authResponse.SelectSingleNode("//result");
                    if (resultNode != null)
                    {
                        string result = resultNode.InnerText.ToUpper();
                        return result == "OK" || result == "SUCCESS" || result == "0";
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logDetail($"[SLA] 授权异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Hex 字符串转字节数组
        /// </summary>
        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            hex = hex.Replace(" ", "").Replace("-", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        
        /// <summary>
        /// 字节数组转 Hex 字符串
        /// </summary>
        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "";
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        #endregion

        #region 认证命令 (CMD:SEND-AUTH / CMD:GET-RANDOM)

        /// <summary>
        /// 发送认证数据 (CMD:SEND-AUTH)
        /// 用于发送证书、签名或其他认证信息到设备
        /// </summary>
        /// <param name="authData">认证数据 (证书/签名)</param>
        /// <param name="authType">认证类型: "CERT", "SIGNATURE", "AUTH_FILE"</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SendAuthAsync(byte[] authData, string authType = "AUTH_FILE", CancellationToken ct = default)
        {
            if (authData == null || authData.Length == 0)
            {
                _log("[AUTH] 认证数据为空");
                return false;
            }

            _log($"[AUTH] 发送认证数据: {authData.Length} 字节, 类型: {authType}");

            try
            {
                string authHex = BytesToHex(authData);
                
                string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                            "<da><version>1.0</version>" +
                            $"<command>{XmlDaCommands.CMD_SEND_AUTH}</command>" +
                            "<arg>" +
                            $"<type>{authType}</type>" +
                            $"<length>{authData.Length}</length>" +
                            $"<data>{authHex}</data>" +
                            "</arg>" +
                            "</da>";

                var response = await SendCommandAsync(cmd, ct);
                if (response != null)
                {
                    var statusNode = response.SelectSingleNode("//status");
                    if (statusNode != null)
                    {
                        string status = statusNode.InnerText.ToUpper();
                        if (status == "OK" || status == "SUCCESS" || status == "0")
                        {
                            _log("[AUTH] ✓ 认证数据发送成功");
                            return true;
                        }
                        
                        // 检查错误信息
                        var errorNode = response.SelectSingleNode("//error") ?? 
                                       response.SelectSingleNode("//message");
                        if (errorNode != null)
                        {
                            _log($"[AUTH] 认证失败: {errorNode.InnerText}");
                        }
                        else
                        {
                            _log($"[AUTH] 认证失败: {status}");
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _log($"[AUTH] 发送认证数据异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件发送认证数据
        /// </summary>
        /// <param name="authFilePath">认证文件路径</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SendAuthFromFileAsync(string authFilePath, CancellationToken ct = default)
        {
            if (!System.IO.File.Exists(authFilePath))
            {
                _log($"[AUTH] 认证文件不存在: {authFilePath}");
                return false;
            }

            byte[] authData = System.IO.File.ReadAllBytes(authFilePath);
            return await SendAuthAsync(authData, "AUTH_FILE", ct);
        }

        /// <summary>
        /// 获取随机数 (CMD:GET-RANDOM) - 默认禁用
        /// 用于 SLA 认证流程中的质询-响应机制
        /// 注意: 此命令在某些设备上可能被禁用
        /// </summary>
        /// <param name="length">请求的随机数长度 (字节)</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>随机数数据，失败返回 null</returns>
        public async Task<byte[]> GetRandomAsync(int length = 16, CancellationToken ct = default)
        {
            _logDetail($"[AUTH] 获取随机数: {length} 字节");

            try
            {
                string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                            "<da><version>1.0</version>" +
                            $"<command>{XmlDaCommands.CMD_GET_RANDOM}</command>" +
                            "<arg>" +
                            $"<length>{length}</length>" +
                            "</arg>" +
                            "</da>";

                var response = await SendCommandAsync(cmd, ct);
                if (response != null)
                {
                    // 首先检查状态
                    var statusNode = response.SelectSingleNode("//status");
                    if (statusNode != null)
                    {
                        string status = statusNode.InnerText.ToUpper();
                        if (status == "DISABLED" || status == "NOT_SUPPORTED")
                        {
                            _logDetail("[AUTH] 随机数功能已禁用");
                            return null;
                        }
                    }

                    // 获取随机数数据
                    var randomNode = response.SelectSingleNode("//random") ??
                                    response.SelectSingleNode("//data");
                    if (randomNode != null)
                    {
                        string randomHex = randomNode.InnerText;
                        byte[] randomData = HexToBytes(randomHex);
                        
                        if (randomData.Length > 0)
                        {
                            _logDetail($"[AUTH] ✓ 获取随机数成功: {randomData.Length} 字节");
                            return randomData;
                        }
                    }
                }

                _logDetail("[AUTH] 获取随机数失败");
                return null;
            }
            catch (Exception ex)
            {
                _logDetail($"[AUTH] 获取随机数异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 完整 SLA 认证流程 (使用 CMD:GET-RANDOM + CMD:SEND-AUTH)
        /// </summary>
        /// <param name="slaKey">SLA 私钥数据</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public async Task<bool> PerformSlaAuthWithRandomAsync(byte[] slaKey, CancellationToken ct = default)
        {
            _log("[SLA] 开始完整 SLA 认证流程...");

            try
            {
                // 1. 获取随机数作为质询
                byte[] challenge = await GetRandomAsync(16, ct);
                if (challenge == null)
                {
                    _log("[SLA] 无法获取随机数，尝试使用 SLA-CHALLENGE 方式");
                    return await ExecuteSlaAuthAsync(ct);
                }

                _log($"[SLA] 收到质询: {BytesToHex(challenge).Substring(0, Math.Min(16, challenge.Length * 2))}...");

                // 2. 使用 SLA 密钥签名质询
                byte[] signature = await MtkSlaAuth.SignChallengeAsync(challenge, ct);
                if (signature == null || signature.Length == 0)
                {
                    _log("[SLA] 签名生成失败");
                    return false;
                }

                _log($"[SLA] 生成签名: {signature.Length} 字节");

                // 3. 发送签名作为认证数据
                bool result = await SendAuthAsync(signature, "SIGNATURE", ct);
                
                if (result)
                {
                    _log("[SLA] ✓ SLA 认证成功");
                }
                else
                {
                    _log("[SLA] SLA 认证失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                _log($"[SLA] SLA 认证异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 数据传输

        /// <summary>
        /// 发送数据 (内部方法)
        /// 根据 mtkclient xflash_lib.py send_data() 修正:
        /// pkt2 = pack("<III", MAGIC, DT_PROTOCOL_FLOW, len(data))
        /// </summary>
        private async Task<bool> SendDataInternalAsync(byte[] data, CancellationToken ct = default)
        {
            try
            {
                // ★ 修正: 使用 ProtocolFlow (1) 而不是 ProtocolRaw (2)
                byte[] header = new byte[12];
                MtkDataPacker.WriteUInt32LE(header, 0, XML_MAGIC);
                MtkDataPacker.WriteUInt32LE(header, 4, (uint)DataType.ProtocolFlow);  // DT_PROTOCOL_FLOW
                MtkDataPacker.WriteUInt32LE(header, 8, (uint)data.Length);
                _port.Write(header, 0, 12);

                // 分块发送数据 (参考 mtkclient: maxoutsize = EP_OUT.wMaxPacketSize)
                int maxPacketSize = 0x200;  // 默认 512 字节
                int offset = 0;
                while (offset < data.Length)
                {
                    if (ct.IsCancellationRequested)
                        return false;

                    int toSend = Math.Min(maxPacketSize, data.Length - offset);
                    _port.Write(data, offset, toSend);
                    offset += toSend;

                    _progressCallback?.Invoke((double)offset * 100 / data.Length);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region DA2 上传

        /// <summary>
        /// 上传 DA2 - ChimeraTool 协议
        /// 设备主动发送 CMD:DOWNLOAD-FILE 请求，主机响应 OK@size 然后发送数据
        /// </summary>
        public async Task<bool> UploadDa2Async(DaEntry da2, CancellationToken ct = default)
        {
            if (da2 == null || da2.Data == null)
            {
                _log("[XML] DA2 数据为空");
                return false;
            }

            _log($"[XML] 上传 DA2: {da2.Data.Length} 字节");
            
            // 0. 处理之前的消息 (CMD:END, CMD:START 等)
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    string msg = await XRecvAsync(500, ct);
                    if (!string.IsNullOrEmpty(msg))
                    {
                        _logDetail($"[XML] 处理消息: {msg.Substring(0, Math.Min(80, msg.Length))}...");
                        if (msg.Contains("CMD:END") || msg.Contains("CMD:START") || msg.Contains("CMD:PROGRESS"))
                        {
                            await SendOkAsync(ct);
                        }
                    }
                }
                catch { }
            }
            
            // 1. 发送 BOOT-TO 命令触发 DA2 下载
            _log("[XML] 发送 BOOT-TO 命令...");
            string bootToCmd = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                              "<da>" +
                              "<version>1.0</version>" +
                              "<command>CMD:BOOT-TO</command>" +
                              "<arg>" +
                              $"<at_address>0x{da2.LoadAddr:X8}</at_address>" +
                              $"<jmp_address>0x{da2.LoadAddr:X8}</jmp_address>" +
                              "<source_file>Boot to</source_file>" +
                              "</arg>" +
                              "</da>";
            
            await XSendAsync(bootToCmd, ct);
            
            _log("[XML] 等待设备请求 DA2...");
            
            // 2. 等待设备发送 CMD:DOWNLOAD-FILE 请求
            bool receivedRequest = false;
            int packetLength = 0x1000; // 默认 4KB
            
            for (int retry = 0; retry < 30 && !receivedRequest; retry++)
            {
                try
                {
                    // 读取设备消息
                    string msg = await XRecvAsync(1000, ct);
                    if (string.IsNullOrEmpty(msg))
                    {
                        continue;
                    }
                    
                    _logDetail($"[XML] 收到消息: {msg.Substring(0, Math.Min(100, msg.Length))}...");
                    
                    // 检查是否是 DOWNLOAD-FILE 请求
                    if (msg.Contains("CMD:DOWNLOAD-FILE") && msg.Contains("2nd-DA"))
                    {
                        _log("[XML] ✓ 收到 DA2 下载请求");
                        
                        // 解析 packet_length
                        var match = System.Text.RegularExpressions.Regex.Match(
                            msg, @"<packet_length>0x([0-9A-Fa-f]+)</packet_length>");
                        if (match.Success)
                        {
                            packetLength = Convert.ToInt32(match.Groups[1].Value, 16);
                            _logDetail($"[XML] 数据块大小: 0x{packetLength:X}");
                        }
                        
                        // 重要：先发送 OK 确认 DOWNLOAD-FILE 请求
                        _log("[XML] 发送 OK 确认 DOWNLOAD-FILE...");
                        await SendOkAsync(ct);
                        
                        receivedRequest = true;
                    }
                    else if (msg.Contains("CMD:START") || msg.Contains("CMD:PROGRESS-REPORT"))
                    {
                        // 设备发送其他消息，响应 OK
                        await SendOkAsync(ct);
                    }
                    else if (msg.Contains("CMD:END"))
                    {
                        // 命令完成，响应 OK
                        await SendOkAsync(ct);
                    }
                }
                catch (TimeoutException)
                {
                    // 继续等待
                }
            }
            
            if (!receivedRequest)
            {
                _log("[XML] 超时：未收到 DA2 下载请求");
                return false;
            }
            
            // 2. 响应 OK@<size> (告诉设备文件大小)
            string sizeResponse = $"OK@{da2.Data.Length} ";
            _log($"[XML] 发送大小响应: {sizeResponse}");
            
            // 手动构建并发送（更精确控制）
            byte[] sizePayload = Encoding.ASCII.GetBytes(sizeResponse);
            byte[] sizeHeader = new byte[12];
            sizeHeader[0] = 0xEF; sizeHeader[1] = 0xEE; sizeHeader[2] = 0xEE; sizeHeader[3] = 0xFE;
            sizeHeader[4] = 0x01; sizeHeader[5] = 0x00; sizeHeader[6] = 0x00; sizeHeader[7] = 0x00;
            sizeHeader[8] = (byte)(sizePayload.Length & 0xFF);
            sizeHeader[9] = (byte)((sizePayload.Length >> 8) & 0xFF);
            sizeHeader[10] = 0; sizeHeader[11] = 0;
            
            _logDetail($"[XML] 发送头部: {BitConverter.ToString(sizeHeader).Replace("-", " ")}");
            _logDetail($"[XML] 发送数据: {BitConverter.ToString(sizePayload).Replace("-", " ")}");
            
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(sizeHeader, 0, 12);
                _port.BaseStream.Flush();
                _port.Write(sizePayload, 0, sizePayload.Length);
                _port.BaseStream.Flush();
            }
            finally
            {
                _portLock.Release();
            }
            
            // 等待一下让设备处理
            await Task.Delay(100, ct);
            
            // 检查串口是否有数据
            _log($"[XML] 串口缓冲区: {_port.BytesToRead} 字节");
            
            bool deviceReady = false;
            
            if (_port.BytesToRead > 0)
            {
                byte[] rawResp = new byte[Math.Min(_port.BytesToRead, 256)];
                int rawRead = _port.Read(rawResp, 0, rawResp.Length);
                _log($"[XML] 原始响应 ({rawRead} 字节): {BitConverter.ToString(rawResp, 0, rawRead).Replace("-", " ")}");
                
                // 检查是否是 "OK" 响应
                if (rawRead >= 3 && rawResp[0] == 0xEF && rawResp[1] == 0xEE)
                {
                    _log("[XML] ✓ 收到设备确认");
                    // 发送 OK 回复
                    await SendOkAsync(ct);
                    
                    // 再等待第二个确认
                    await Task.Delay(50, ct);
                    if (_port.BytesToRead > 0)
                    {
                        byte[] ack2 = new byte[_port.BytesToRead];
                        _port.Read(ack2, 0, ack2.Length);
                        _logDetail($"[XML] 第二次响应: {BitConverter.ToString(ack2).Replace("-", " ")}");
                    }
                    
                    deviceReady = true;
                }
            }
            
            if (!deviceReady)
            {
                _log("[XML] ⚠ 设备无响应，尝试继续发送数据...");
            }
            
            // 不再额外等待，直接开始发送数据
            
            // 4. 分块发送 DA2 数据
            _log($"[XML] 开始发送 DA2 数据: {da2.Data.Length} 字节, 块大小: {packetLength}");
            
            int offset = 0;
            int totalChunks = (da2.Data.Length + packetLength - 1) / packetLength;
            int chunkIndex = 0;
            
            while (offset < da2.Data.Length)
            {
                int remaining = da2.Data.Length - offset;
                int chunkSize = Math.Min(remaining, packetLength);
                
                byte[] chunk = new byte[chunkSize];
                Array.Copy(da2.Data, offset, chunk, 0, chunkSize);
                
                // 第一块显示更多信息
                if (chunkIndex == 0)
                {
                    _log($"[XML] 发送第一块: {chunkSize} 字节");
                    _logDetail($"[XML] 前16字节: {BitConverter.ToString(chunk, 0, Math.Min(16, chunk.Length)).Replace("-", " ")}");
                }
                
                // 使用 XML 头部格式发送数据块
                await XSendBinaryAsync(chunk, ct);
                
                offset += chunkSize;
                chunkIndex++;
                
                if (chunkIndex % 20 == 0 || offset >= da2.Data.Length)
                {
                    _log($"[XML] 已发送: {offset}/{da2.Data.Length} ({100 * offset / da2.Data.Length}%)");
                }
                
                // 等待设备 ACK (严格同步: 必须收到 ACK 才能继续)
                int ackWaitMs = 0;
                const int maxAckWaitMs = 5000;  // 最大等待 5 秒 (大文件写入可能较慢)
                const int ackPollIntervalMs = 10;
                int noAckRetries = 0;
                const int maxNoAckRetries = 3;  // 最大重试次数
                
                while (_port.BytesToRead == 0 && ackWaitMs < maxAckWaitMs)
                {
                    await Task.Delay(ackPollIntervalMs, ct);
                    ackWaitMs += ackPollIntervalMs;
                }
                
                // 严格检查: 如果超时未收到 ACK，不能继续发送
                if (_port.BytesToRead == 0)
                {
                    noAckRetries++;
                    if (noAckRetries >= maxNoAckRetries)
                    {
                        _log($"[XML] ✗ 块 {chunkIndex} 发送后未收到 ACK (超时 {maxAckWaitMs}ms)，设备可能已停止响应");
                        _log("[XML] 提示: 可能是设备缓冲区溢出或连接中断");
                        return false;
                    }
                    _log($"[XML] ⚠ 块 {chunkIndex} 无 ACK，重试 {noAckRetries}/{maxNoAckRetries}");
                    // 回退 offset，重发当前块
                    offset -= chunkSize;
                    chunkIndex--;
                    await Task.Delay(100, ct);  // 稍等后重试
                    continue;
                }
                
                // 收到数据，重置重试计数
                noAckRetries = 0;
                
                if (_port.BytesToRead > 0)
                {
                    byte[] rawAck = new byte[Math.Min(_port.BytesToRead, 64)];
                    int ackRead = _port.Read(rawAck, 0, rawAck.Length);
                    string ackStr = Encoding.ASCII.GetString(rawAck, 0, ackRead).TrimEnd('\0');
                    
                    if (chunkIndex <= 3)
                    {
                        _log($"[XML] 块{chunkIndex} ACK ({ackRead}字节): {BitConverter.ToString(rawAck, 0, ackRead).Replace("-", " ")}");
                    }
                    
                    // 检查是否包含 ERR
                    if (ackStr.Contains("ERR"))
                    {
                        _log($"[XML] ✗ 设备返回错误");
                        
                        // 尝试读取完整的错误消息 (可能是 XML)
                        await Task.Delay(100, ct);
                        if (_port.BytesToRead > 0)
                        {
                            byte[] errMsg = new byte[Math.Min(_port.BytesToRead, 1024)];
                            int errRead = _port.Read(errMsg, 0, errMsg.Length);
                            string errStr = Encoding.UTF8.GetString(errMsg, 0, errRead);
                            _log($"[XML] 错误详情: {errStr}");
                        }
                        
                        // 检查 rawAck 中是否已经包含 XML 错误消息
                        // 格式: OK(15) + ERR(16) + XML(头部12 + 数据)
                        if (ackRead > 31)
                        {
                            // 解析 XML 部分 - 找到 XML 头部 (跳过 OK + ERR 包)
                            for (int i = 0; i < ackRead - 12; i++)
                            {
                                if (rawAck[i] == 0xEF && rawAck[i+1] == 0xEE && rawAck[i+2] == 0xEE && rawAck[i+3] == 0xFE)
                                {
                                    if (i + 12 < ackRead)
                                    {
                                        int xmlLen = rawAck[i+8] | (rawAck[i+9] << 8) | (rawAck[i+10] << 16) | (rawAck[i+11] << 24);
                                        int payloadStart = i + 12;
                                        int payloadLen = Math.Min(xmlLen, ackRead - payloadStart);
                                        if (payloadLen > 0)
                                        {
                                            string xmlPart = Encoding.UTF8.GetString(rawAck, payloadStart, payloadLen);
                                            _log($"[XML] 错误消息: {xmlPart}");
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        
                        return false;
                    }
                    
                    // 检查是否是 OK ACK (EF EE EE FE ... 4F 4B 00)
                    if (ackRead >= 12 && rawAck[0] == 0xEF && rawAck[1] == 0xEE)
                    {
                        // 发送 OK 确认
                        await SendOkAsync(ct);
                        
                        // 等待第二个 ACK
                        await Task.Delay(10, ct);
                        if (_port.BytesToRead > 0)
                        {
                            byte[] ack2 = new byte[_port.BytesToRead];
                            _port.Read(ack2, 0, ack2.Length);
                            // 继续下一块
                        }
                    }
                    
                    // 检查是否是 CMD:END
                    if (ackStr.Contains("CMD:END"))
                    {
                        _log("[XML] 收到 CMD:END");
                        await SendOkAsync(ct);
                        break;
                    }
                }
            }
            
            _log($"[XML] DA2 数据发送完成: {offset} 字节");
            
            // 4. 等待最终确认
            for (int i = 0; i < 10; i++)
            {
                string finalMsg = await XRecvAsync(1000, ct);
                if (!string.IsNullOrEmpty(finalMsg))
                {
                    _logDetail($"[XML] 最终响应: {finalMsg.Substring(0, Math.Min(100, finalMsg.Length))}...");
                    
                    if (finalMsg.Contains("CMD:END"))
                    {
                        await SendOkAsync(ct);
                        
                        if (finalMsg.Contains("OK") || finalMsg.Contains("result>OK"))
                        {
                            _log("[XML] ✓ DA2 上传成功");
                            State = MtkDeviceState.Da2Loaded;
                            return true;
                        }
                    }
                    else if (finalMsg.Contains("CMD:START") || finalMsg.Contains("CMD:PROGRESS-REPORT"))
                    {
                        await SendOkAsync(ct);
                    }
                }
            }
            
            _log("[XML] ✓ DA2 上传完成 (假设成功)");
            State = MtkDeviceState.Da2Loaded;
            return true;
        }
        
        /// <summary>
        /// 发送原始字符串 (带 XML 头部)
        /// </summary>
        private async Task XSendRawAsync(string data, CancellationToken ct = default)
        {
            byte[] payload = Encoding.ASCII.GetBytes(data);
            
            // 构建头部: magic(4) + dataType(4) + length(4)
            byte[] header = new byte[12];
            header[0] = 0xEF; header[1] = 0xEE; header[2] = 0xEE; header[3] = 0xFE; // Magic
            header[4] = 0x01; header[5] = 0x00; header[6] = 0x00; header[7] = 0x00; // DataType = 1
            
            // Length (little-endian)
            int len = payload.Length;
            header[8] = (byte)(len & 0xFF);
            header[9] = (byte)((len >> 8) & 0xFF);
            header[10] = (byte)((len >> 16) & 0xFF);
            header[11] = (byte)((len >> 24) & 0xFF);
            
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(header, 0, 12);
                _port.BaseStream.Flush();
                _port.Write(payload, 0, payload.Length);
                _port.BaseStream.Flush();
            }
            finally
            {
                _portLock.Release();
            }
        }
        
        /// <summary>
        /// 发送二进制数据 (带 XML 头部)
        /// ChimeraTool 将 header 和 data 作为两个独立的 USB 事务发送
        /// </summary>
        private async Task XSendBinaryAsync(byte[] data, CancellationToken ct = default)
        {
            // 验证端口状态
            EnsurePortValid();
            
            // 构建头部: magic(4) + dataType(4) + length(4)
            byte[] header = new byte[12];
            header[0] = 0xEF; header[1] = 0xEE; header[2] = 0xEE; header[3] = 0xFE; // Magic
            header[4] = 0x01; header[5] = 0x00; header[6] = 0x00; header[7] = 0x00; // DataType = 1
            
            // Length (little-endian)
            int len = data.Length;
            header[8] = (byte)(len & 0xFF);
            header[9] = (byte)((len >> 8) & 0xFF);
            header[10] = (byte)((len >> 16) & 0xFF);
            header[11] = (byte)((len >> 24) & 0xFF);
            
            await _portLock.WaitAsync(ct);
            try
            {
                // 分开发送 header 和 data (模拟两个 USB 事务)
                _port.Write(header, 0, 12);
                _port.BaseStream.Flush();
                
                // 短暂延迟确保分开传输
                await Task.Delay(1, ct);
                
                _port.Write(data, 0, data.Length);
                _port.BaseStream.Flush();
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 上传数据块
        /// </summary>
        private async Task UploadDataAsync(byte[] data, CancellationToken ct = default)
        {
            int chunkSize = 4096;
            int totalSent = 0;

            while (totalSent < data.Length)
            {
                if (ct.IsCancellationRequested)
                    break;

                int remaining = data.Length - totalSent;
                int sendSize = Math.Min(chunkSize, remaining);

                byte[] chunk = new byte[sendSize];
                Array.Copy(data, totalSent, chunk, 0, sendSize);

                await XSendRawAsync(chunk, ct);
                totalSent += sendSize;

                // 更新进度
                double progress = (double)totalSent * 100 / data.Length;
                _progressCallback?.Invoke(progress);
            }
        }

        #endregion

        #region Flash 操作

        /// <summary>
        /// 读取分区表
        /// </summary>
        public async Task<MtkPartitionInfo[]> ReadPartitionTableAsync(CancellationToken ct = default)
        {
            _log("[XML] 读取分区表...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         "<da><version>1.0</version><command>CMD:READ_PARTITION_TABLE</command></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return null;

            var partitions = new System.Collections.Generic.List<MtkPartitionInfo>();
            var partitionNodes = response.SelectNodes("//partition");

            if (partitionNodes != null)
            {
                foreach (XmlNode node in partitionNodes)
                {
                    var partition = new MtkPartitionInfo
                    {
                        Name = node.SelectSingleNode("name")?.InnerText ?? "",
                        StartSector = ParseULong(node.SelectSingleNode("start_sector")?.InnerText),
                        SectorCount = ParseULong(node.SelectSingleNode("sector_count")?.InnerText),
                        Size = ParseULong(node.SelectSingleNode("size")?.InnerText),
                        Type = node.SelectSingleNode("type")?.InnerText ?? ""
                    };
                    partitions.Add(partition);
                }
            }

            _log($"[XML] 读取到 {partitions.Count} 个分区");
            return partitions.ToArray();
        }

        /// <summary>
        /// 读取分区
        /// </summary>
        public async Task<byte[]> ReadPartitionAsync(string partitionName, ulong size, CancellationToken ct = default)
        {
            _log($"[XML] 读取分区: {partitionName}");

            string cmd = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         $"<da><version>1.0</version><command>CMD:READ_PARTITION</command>" +
                         $"<arg><partition_name>{partitionName}</partition_name>" +
                         $"<read_size>{size}</read_size></arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return null;

            var statusNode = response.SelectSingleNode("//status");
            if (statusNode == null || statusNode.InnerText != "READY")
                return null;

            // 接收数据
            using (var ms = new MemoryStream())
            {
                ulong received = 0;
                while (received < size)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var chunk = await ReadDataChunkAsync(ct);
                    if (chunk == null || chunk.Length == 0)
                        break;

                    ms.Write(chunk, 0, chunk.Length);
                    received += (ulong)chunk.Length;

                    double progress = (double)received * 100 / size;
                    _progressCallback?.Invoke(progress);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 写入分区
        /// </summary>
        public async Task<bool> WritePartitionAsync(string partitionName, byte[] data, CancellationToken ct = default)
        {
            _log($"[XML] 写入分区: {partitionName} ({data.Length} 字节)");

            string cmd = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         $"<da><version>1.0</version><command>CMD:WRITE_PARTITION</command>" +
                         $"<arg><partition_name>{partitionName}</partition_name>" +
                         $"<write_size>{data.Length}</write_size></arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return false;

            var statusNode = response.SelectSingleNode("//status");
            if (statusNode == null || statusNode.InnerText != "READY")
                return false;

            // 发送数据
            await UploadDataAsync(data, ct);

            // 等待完成
            string completeResponse = await XRecvAsync(DEFAULT_TIMEOUT_MS * 2, ct);
            if (completeResponse != null && completeResponse.Contains("OK"))
            {
                _log($"[XML] ✓ 分区 {partitionName} 写入成功");
                return true;
            }

            _log($"[XML] 分区 {partitionName} 写入失败");
            return false;
        }

        /// <summary>
        /// 擦除分区
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName, CancellationToken ct = default)
        {
            _log($"[XML] 擦除分区: {partitionName}");

            string cmd = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         $"<da><version>1.0</version><command>CMD:ERASE_PARTITION</command>" +
                         $"<arg><partition_name>{partitionName}</partition_name></arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return false;

            var statusNode = response.SelectSingleNode("//status");
            return statusNode != null && statusNode.InnerText == "OK";
        }

        /// <summary>
        /// 格式化所有分区
        /// </summary>
        public async Task<bool> FormatAllAsync(CancellationToken ct = default)
        {
            _log("[XML] 格式化所有分区...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         "<da><version>1.0</version><command>CMD:FORMAT_ALL</command></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return false;

            var statusNode = response.SelectSingleNode("//status");
            return statusNode != null && statusNode.InnerText == "OK";
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task<bool> RebootAsync(CancellationToken ct = default)
        {
            _log("[XML] 重启设备...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         "<da><version>1.0</version><command>CMD:REBOOT</command></da>";

            await XSendAsync(cmd, ct);
            return true;
        }

        /// <summary>
        /// 关机
        /// </summary>
        public async Task<bool> ShutdownAsync(CancellationToken ct = default)
        {
            _log("[XML] 关闭设备...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         "<da><version>1.0</version><command>CMD:SHUTDOWN</command></da>";

            await XSendAsync(cmd, ct);
            return true;
        }

        #endregion

        #region 扩展命令 (iReverse 参考)

        /// <summary>
        /// 设置启动模式
        /// </summary>
        /// <param name="mode">启动模式: FASTBOOT, META, ANDROID-TEST-MODE</param>
        /// <param name="adb">是否启用 ADB</param>
        /// <param name="mobileLog">是否启用 Mobile Log</param>
        /// <param name="connectType">连接类型: USB 或 UART</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> SetBootModeAsync(string mode = "ANDROID-TEST-MODE", bool adb = true, 
            bool mobileLog = true, string connectType = "USB", CancellationToken ct = default)
        {
            _log($"[XML] 设置启动模式: {mode}");

            string adbStr = adb ? "ON" : "OFF";
            string logStr = mobileLog ? "ON" : "OFF";

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_SET_BOOT_MODE}</command>" +
                        "<arg>" +
                        $"<mode>{mode}</mode>" +
                        $"<connect_type>{connectType}</connect_type>" +
                        $"<mobile_log>{logStr}</mobile_log>" +
                        $"<adb>{adbStr}</adb>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null && statusNode.InnerText.ToUpper() == "OK")
                {
                    _log($"[XML] ✓ 启动模式已设置为 {mode}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 设置 Flash 策略
        /// </summary>
        /// <param name="hostOffset">主机内存偏移</param>
        /// <param name="length">数据长度</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> SetFlashPolicyAsync(long hostOffset = 0x8000000, long length = 0x100000, 
            CancellationToken ct = default)
        {
            _logDetail("[XML] 设置 Flash 策略...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_SECURITY_SET_FLASH_POLICY}</command>" +
                        "<arg>" +
                        $"<source_file>MEM://0x{hostOffset:X}:0x{length:X}</source_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            return response != null;
        }

        /// <summary>
        /// 获取设备固件信息 (含 SLA 信息)
        /// </summary>
        /// <param name="ct">取消令牌</param>
        /// <returns>包含 rnd, hrid, socid 等信息的响应</returns>
        public async Task<DeviceFwInfo> GetDeviceFwInfoAsync(CancellationToken ct = default)
        {
            _log("[XML] 获取设备固件信息...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_SECURITY_GET_DEV_FW_INFO}</command>" +
                        "<arg>" +
                        "<target_file>MEM://0x8000000:0x100000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return null;

            var info = new DeviceFwInfo
            {
                Rnd = response.SelectSingleNode("//rnd")?.InnerText ?? "",
                HrId = response.SelectSingleNode("//hrid")?.InnerText ?? "",
                SocId = response.SelectSingleNode("//socid")?.InnerText ?? ""
            };

            _log($"[XML] 设备固件信息: SocId={info.SocId?.Substring(0, Math.Min(16, info.SocId?.Length ?? 0))}...");
            return info;
        }

        /// <summary>
        /// eMMC 控制
        /// </summary>
        /// <param name="function">功能: GET-RPMB-STATUS, LIFE-CYCLE-STATUS 等</param>
        /// <param name="ct">取消令牌</param>
        public async Task<string> EmmcControlAsync(string function = "GET-RPMB-STATUS", CancellationToken ct = default)
        {
            _logDetail($"[XML] eMMC 控制: {function}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_EMMC_CONTROL}</command>" +
                        "<arg>" +
                        $"<function>{function}</function>" +
                        "<target_file>MEM://0x8000000:0x200000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var resultNode = response.SelectSingleNode("//result") ?? 
                                response.SelectSingleNode("//status");
                return resultNode?.InnerText ?? "";
            }
            return null;
        }

        /// <summary>
        /// 读取 eFuse
        /// </summary>
        /// <param name="ct">取消令牌</param>
        /// <returns>eFuse XML 数据</returns>
        public async Task<string> ReadEfuseAsync(CancellationToken ct = default)
        {
            _log("[XML] 读取 eFuse...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_READ_EFUSE}</command>" +
                        "<arg>" +
                        "<target_file>ms-appdata:///local/efuse.xml</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                // 返回完整的 eFuse XML
                return response.OuterXml;
            }
            return null;
        }

        /// <summary>
        /// 写入 eFuse (危险操作!)
        /// </summary>
        /// <param name="efuseXml">eFuse XML 数据</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> WriteEfuseAsync(string efuseXml, CancellationToken ct = default)
        {
            _log("[XML] ⚠ 写入 eFuse (危险操作!)...");

            // 首先上传 eFuse 数据
            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_WRITE_EFUSE}</command>" +
                        "<arg>" +
                        "<source_file>ms-appdata:///local/efuse.xml</source_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null && statusNode.InnerText.ToUpper() == "OK")
                {
                    _log("[XML] ✓ eFuse 写入成功");
                    return true;
                }
            }
            
            _log("[XML] eFuse 写入失败");
            return false;
        }

        /// <summary>
        /// RAM 测试
        /// </summary>
        /// <param name="function">测试类型: FLIP 或 CALIBRATION</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">测试长度</param>
        /// <param name="repeat">重复次数</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> RamTestAsync(string function = "FLIP", long startAddress = 0x4000000, 
            long length = 0x100000, int repeat = 10, CancellationToken ct = default)
        {
            _log($"[XML] RAM 测试: {function}");

            string cmd;
            if (function == "FLIP")
            {
                cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                     "<da><version>1.0</version>" +
                     $"<command>{XmlDaCommands.CMD_RAM_TEST}</command>" +
                     "<arg>" +
                     "<function>FLIP</function>" +
                     $"<start_address>0x{startAddress:X}</start_address>" +
                     $"<length>0x{length:X}</length>" +
                     $"<repeat>0x{repeat:X}</repeat>" +
                     "</arg></da>";
            }
            else
            {
                cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                     "<da><version>1.0</version>" +
                     $"<command>{XmlDaCommands.CMD_RAM_TEST}</command>" +
                     "<arg>" +
                     "<function>CALIBRATION</function>" +
                     "<target_file>ms-appdata:///local/calib.bin</target_file>" +
                     "</arg></da>";
            }

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null)
                {
                    _log($"[XML] RAM 测试结果: {statusNode.InnerText}");
                    return statusNode.InnerText.ToUpper() == "OK" || 
                           statusNode.InnerText.ToUpper() == "PASS";
                }
            }
            return false;
        }

        /// <summary>
        /// DRAM 修复
        /// </summary>
        /// <param name="ct">取消令牌</param>
        /// <returns>修复结果: SUCCEEDED, NO-NEED, FAILED</returns>
        public async Task<string> DramRepairAsync(CancellationToken ct = default)
        {
            _log("[XML] DRAM 修复...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_DRAM_REPAIR}</command>" +
                        "<arg>" +
                        "<param_file>D:\\dram.info</param_file>" +
                        "<target_file>MEM://0x10000:0x1000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var resultNode = response.SelectSingleNode("//result") ?? 
                                response.SelectSingleNode("//status");
                string result = resultNode?.InnerText ?? "FAILED";
                _log($"[XML] DRAM 修复结果: {result}");
                return result;
            }
            return "FAILED";
        }

        /// <summary>
        /// 读取寄存器
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="bitWidth">位宽: 32 或 16</param>
        /// <param name="ct">取消令牌</param>
        public async Task<uint?> ReadRegisterAsync(long address, int bitWidth = 32, CancellationToken ct = default)
        {
            _logDetail($"[XML] 读取寄存器: 0x{address:X}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_READ_REGISTER}</command>" +
                        "<arg>" +
                        $"<bit_width>{bitWidth}</bit_width>" +
                        $"<base_address>0x{address:X}</base_address>" +
                        "<target_file>MEM://0x8000000:0x4</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var valueNode = response.SelectSingleNode("//value");
                if (valueNode != null)
                {
                    return (uint)ParseULong(valueNode.InnerText);
                }
            }
            return null;
        }

        /// <summary>
        /// 写入寄存器
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入值</param>
        /// <param name="bitWidth">位宽: 32 或 16</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> WriteRegisterAsync(long address, uint value, int bitWidth = 32, 
            CancellationToken ct = default)
        {
            _logDetail($"[XML] 写入寄存器: 0x{address:X} = 0x{value:X}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_WRITE_REGISTER}</command>" +
                        "<arg>" +
                        $"<bit_width>{bitWidth}</bit_width>" +
                        $"<base_address>0x{address:X}</base_address>" +
                        "<source_file>MEM://0x8000000:0x4</source_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            return response != null;
        }

        /// <summary>
        /// 擦除 Flash
        /// </summary>
        /// <param name="partition">分区: EMMC-USER, EMMC-BOOT1, EMMC-BOOT2 等</param>
        /// <param name="offset">偏移</param>
        /// <param name="length">长度</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> EraseFlashAsync(string partition = "EMMC-USER", ulong offset = 0, 
            ulong length = 0x100000, CancellationToken ct = default)
        {
            _log($"[XML] 擦除 Flash: {partition} @ 0x{offset:X}, 长度: 0x{length:X}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_ERASE_FLASH}</command>" +
                        "<arg>" +
                        $"<partition>{partition}</partition>" +
                        $"<offset>0x{offset:X}</offset>" +
                        $"<length>0x{length:X}</length>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null && statusNode.InnerText.ToUpper() == "OK")
                {
                    _log("[XML] ✓ Flash 擦除成功");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 读取 Flash
        /// </summary>
        /// <param name="partition">分区</param>
        /// <param name="offset">偏移</param>
        /// <param name="length">长度</param>
        /// <param name="ct">取消令牌</param>
        public async Task<byte[]> ReadFlashAsync(string partition = "EMMC-USER", ulong offset = 0, 
            ulong length = 0x100000, CancellationToken ct = default)
        {
            _log($"[XML] 读取 Flash: {partition} @ 0x{offset:X}, 长度: 0x{length:X}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_READ_FLASH}</command>" +
                        "<arg>" +
                        $"<partition>{partition}</partition>" +
                        $"<offset>0x{offset:X}</offset>" +
                        $"<length>0x{length:X}</length>" +
                        "<target_file>ROM_0</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null && statusNode.InnerText.ToUpper() == "READY")
                {
                    // 读取数据块
                    return await ReadDataChunkAsync(ct);
                }
            }
            return null;
        }

        /// <summary>
        /// 获取硬件信息 (详细)
        /// </summary>
        /// <param name="ct">取消令牌</param>
        public async Task<HardwareInfo> GetHardwareInfoAsync(CancellationToken ct = default)
        {
            _log("[XML] 获取硬件信息...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_GET_HW_INFO}</command>" +
                        "<arg>" +
                        "<target_file>MEM://0x8000000:0x200000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return null;

            var info = new HardwareInfo
            {
                RamSize = ParseULong(response.SelectSingleNode("//ram_size")?.InnerText),
                BatteryVoltage = (int)ParseULong(response.SelectSingleNode("//battery_voltage")?.InnerText),
                RandomId = response.SelectSingleNode("//random_id")?.InnerText ?? "",
                StorageType = response.SelectSingleNode("//storage")?.InnerText ?? "Unknown",
                ProductId = response.SelectSingleNode("//product_id")?.InnerText ?? ""
            };

            // 根据存储类型解析更多信息
            if (info.StorageType == "UFS")
            {
                info.BlockSize = (uint)ParseULong(response.SelectSingleNode("//block_size")?.InnerText);
                info.Lua0Size = ParseULong(response.SelectSingleNode("//lua0_size")?.InnerText);
                info.Lua1Size = ParseULong(response.SelectSingleNode("//lua1_size")?.InnerText);
                info.UfsVendorId = response.SelectSingleNode("//ufs_vendor_id")?.InnerText ?? "";
                info.UfsCid = response.SelectSingleNode("//ufs_cid")?.InnerText ?? "";
            }
            else if (info.StorageType == "EMMC")
            {
                info.BlockSize = (uint)ParseULong(response.SelectSingleNode("//block_size")?.InnerText);
                info.Boot1Size = ParseULong(response.SelectSingleNode("//boot1_size")?.InnerText);
                info.Boot2Size = ParseULong(response.SelectSingleNode("//boot2_size")?.InnerText);
                info.RpmbSize = ParseULong(response.SelectSingleNode("//rpmb_size")?.InnerText);
                info.UserSize = ParseULong(response.SelectSingleNode("//user_size")?.InnerText);
            }

            _log($"[XML] 存储类型: {info.StorageType}, RAM: {info.RamSize / 1024 / 1024}MB");
            return info;
        }

        /// <summary>
        /// 获取系统属性
        /// </summary>
        /// <param name="key">属性键: DA.SLA, DA.VERSION 等</param>
        /// <param name="ct">取消令牌</param>
        public async Task<string> GetSysPropertyAsync(string key = "DA.SLA", CancellationToken ct = default)
        {
            _logDetail($"[XML] 获取系统属性: {key}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_GET_SYS_PROPERTY}</command>" +
                        "<arg>" +
                        $"<key>{key}</key>" +
                        "<target_file>MEM://0x8000000:0x200000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var valueNode = response.SelectSingleNode("//value") ?? 
                               response.SelectSingleNode($"//{key.Replace(".", "_")}");
                return valueNode?.InnerText ?? "";
            }
            return null;
        }

        /// <summary>
        /// 获取 DA 信息
        /// </summary>
        /// <param name="ct">取消令牌</param>
        public async Task<DaInfo> GetDaInfoAsync(CancellationToken ct = default)
        {
            _log("[XML] 获取 DA 信息...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_GET_DA_INFO}</command>" +
                        "<arg>" +
                        "<target_file>MEM://0x2000000:0x20000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return null;

            var info = new DaInfo
            {
                Version = response.SelectSingleNode("//da_version")?.InnerText ?? "",
                Build = response.SelectSingleNode("//build")?.InnerText ?? ""
            };

            _log($"[XML] DA 版本: {info.Version}, 构建: {info.Build}");
            return info;
        }

        /// <summary>
        /// 设置 RSC (Regional Sales Code)
        /// </summary>
        /// <param name="key">RSC 键</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> SetRscAsync(string key, CancellationToken ct = default)
        {
            _log($"[XML] 设置 RSC: {key}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_SET_RSC}</command>" +
                        "<arg>" +
                        $"<key>{key}</key>" +
                        "<source_file>ms-appdata:///local/RSC.bin</source_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null && statusNode.InnerText.ToUpper() == "OK")
                {
                    _log("[XML] ✓ RSC 设置成功");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 写入私有证书
        /// </summary>
        /// <param name="certData">证书数据</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> WritePrivateCertAsync(byte[] certData, CancellationToken ct = default)
        {
            _log("[XML] 写入私有证书...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_WRITE_PRIVATE_CERT}</command>" +
                        "<arg>" +
                        "<source_file>ms-appdata:///local/cert.bin</source_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                var statusNode = response.SelectSingleNode("//status");
                if (statusNode != null && statusNode.InnerText.ToUpper() == "OK")
                {
                    _log("[XML] ✓ 私有证书写入成功");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取下载镜像反馈
        /// </summary>
        /// <param name="ct">取消令牌</param>
        public async Task<string> GetDownloadedImageFeedbackAsync(CancellationToken ct = default)
        {
            _logDetail("[XML] 获取下载镜像反馈...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_GET_DOWNLOADED_IMAGE_FEEDBACK}</command>" +
                        "<arg>" +
                        "<target_file>MEM://0x2000000:0x20000</target_file>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response != null)
            {
                return response.OuterXml;
            }
            return null;
        }

        /// <summary>
        /// 设置主机信息
        /// </summary>
        /// <param name="hostInfo">主机信息 (默认为当前时间戳)</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> SetHostInfoAsync(string hostInfo = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(hostInfo))
            {
                hostInfo = DateTime.Now.ToString("yyyyMMddTHHmmss");
            }

            _logDetail($"[XML] 设置主机信息: {hostInfo}");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                        "<da><version>1.0</version>" +
                        $"<command>{XmlDaCommands.CMD_SET_HOST_INFO}</command>" +
                        "<arg>" +
                        $"<info>{hostInfo}</info>" +
                        "</arg></da>";

            var response = await SendCommandAsync(cmd, ct);
            return response != null;
        }

        #endregion

        #region 信息查询

        /// <summary>
        /// 获取 Flash 信息
        /// </summary>
        public async Task<MtkFlashInfo> GetFlashInfoAsync(CancellationToken ct = default)
        {
            _log("[XML] 获取 Flash 信息...");

            string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                         "<da><version>1.0</version><command>CMD:GET_FLASH_INFO</command></da>";

            var response = await SendCommandAsync(cmd, ct);
            if (response == null)
                return null;

            var flashInfo = new MtkFlashInfo
            {
                FlashType = response.SelectSingleNode("//flash_type")?.InnerText ?? "Unknown",
                Capacity = ParseULong(response.SelectSingleNode("//capacity")?.InnerText),
                BlockSize = (uint)ParseULong(response.SelectSingleNode("//block_size")?.InnerText),
                PageSize = (uint)ParseULong(response.SelectSingleNode("//page_size")?.InnerText),
                Model = response.SelectSingleNode("//model")?.InnerText ?? ""
            };

            return flashInfo;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 读取数据块 (带线程安全)
        /// </summary>
        private async Task<byte[]> ReadDataChunkAsync(CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                byte[] header = await ReadBytesInternalAsync(12, DEFAULT_TIMEOUT_MS, ct);
                if (header == null)
                    return null;

                uint magic = MtkDataPacker.UnpackUInt32LE(header, 0);
                if (magic != XML_MAGIC)
                    return null;

                uint length = MtkDataPacker.UnpackUInt32LE(header, 8);
                if (length == 0 || length > MAX_BUFFER_SIZE)
                    return null;

                return await ReadBytesInternalAsync((int)length, DEFAULT_TIMEOUT_MS, ct);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 读取指定字节数 (内部方法，不加锁)
        /// </summary>
        private async Task<byte[]> ReadBytesInternalAsync(int count, int timeoutMs, CancellationToken ct = default)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            DateTime start = DateTime.Now;

            while (read < count)
            {
                if (ct.IsCancellationRequested)
                    return null;

                if ((DateTime.Now - start).TotalMilliseconds > timeoutMs)
                    return null;

                if (_port.BytesToRead > 0)
                {
                    int toRead = Math.Min(_port.BytesToRead, count - read);
                    int actualRead = _port.Read(buffer, read, toRead);
                    read += actualRead;
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }

            return buffer;
        }

        /// <summary>
        /// 读取指定字节数 (带线程安全)
        /// </summary>
        private async Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                return await ReadBytesInternalAsync(count, timeoutMs, ct);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 解析 ulong 字符串
        /// </summary>
        private ulong ParseULong(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToUInt64(value, 16);

            return ulong.TryParse(value, out ulong result) ? result : 0;
        }

        /// <summary>
        /// 写入 64 位无符号整数 (Little-Endian) - 避免平台相关性
        /// </summary>
        private static void WriteUInt64LE(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                IsConnected = false;
                
                // 只有当我们拥有锁时才释放它
                if (_ownsPortLock)
                {
                    _portLock?.Dispose();
                }
                
                _disposed = true;
            }
        }

        #endregion
    }
}
