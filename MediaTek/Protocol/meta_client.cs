// ============================================================================
// SakuraEDL - MediaTek META 模式通信客户端
// 基于 MTK META UTILITY 逆向分析
// ============================================================================
// META 模式功能:
// - 工程测试模式通信
// - 设备信息读取
// - NVRAM 操作 (需要 metacore.dll, 暂不实现)
// - 工厂测试指令
// ============================================================================

using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// META 模式状态
    /// </summary>
    public enum MetaState
    {
        /// <summary>未连接</summary>
        Disconnected,
        
        /// <summary>等待设备</summary>
        WaitingForDevice,
        
        /// <summary>握手中</summary>
        Handshaking,
        
        /// <summary>已连接 (META 模式)</summary>
        MetaConnected,
        
        /// <summary>已连接 (FACTORY 模式)</summary>
        FactoryConnected,
        
        /// <summary>错误</summary>
        Error
    }

    /// <summary>
    /// META 模式通信客户端
    /// </summary>
    public class MetaClient : IDisposable
    {
        private SerialPort _port;
        private readonly object _portLock = new object();
        private Action<string> _log;
        private MetaState _state = MetaState.Disconnected;

        // ═══════════════════════════════════════════════════════════════════
        // META 协议常量 (来自 MTK META UTILITY 逆向分析)
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>META 模式就绪标识</summary>
        private static readonly byte[] READY_SIGNAL = Encoding.ASCII.GetBytes("READY");
        
        /// <summary>META 模式握手命令</summary>
        private static readonly byte[] META_HANDSHAKE = Encoding.ASCII.GetBytes("METAMETA");
        
        /// <summary>FACTORY 模式握手命令</summary>
        private static readonly byte[] FACTORY_HANDSHAKE = Encoding.ASCII.GetBytes("FACTFACT");
        
        /// <summary>断开连接命令</summary>
        private static readonly byte[] DISCONNECT_CMD = Encoding.ASCII.GetBytes("DISCONNECT");
        
        // META 命令序列 (来自 sub_DFA640 逆向分析)
        private static readonly byte[] META_CMD_1 = new byte[] { 0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };
        private static readonly byte[] META_CMD_2 = new byte[] { 0x04, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0xC0 };
        private static readonly byte[] META_CMD_3 = new byte[] { 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0xC0, 0x00, 0x80, 0x00, 0x00 };

        // 串口参数 (来自 sub_DCF3E0 逆向分析)
        private const int DEFAULT_BAUD_RATE = 115200;
        private const int DATA_BITS = 8;
        private const Parity PARITY = Parity.None;
        private const StopBits STOP_BITS = StopBits.One;
        private const int BUFFER_SIZE = 81920;  // 0x14000

        /// <summary>
        /// 当前状态
        /// </summary>
        public MetaState State => _state;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _state == MetaState.MetaConnected || _state == MetaState.FactoryConnected;

        public MetaClient(Action<string> log = null)
        {
            _log = log ?? (s => { });
        }

        /// <summary>
        /// 连接到 META 模式设备
        /// </summary>
        public async Task<bool> ConnectAsync(string comPort, bool factoryMode = false, CancellationToken ct = default)
        {
            try
            {
                _log($"[META] 连接到 {comPort}...");
                _state = MetaState.WaitingForDevice;

                // 配置串口 (基于逆向分析的参数)
                _port = new SerialPort(comPort, DEFAULT_BAUD_RATE, PARITY, DATA_BITS, STOP_BITS)
                {
                    ReadTimeout = 5000,
                    WriteTimeout = 5000,
                    ReadBufferSize = BUFFER_SIZE,
                    WriteBufferSize = BUFFER_SIZE
                };

                _port.Open();
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();

                _state = MetaState.Handshaking;

                // 执行 META 握手序列
                bool handshakeOk = await PerformHandshakeAsync(factoryMode, ct);
                if (!handshakeOk)
                {
                    _log("[META] 握手失败");
                    _state = MetaState.Error;
                    return false;
                }

                _state = factoryMode ? MetaState.FactoryConnected : MetaState.MetaConnected;
                _log($"[META] ✓ 已连接 ({(factoryMode ? "FACTORY" : "META")} 模式)");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[META] 连接异常: {ex.Message}");
                _state = MetaState.Error;
                return false;
            }
        }

        /// <summary>
        /// 执行 META 握手序列
        /// </summary>
        private async Task<bool> PerformHandshakeAsync(bool factoryMode, CancellationToken ct)
        {
            // 基于 MTK META UTILITY sub_DFA640 逆向分析
            // 握手流程:
            // 1. 发送 "READY" 等待响应
            // 2. 发送 "METAMETA" 或 "FACTFACT"
            // 3. 发送配置命令序列
            // 4. 验证响应

            try
            {
                // Step 1: 发送 READY
                _log("[META] 发送 READY...");
                await WriteAsync(READY_SIGNAL, ct);
                await Task.Delay(100, ct);

                // Step 2: 发送模式握手
                byte[] handshake = factoryMode ? FACTORY_HANDSHAKE : META_HANDSHAKE;
                _log($"[META] 发送 {(factoryMode ? "FACTFACT" : "METAMETA")}...");
                await WriteAsync(handshake, ct);

                // 等待响应
                byte[] response = await ReadWithTimeoutAsync(100, 2000, ct);
                if (response == null || response.Length == 0)
                {
                    _log("[META] 未收到握手响应");
                    return false;
                }

                _log($"[META] 收到响应: {response.Length} 字节");

                // Step 3: 发送配置命令
                _log("[META] 发送配置命令...");
                await WriteAsync(META_CMD_1, ct);
                await Task.Delay(50, ct);
                
                await WriteAsync(META_CMD_2, ct);
                await Task.Delay(50, ct);
                
                await WriteAsync(META_CMD_3, ct);
                await Task.Delay(100, ct);

                // 清空缓冲区
                _port.DiscardInBuffer();

                return true;
            }
            catch (Exception ex)
            {
                _log($"[META] 握手异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            try
            {
                if (IsConnected)
                {
                    _log("[META] 发送断开命令...");
                    await WriteAsync(DISCONNECT_CMD, ct);
                    await Task.Delay(100, ct);
                }
            }
            catch { }
            finally
            {
                ClosePort();
                _state = MetaState.Disconnected;
            }
        }

        /// <summary>
        /// 发送原始命令
        /// </summary>
        public async Task<byte[]> SendCommandAsync(byte[] command, int expectedResponseLength, int timeoutMs = 5000, CancellationToken ct = default)
        {
            if (!IsConnected)
            {
                _log("[META] 未连接");
                return null;
            }

            try
            {
                lock (_portLock)
                {
                    _port.DiscardInBuffer();
                }

                await WriteAsync(command, ct);
                return await ReadWithTimeoutAsync(expectedResponseLength, timeoutMs, ct);
            }
            catch (Exception ex)
            {
                _log($"[META] 命令执行异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 读取设备基本信息 (不需要 DLL)
        /// </summary>
        public async Task<string> ReadBasicInfoAsync(CancellationToken ct = default)
        {
            if (!IsConnected)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine("=== META 模式设备信息 ===");
            sb.AppendLine($"端口: {_port.PortName}");
            sb.AppendLine($"模式: {(_state == MetaState.FactoryConnected ? "FACTORY" : "META")}");
            sb.AppendLine($"波特率: {_port.BaudRate}");

            return sb.ToString();
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        private async Task WriteAsync(byte[] data, CancellationToken ct)
        {
            if (_port == null || !_port.IsOpen)
                return;

            await Task.Run(() =>
            {
                lock (_portLock)
                {
                    _port.Write(data, 0, data.Length);
                }
            }, ct);
        }

        /// <summary>
        /// 读取数据 (带超时)
        /// </summary>
        private async Task<byte[]> ReadWithTimeoutAsync(int minLength, int timeoutMs, CancellationToken ct)
        {
            if (_port == null || !_port.IsOpen)
                return null;

            return await Task.Run(() =>
            {
                DateTime startTime = DateTime.Now;
                var buffer = new byte[4096];
                int totalRead = 0;

                while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    lock (_portLock)
                    {
                        if (_port.BytesToRead > 0)
                        {
                            int toRead = Math.Min(_port.BytesToRead, buffer.Length - totalRead);
                            int read = _port.Read(buffer, totalRead, toRead);
                            totalRead += read;

                            if (totalRead >= minLength)
                                break;
                        }
                    }

                    Thread.Sleep(10);
                }

                if (totalRead > 0)
                {
                    byte[] result = new byte[totalRead];
                    Array.Copy(buffer, result, totalRead);
                    return result;
                }

                return null;
            }, ct);
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        private void ClosePort()
        {
            try
            {
                lock (_portLock)
                {
                    if (_port != null && _port.IsOpen)
                    {
                        _port.DiscardInBuffer();
                        _port.DiscardOutBuffer();
                        _port.Close();
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            ClosePort();
            _port?.Dispose();
            _port = null;
            _state = MetaState.Disconnected;
        }
    }
}
