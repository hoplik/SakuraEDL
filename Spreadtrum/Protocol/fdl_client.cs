// ============================================================================
// SakuraEDL - 展讯 FDL 刷机客户端
// Spreadtrum/Unisoc FDL (Flash Download) Client - 纯 C# 实现
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SakuraEDL.Spreadtrum.Protocol
{
    /// <summary>
    /// FDL 刷机客户端 (纯 C# 实现，不依赖外部工具)
    /// </summary>
    public class FdlClient : IDisposable
    {
        private SerialPort _port;
        private readonly HdlcProtocol _hdlc;
        private FdlStage _stage = FdlStage.None;
        private SprdDeviceState _state = SprdDeviceState.Disconnected;
        private readonly SemaphoreSlim _portLock = new SemaphoreSlim(1, 1);  // 使用 SemaphoreSlim 替代 lock
        private CancellationTokenSource _cts;
        private volatile bool _isDisposed = false;  // 标记是否已释放
        
        // 端口信息 (用于重连)
        private string _portName;
        private int _baudRate = 115200;

        // 缓冲区
        private readonly byte[] _readBuffer = new byte[65536];
        private int _readBufferLength = 0;

        // 配置
        public int DefaultTimeout { get; set; } = 10000;    // 增加默认超时
        public int MaxOperationTimeout { get; set; } = 60000;  // 最大操作超时 (防止卡死)
        public int DataChunkSize { get; set; } = 528;       // BROM 模式块大小 (参考 sprdproto)
        public const int BROM_CHUNK_SIZE = 528;             // BROM 协议: 528 字节
        public const int FDL_CHUNK_SIZE = 2112;             // FDL 协议: 2112 字节
        public int HandshakeRetries { get; set; } = 50;
        public int CommandRetries { get; set; } = 3;        // 命令重试次数
        public int RetryDelayMs { get; set; } = 500;        // 重试间隔 (毫秒)

        // 事件
        public event Action<string> OnLog;
        public event Action<int, int> OnProgress;
        public event Action<SprdDeviceState> OnStateChanged;

        // 属性
        public bool IsConnected => _port != null && _port.IsOpen;
        public FdlStage CurrentStage => _stage;
        public SprdDeviceState State => _state;
        public string PortName => _port?.PortName;

        /// <summary>
        /// 获取当前串口 (用于漏洞利用)
        /// </summary>
        public SerialPort GetPort() => _port;

        // 芯片 ID (0 表示自动检测，用于确定 FDL 加载地址)
        public uint ChipId { get; private set; }

        public FdlClient()
        {
            _hdlc = new HdlcProtocol(msg => OnLog?.Invoke(msg));
        }

        // 自定义 FDL 配置
        public string CustomFdl1Path { get; private set; }
        public string CustomFdl2Path { get; private set; }
        public uint CustomFdl1Address { get; private set; }
        public uint CustomFdl2Address { get; private set; }
        
        // 自定义执行地址 (用于绕过签名验证)
        public uint CustomExecAddress { get; private set; }
        public bool UseExecNoVerify { get; set; } = true;  // 默认启用绕过验证

        /// <summary>
        /// 设置芯片 ID (影响 FDL 加载地址和 exec_addr)
        /// </summary>
        public void SetChipId(uint chipId)
        {
            ChipId = chipId;
            if (chipId > 0)
            {
                string platform = SprdPlatform.GetPlatformName(chipId);
                uint execAddr = SprdPlatform.GetExecAddress(chipId);
                
                // 自动设置 exec_addr
                if (CustomExecAddress == 0 && execAddr > 0)
                {
                    CustomExecAddress = execAddr;
                }
                
                Log("[FDL] 芯片配置: {0}", platform);
                Log("[FDL]   FDL1: 0x{0:X8}, FDL2: 0x{1:X8}", 
                    SprdPlatform.GetFdl1Address(chipId), SprdPlatform.GetFdl2Address(chipId));
                
                if (execAddr > 0)
                {
                    Log("[FDL]   exec_addr: 0x{0:X8} (需要签名绕过)", execAddr);
                }
                else
                {
                    Log("[FDL]   不需要签名绕过");
                }
            }
            else
            {
                Log("[FDL] 芯片设置为自动检测");
            }
        }

        /// <summary>
        /// 设置自定义 FDL1
        /// </summary>
        public void SetCustomFdl1(string filePath, uint address)
        {
            CustomFdl1Path = filePath;
            CustomFdl1Address = address;
        }

        /// <summary>
        /// 设置自定义执行地址 (用于绕过签名验证)
        /// </summary>
        public void SetCustomExecAddress(uint execAddr)
        {
            CustomExecAddress = execAddr;
            if (execAddr > 0)
            {
                Log("[FDL] 设置 exec_addr: 0x{0:X8}", execAddr);
            }
        }
        
        // 自定义 exec_no_verify 文件路径
        public string CustomExecNoVerifyPath { get; set; }
        
        /// <summary>
        /// 设置 exec_no_verify 文件路径
        /// </summary>
        public void SetExecNoVerifyFile(string filePath)
        {
            CustomExecNoVerifyPath = filePath;
            if (!string.IsNullOrEmpty(filePath))
            {
                Log("[FDL] 设置 exec_no_verify 文件: {0}", System.IO.Path.GetFileName(filePath));
            }
        }
        
        /// <summary>
        /// 查找 custom_exec_no_verify 文件
        /// 搜索顺序: 指定路径 > FDL1同目录 > 程序目录
        /// </summary>
        private byte[] LoadExecNoVerifyPayload(uint execAddr)
        {
            string execFileName = string.Format("custom_exec_no_verify_{0:x}.bin", execAddr);
            
            // 1. 使用指定的文件
            if (!string.IsNullOrEmpty(CustomExecNoVerifyPath) && System.IO.File.Exists(CustomExecNoVerifyPath))
            {
                Log("[FDL] 使用指定的 exec_no_verify: {0}", System.IO.Path.GetFileName(CustomExecNoVerifyPath));
                return System.IO.File.ReadAllBytes(CustomExecNoVerifyPath);
            }
            
            // 2. 在 FDL1 同目录查找 (spd_dump 格式)
            if (!string.IsNullOrEmpty(CustomFdl1Path))
            {
                string fdl1Dir = System.IO.Path.GetDirectoryName(CustomFdl1Path);
                string execPath = System.IO.Path.Combine(fdl1Dir, execFileName);
                
                if (System.IO.File.Exists(execPath))
                {
                    Log("[FDL] 找到 exec_no_verify: {0} (FDL目录)", execFileName);
                    return System.IO.File.ReadAllBytes(execPath);
                }
                
                // 也查找简化名称
                execPath = System.IO.Path.Combine(fdl1Dir, "exec_no_verify.bin");
                if (System.IO.File.Exists(execPath))
                {
                    Log("[FDL] 找到 exec_no_verify.bin (FDL目录)");
                    return System.IO.File.ReadAllBytes(execPath);
                }
            }
            
            // 3. 在程序目录查找
            string appDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string appExecPath = System.IO.Path.Combine(appDir, execFileName);
            
            if (System.IO.File.Exists(appExecPath))
            {
                Log("[FDL] 找到 exec_no_verify: {0} (程序目录)", execFileName);
                return System.IO.File.ReadAllBytes(appExecPath);
            }
            
            // 也查找简化名称
            appExecPath = System.IO.Path.Combine(appDir, "exec_no_verify.bin");
            if (System.IO.File.Exists(appExecPath))
            {
                Log("[FDL] 找到 exec_no_verify.bin (程序目录)");
                return System.IO.File.ReadAllBytes(appExecPath);
            }
            
            // 4. 没有找到外部文件
            Log("[FDL] 未找到 exec_no_verify 文件，跳过签名绕过");
            Log("[FDL] 提示: 需要 {0}", execFileName);
            return null;
        }
        
        /// <summary>
        /// 发送 custom_exec_no_verify payload
        /// 参考 spd_dump: 在 fdl1 发送后、EXEC 前发送
        /// </summary>
        private async Task<bool> SendExecNoVerifyPayloadAsync(uint execAddr)
        {
            if (execAddr == 0) return true;  // 无需发送
            
            // 加载 payload
            byte[] payload = LoadExecNoVerifyPayload(execAddr);
            
            if (payload == null || payload.Length == 0)
            {
                // 没有找到 exec_no_verify 文件，跳过
                return true;
            }
            
            Log("[FDL] 发送 custom_exec_no_verify 到 0x{0:X8} ({1} bytes)...", execAddr, payload.Length);
            
                                                                                               // 注意: spd_dump 直接连续发送第二个 FDL，不需要重新 CONNECT
            // 确保使用 BROM 模式 (CRC16)
            _hdlc.SetBromMode();
            
            // 发送 START_DATA
            var startPayload = new byte[8];
            startPayload[0] = (byte)((execAddr >> 24) & 0xFF);
            startPayload[1] = (byte)((execAddr >> 16) & 0xFF);
            startPayload[2] = (byte)((execAddr >> 8) & 0xFF);
            startPayload[3] = (byte)(execAddr & 0xFF);
            startPayload[4] = (byte)((payload.Length >> 24) & 0xFF);
            startPayload[5] = (byte)((payload.Length >> 16) & 0xFF);
            startPayload[6] = (byte)((payload.Length >> 8) & 0xFF);
            startPayload[7] = (byte)(payload.Length & 0xFF);
            
            Log("[FDL] exec_no_verify START_DATA: addr=0x{0:X8}, size={1}", execAddr, payload.Length);
            var startFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_START_DATA, startPayload);
            await WriteFrameAsync(startFrame);
            
            if (!await WaitAckAsync(5000))
            {
                Log("[FDL] exec_no_verify START_DATA 失败");
                return false;
            }
            Log("[FDL] exec_no_verify START_DATA OK");
            
            // 发送数据 - exec_no_verify payload 通常很小，直接发送
            var midstFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_MIDST_DATA, payload);
            await WriteFrameAsync(midstFrame);
            
            if (!await WaitAckAsync(5000))
            {
                Log("[FDL] exec_no_verify MIDST_DATA 失败");
                return false;
            }
            Log("[FDL] exec_no_verify MIDST_DATA OK");
            
            // 发送 END_DATA
            var endFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_END_DATA);
            await WriteFrameAsync(endFrame);
            
            // 读取 END_DATA 响应并记录详细信息
            var endResp = await ReadFrameAsyncSafe(5000);
            if (endResp != null && endResp.Length > 0)
            {
                Log("[FDL] exec_no_verify END_DATA 响应: {0}", BitConverter.ToString(endResp).Replace("-", " "));
                try
                {
                    var parsed = _hdlc.ParseFrame(endResp);
                    if (parsed.Type == (byte)BslCommand.BSL_REP_ACK)
                    {
                        Log("[FDL] exec_no_verify END_DATA OK");
                    }
                    else
                    {
                        string errorMsg = GetBslErrorMessage(parsed.Type);
                        Log("[FDL] exec_no_verify END_DATA 错误: 0x{0:X2} ({1})", parsed.Type, errorMsg);
                        Log("[FDL] 警告: END_DATA 失败，但继续尝试 EXEC...");
                    }
                }
                catch (Exception ex)
                {
                    Log("[FDL] exec_no_verify END_DATA 解析失败: {0}", ex.Message);
                }
            }
            else
            {
                Log("[FDL] exec_no_verify END_DATA 无响应");
                Log("[FDL] 警告: END_DATA 无响应，但继续尝试 EXEC...");
            }
            
            Log("[FDL] exec_no_verify payload 发送成功");
            return true;
        }

        /// <summary>
        /// 设置自定义 FDL2
        /// </summary>
        public void SetCustomFdl2(string filePath, uint address)
        {
            CustomFdl2Path = filePath;
            CustomFdl2Address = address;
        }

        /// <summary>
        /// 清除自定义 FDL 配置
        /// </summary>
        public void ClearCustomFdl()
        {
            CustomFdl1Path = null;
            CustomFdl2Path = null;
            CustomFdl1Address = 0;
            CustomFdl2Address = 0;
        }

        /// <summary>
        /// 获取 FDL1 加载地址 (优先使用自定义地址)
        /// </summary>
        public uint GetFdl1Address()
        {
            if (CustomFdl1Address > 0)
                return CustomFdl1Address;
            return SprdPlatform.GetFdl1Address(ChipId);
        }

        /// <summary>
        /// 获取 FDL2 加载地址 (优先使用自定义地址)
        /// </summary>
        public uint GetFdl2Address()
        {
            if (CustomFdl2Address > 0)
                return CustomFdl2Address;
            return SprdPlatform.GetFdl2Address(ChipId);
        }

        /// <summary>
        /// 获取 FDL1 文件路径 (优先使用自定义路径)
        /// </summary>
        public string GetFdl1Path(string defaultPath)
        {
            if (!string.IsNullOrEmpty(CustomFdl1Path) && File.Exists(CustomFdl1Path))
                return CustomFdl1Path;
            return defaultPath;
        }

        /// <summary>
        /// 获取 FDL2 文件路径 (优先使用自定义路径)
        /// </summary>
        public string GetFdl2Path(string defaultPath)
        {
            if (!string.IsNullOrEmpty(CustomFdl2Path) && File.Exists(CustomFdl2Path))
                return CustomFdl2Path;
            return defaultPath;
        }

        #region 连接管理

        /// <summary>
        /// 连接设备
        /// </summary>
        public async Task<bool> ConnectAsync(string portName, int baudRate = 115200)
        {
            try
            {
                Log("[FDL] 连接端口: {0}, 波特率: {1}", portName, baudRate);

                // 保存端口信息用于重连
                _portName = portName;
                _baudRate = baudRate;

                _port = new SerialPort(portName)
                {
                    BaudRate = baudRate,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                    ReadTimeout = DefaultTimeout,
                    WriteTimeout = DefaultTimeout,
                    ReadBufferSize = 65536,
                    WriteBufferSize = 65536
                };

                _port.Open();
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();

                // 握手
                bool success = await HandshakeAsync();
                if (success)
                {
                    SetState(SprdDeviceState.Connected);
                    Log("[FDL] 连接成功");
                }

                return success;
            }
            catch (Exception ex)
            {
                Log("[FDL] 连接失败: {0}", ex.Message);
                SetState(SprdDeviceState.Error);
                return false;
            }
        }
        
        /// <summary>
        /// 安全关闭端口
        /// </summary>
        private void ClosePortSafe()
        {
            try
            {
                if (_port != null)
                {
                    if (_port.IsOpen)
                    {
                        try
                        {
                            _port.DiscardInBuffer();
                            _port.DiscardOutBuffer();
                        }
                        catch { }
                        _port.Close();
                    }
                    _port.Dispose();
                    _port = null;
                }
            }
            catch { }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                
                if (_port != null && _port.IsOpen)
                {
                    _port.Close();
                }
                _port?.Dispose();
                _port = null;
                
                _stage = FdlStage.None;
                SetState(SprdDeviceState.Disconnected);
                
                Log("[FDL] 已断开连接");
            }
            catch (Exception ex)
            {
                Log("[FDL] 断开连接异常: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 握手 (参考 sprdproto 实现)
        /// </summary>
        private async Task<bool> HandshakeAsync()
        {
            Log("[FDL] 开始握手...");

            // 确保使用 CRC16 模式 (BROM 阶段)
            _hdlc.SetBromMode();

            // 方法1: 发送单个 0x7E (参考 sprdproto)
            // sprdproto 只发送一个 0x7E 然后等待 BSL_REP_VER
            await WriteFrameAsyncSafe(new byte[] { 0x7E }, 1000);
            await Task.Delay(100);
            
            // 检查是否有响应数据
            var response = await ReadFrameAsync(2000);
            if (response != null)
            {
                Log("[FDL] 收到原始数据 ({0} bytes): {1}", response.Length, BitConverter.ToString(response).Replace("-", " "));
                
                try
                {
                    var frame = _hdlc.ParseFrame(response);
                    
                    if (frame.Type == (byte)BslCommand.BSL_REP_VER)
                    {
                        // BROM 返回版本信息，提取版本字符串
                        string version = frame.Payload != null 
                            ? System.Text.Encoding.ASCII.GetString(frame.Payload).TrimEnd('\0')
                            : "Unknown";
                        Log("[FDL] BROM 版本: {0}", version);
                        _isBromMode = true;
                        _bromVersion = version;
                        SetState(SprdDeviceState.Connected);
                        return true;
                    }
                    else if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                    {
                        Log("[FDL] 握手成功 (FDL 模式)");
                        _isBromMode = false;
                        return true;
                    }
                    else
                    {
                        Log("[FDL] 未知响应类型: 0x{0:X2}", frame.Type);
                    }
                }
                catch (Exception ex)
                {
                    Log("[FDL] 解析响应失败: {0}", ex.Message);
                }
            }
            
            // 方法2: 如果单个 0x7E 没有响应，尝试发送多个
            Log("[FDL] 尝试多字节同步...");
            try { _port?.DiscardInBuffer(); } 
            catch (Exception ex) { LogDebug("[FDL] 清空缓冲区异常: {0}", ex.Message); }
            
            for (int i = 0; i < 3; i++)
            {
                await WriteFrameAsyncSafe(new byte[] { 0x7E }, 500);
                await Task.Delay(50);
            }
            await Task.Delay(100);
            
            response = await ReadFrameAsync(2000);
            if (response != null)
            {
                Log("[FDL] 收到响应 ({0} bytes): {1}", response.Length, BitConverter.ToString(response).Replace("-", " "));
                
                try
                {
                    var frame = _hdlc.ParseFrame(response);
                    if (frame.Type == (byte)BslCommand.BSL_REP_VER)
                    {
                        string version = frame.Payload != null 
                            ? System.Text.Encoding.ASCII.GetString(frame.Payload).TrimEnd('\0')
                            : "Unknown";
                        Log("[FDL] BROM 版本: {0}", version);
                        _isBromMode = true;
                        _bromVersion = version;
                        SetState(SprdDeviceState.Connected);
                        return true;
                    }
                    else if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                    {
                        Log("[FDL] 握手成功 (FDL 模式)");
                        _isBromMode = false;
                        return true;
                    }
                }
                catch (Exception ex) 
                { 
                    LogDebug("[FDL] 解析多字节响应异常: {0}", ex.Message); 
                }
            }
            
            // 方法3: 尝试发送 CONNECT 命令
            Log("[FDL] 尝试发送 CONNECT 命令...");
            _port.DiscardInBuffer();

            var connectFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_CONNECT);
            Log("[FDL] CONNECT frame: {0}", BitConverter.ToString(connectFrame).Replace("-", " "));
            await WriteFrameAsync(connectFrame);

            response = await ReadFrameAsync(3000);
            if (response != null)
            {
                Log("[FDL] CONNECT 响应 ({0} bytes): {1}", response.Length, BitConverter.ToString(response).Replace("-", " "));
                
                try
                {
                    var frame = _hdlc.ParseFrame(response);
                    if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                    {
                        // 重要: 如果是初始连接(未下载FDL)，ACK 也应该被当作 BROM 模式
                        // 因为 BROM 也可能对 CONNECT 返回 ACK
                        if (_stage == FdlStage.None)
                        {
                            Log("[FDL] CONNECT ACK (初始连接，假定 BROM 模式)");
                            _isBromMode = true;
                            SetState(SprdDeviceState.Connected);
                        }
                        else
                        {
                            Log("[FDL] CONNECT ACK (FDL 模式)");
                        _isBromMode = false;
                        }
                        return true;
                    }
                    else if (frame.Type == (byte)BslCommand.BSL_REP_VER)
                    {
                        string version = frame.Payload != null 
                            ? System.Text.Encoding.ASCII.GetString(frame.Payload).TrimEnd('\0')
                            : "Unknown";
                        Log("[FDL] BROM 版本: {0}", version);
                        _isBromMode = true;
                        _bromVersion = version;
                        SetState(SprdDeviceState.Connected);
                        return true;
                    }
                    Log("[FDL] CONNECT 响应类型: 0x{0:X2}", frame.Type);
                }
                catch (Exception ex)
                {
                    Log("[FDL] 解析 CONNECT 响应失败: {0}", ex.Message);
                    // 有响应就认为连接成功
                    Log("[FDL] 假定为 BROM 模式");
                    _isBromMode = true;
                    SetState(SprdDeviceState.Connected);
                    return true;
                }
            }

            Log("[FDL] 握手失败 - 无响应");
            return false;
        }
        
        private string _bromVersion = "";
        
        /// <summary>
        /// 是否处于 BROM 模式（需要下载 FDL）
        /// </summary>
        public bool IsBromMode => _isBromMode;
        private bool _isBromMode = true;

        #endregion

        #region FDL 下载

        /// <summary>
        /// 下载 FDL
        /// </summary>
        public async Task<bool> DownloadFdlAsync(byte[] fdlData, uint baseAddr, FdlStage stage)
        {
            if (!IsConnected)
            {
                Log("[FDL] 设备未连接");
                return false;
            }

            Log("[FDL] 下载 {0}, 地址: 0x{1:X8}, 大小: {2} bytes", stage, baseAddr, fdlData.Length);

            try
            {
                // 根据阶段设置正确的块大小
                if (stage == FdlStage.FDL1)
                {
                    DataChunkSize = BROM_CHUNK_SIZE;
                    _hdlc.SetBromMode();
                    Log("[FDL] BROM 模式: CRC16, 块大小={0}", DataChunkSize);
                }
                
                // 0. BROM 模式下需要先发送 CONNECT 命令建立通信
                if (_isBromMode || stage == FdlStage.FDL1)
                {
                    Log("[FDL] 发送 CONNECT 命令...");
                    _port.DiscardInBuffer();
                    
                    var connectFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_CONNECT);
                    await WriteFrameAsync(connectFrame);
                    
                    var connectResp = await ReadFrameAsync(3000);
                    if (connectResp != null)
                    {
                        try
                        {
                            var frame = _hdlc.ParseFrame(connectResp);
                            if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                            {
                                Log("[FDL] CONNECT ACK 收到");
                            }
                            else if (frame.Type == (byte)BslCommand.BSL_REP_VER)
                            {
                                Log("[FDL] BROM 返回版本响应，继续...");
                                // 再次发送 CONNECT
                                await WriteFrameAsync(connectFrame);
                                if (!await WaitAckAsync(3000))
                                {
                                    Log("[FDL] 第二次 CONNECT 无 ACK，尝试继续...");
                                }
                            }
                            else
                            {
                                Log("[FDL] CONNECT 响应: 0x{0:X2}", frame.Type);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("[FDL] 解析 CONNECT 响应失败: {0}", ex.Message);
                        }
                    }
                    else
                    {
                        Log("[FDL] CONNECT 无响应，尝试继续...");
                    }
                }

                // 1. START_DATA - 发送地址和大小 (Big-Endian 格式，与 BROM 协议一致)
                var startPayload = new byte[8];
                // 使用 Big-Endian 格式写入地址和大小
                WriteBigEndian32(startPayload, 0, baseAddr);
                WriteBigEndian32(startPayload, 4, (uint)fdlData.Length);

                Log("[FDL] 发送 START_DATA: 地址=0x{0:X8}, 大小={1}", baseAddr, fdlData.Length);
                Log("[FDL] START_DATA payload (BE): {0}", BitConverter.ToString(startPayload).Replace("-", " "));
                var startFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_START_DATA, startPayload);
                Log("[FDL] START_DATA frame: {0}", BitConverter.ToString(startFrame).Replace("-", " "));
                await WriteFrameAsync(startFrame);

                if (!await WaitAckWithDetailAsync(5000, "START_DATA"))
                {
                    // 重试一次
                    Log("[FDL] START_DATA 无响应，重试...");
                    _port.DiscardInBuffer();
                    await Task.Delay(100);
                    await WriteFrameAsync(startFrame);
                    
                    if (!await WaitAckWithDetailAsync(5000, "START_DATA (重试)"))
                    {
                        Log("[FDL] START_DATA 失败");
                        Log("[FDL] 提示: 0x8B 校验错误通常表示 FDL 文件与芯片不匹配或地址错误");
                        return false;
                    }
                }
                Log("[FDL] START_DATA OK");

                // 2. MIDST_DATA - 分块发送数据
                int totalChunks = (fdlData.Length + DataChunkSize - 1) / DataChunkSize;
                Log("[FDL] 开始传输数据: {0} 块, 每块 {1} 字节", totalChunks, DataChunkSize);
                
                for (int i = 0; i < totalChunks; i++)
                {
                    int offset = i * DataChunkSize;
                    int length = Math.Min(DataChunkSize, fdlData.Length - offset);

                    var chunk = new byte[length];
                    Array.Copy(fdlData, offset, chunk, 0, length);

                    var midstFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_MIDST_DATA, chunk);
                    await WriteFrameAsync(midstFrame);

                    // 等待 ACK，带重试
                    bool ackReceived = false;
                    for (int retry = 0; retry < 3 && !ackReceived; retry++)
                    {
                        if (retry > 0)
                        {
                            Log("[FDL] 重试块 {0}...", i + 1);
                            await Task.Delay(100);
                            await WriteFrameAsync(midstFrame);
                        }
                        ackReceived = await WaitAckAsync(10000);  // 10秒超时
                    }
                    
                    if (!ackReceived)
                    {
                        Log("[FDL] MIDST_DATA 块 {0}/{1} 失败", i + 1, totalChunks);
                        return false;
                    }

                    OnProgress?.Invoke(i + 1, totalChunks);
                    
                    // 每 10 块输出一次进度
                    if ((i + 1) % 10 == 0 || i + 1 == totalChunks)
                    {
                        Log("[FDL] 进度: {0}/{1} 块 ({2}%)", i + 1, totalChunks, (i + 1) * 100 / totalChunks);
                    }
                }
                Log("[FDL] MIDST_DATA OK ({0} 块)", totalChunks);

                // 3. END_DATA
                var endFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_END_DATA);
                await WriteFrameAsync(endFrame);

                var endResp = await ReadFrameAsync(10000);
                if (endResp != null)
                {
                    try
                    {
                        var endParsed = _hdlc.ParseFrame(endResp);
                        if (endParsed.Type == (byte)BslCommand.BSL_REP_ACK)
                        {
                            Log("[FDL] END_DATA OK");
                        }
                        else
                        {
                            string errorMsg = GetBslErrorMessage(endParsed.Type);
                            Log("[FDL] END_DATA 错误: 0x{0:X2} ({1})", endParsed.Type, errorMsg);
                            Log("[FDL] 提示: 可能 FDL 文件与芯片不匹配，请使用设备专用 FDL");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("[FDL] END_DATA 解析异常: {0}", ex.Message);
                        return false;
                    }
                }
                else
                {
                    Log("[FDL] END_DATA 无响应");
                    return false;
                }

                // 3.5. 发送 custom_exec_no_verify payload (仅 FDL1，参考 spd_dump)
                if (stage == FdlStage.FDL1 && UseExecNoVerify && CustomExecAddress > 0)
                {
                    Log("[FDL] 发送签名验证绕过 payload...");
                    if (!await SendExecNoVerifyPayloadAsync(CustomExecAddress))
                    {
                        Log("[FDL] 警告: exec_no_verify 发送失败，继续尝试执行...");
                        // 不直接返回失败，尝试继续
                    }
                }

                // 4. EXEC_DATA - 执行 FDL (参考 spd_dump.c)
                Log("[FDL] 发送 EXEC_DATA...");
                var execFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_EXEC_DATA);
                await WriteFrameAsync(execFrame);

                // 等待 EXEC_DATA 响应
                var execResp = await ReadFrameAsyncSafe(5000);
                if (execResp != null)
                {
                    try
                    {
                        var execParsed = _hdlc.ParseFrame(execResp);
                        if (execParsed.Type == (byte)BslCommand.BSL_REP_ACK)
                        {
                            Log("[FDL] EXEC_DATA ACK 收到");
                        }
                        else if (execParsed.Type == (byte)BslCommand.BSL_REP_INCOMPATIBLE_PARTITION)
                        {
                            // FDL2 执行成功，但返回分区不兼容 (参考 spd_dump.c 第 1282-1283 行)
                            Log("[FDL] FDL2: 分区不兼容警告 (正常)");
                            if (stage == FdlStage.FDL2)
                            {
                                // 禁用转码 (FDL2 必需步骤)
                                await DisableTranscodeAsync();
                                
                                _stage = stage;
                                SetState(SprdDeviceState.Fdl2Loaded);
                                Log("[FDL] FDL2 下载并执行成功");
                                return true;
                            }
                        }
                        else
                        {
                            Log("[FDL] EXEC_DATA 响应: 0x{0:X2}", execParsed.Type);
                        }
                    }
                    catch { }
                }
                else
                {
                    Log("[FDL] EXEC_DATA 无响应");
                }

                // FDL1 执行后切换到 FDL 模式 (参考 SPRDClientCore)
                if (stage == FdlStage.FDL1)
                {
                    // FDL1 执行后需要等待设备初始化
                    // 参考 spd_dump: CHECK_BAUD 第一次会失败，这是正常的
                    Log("[FDL] 等待 FDL1 初始化...");
                    
                    string portName = _port?.PortName ?? _portName;
                    
                    // 检查端口状态
                    bool portValid = _port != null && _port.IsOpen;
                    Log("[FDL] 当前端口状态: {0}, 端口名: {1}", portValid ? "打开" : "关闭/无效", portName);
                    
                    // 等待设备稳定 (EXEC 后设备可能重置 USB)
                    await Task.Delay(1000);
                    
                    // 如果端口已关闭，尝试重新打开
                    if (_port != null && !_port.IsOpen)
                    {
                        Log("[FDL] 端口已关闭，尝试重新打开: {0}", portName);
                        try
                        {
                            _port.Open();
                            Log("[FDL] 端口重新打开成功");
                            await Task.Delay(200);
                        }
                        catch (Exception ex)
                        {
                            Log("[FDL] 端口重新打开失败: {0}", ex.Message);
                            // 尝试创建新端口
                            try
                            {
                                _port = new SerialPort(portName, _baudRate);
                                _port.ReadTimeout = 3000;
                                _port.WriteTimeout = 3000;
                                _port.Open();
                                Log("[FDL] 创建新端口成功");
                            }
                            catch (Exception ex2)
                            {
                                Log("[FDL] 创建新端口失败: {0}", ex2.Message);
                            }
                        }
                    }
                    
                    // 清空缓冲区
                    if (_port != null && _port.IsOpen)
                    {
                        try
                        {
                            _port.DiscardInBuffer();
                            _port.DiscardOutBuffer();
                            Log("[FDL] 端口缓冲区已清空");
                        }
                        catch (Exception ex)
                        {
                            Log("[FDL] 清空缓冲区失败: {0}", ex.Message);
                        }
                    }
                    
                    // 切换到 FDL 模式: 使用 checksum (关闭 CRC16)
                    _hdlc.SetFdlMode();
                    DataChunkSize = FDL_CHUNK_SIZE;
                    _isBromMode = false;
                    Log("[FDL] 切换到 FDL 模式: Checksum, 块大小={0}", DataChunkSize);
                    
                    // 参考 spd_dump: 发送 CHECK_BAUD 并重试
                    // CHECK_BAUD 第一次失败是正常的 (设备还在初始化)
                    Log("[FDL] 发送 CHECK_BAUD 等待 FDL1 响应...");
                    byte[] checkBaud = new byte[] { 0x7E, 0x7E, 0x7E, 0x7E };
                    byte[] lastSentPacket = checkBaud;
                    bool reconnected = false;
                    
                    for (int i = 0; i < 20; i++)
                    {
                        try
                        {
                            if (i > 0)
                            {
                                Log("[FDL] 重试 {0}/20...", i + 1);
                            }
                            
                            // 第3次尝试时，尝试重连端口
                            if (i == 3 && !reconnected && !string.IsNullOrEmpty(portName))
                            {
                                Log("[FDL] 尝试重新连接端口: {0}", portName);
                                try
                                {
                                    ClosePortSafe();
                                    await Task.Delay(500);
                                    
                                    _port = new SerialPort(portName, _baudRate);
                                    _port.ReadTimeout = 3000;
                                    _port.WriteTimeout = 3000;
                                    _port.Open();
                                    _port.DiscardInBuffer();
                                    _port.DiscardOutBuffer();
                                    
                                    Log("[FDL] 端口重连成功");
                                    reconnected = true;
                                }
                                catch (Exception ex)
                                {
                                    Log("[FDL] 端口重连失败: {0}", ex.Message);
                                }
                            }
                            
                            // 第8次尝试时，尝试切换波特率
                            if (i == 8 && _port != null && _port.IsOpen)
                            {
                                Log("[FDL] 尝试切换波特率到 921600...");
                                try
                                {
                                    _port.Close();
                                    _port.BaudRate = 921600;
                                    _port.Open();
                                    _port.DiscardInBuffer();
                                }
                                catch { }
                            }
                            
                            // 第13次尝试时，尝试切换回原波特率并用 CRC16 模式
                            if (i == 13 && _port != null && _port.IsOpen)
                            {
                                Log("[FDL] 尝试切换回 115200 并使用 CRC16...");
                                try
                                {
                                    _port.Close();
                                    _port.BaudRate = 115200;
                                    _port.Open();
                                    _port.DiscardInBuffer();
                                    _hdlc.SetBromMode();  // 切回 CRC16
                                }
                                catch { }
                            }
                            
                            // 发送数据 (参考 SPRDClientCore 的重发机制)
                            if (!SafeWriteToPort(lastSentPacket))
                            {
                                Log("[FDL] 串口写入失败");
                                await Task.Delay(300);
                                continue;
                            }
                            
                            // 读取响应 (使用 BytesToRead 轮询方式)
                            byte[] response = await SafeReadFromPortAsync(2000);
                            
                            if (response != null && response.Length > 0)
                            {
                                Log("[FDL] 收到响应 ({0} bytes): {1}", response.Length, 
                                    BitConverter.ToString(response).Replace("-", " "));
                                
                                try
                                {
                                    var parsed = _hdlc.ParseFrame(response);
                                    
                                    if (parsed.Type == (byte)BslCommand.BSL_REP_VER)
                                    {
                                        string version = parsed.Payload != null 
                                            ? System.Text.Encoding.ASCII.GetString(parsed.Payload).TrimEnd('\0')
                                            : "Unknown";
                                        Log("[FDL] FDL1 版本: {0}", version);
                                        
                                        // 发送 CONNECT 命令
                                        var connectFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_CONNECT);
                                        lastSentPacket = connectFrame;
                                        SafeWriteToPort(connectFrame);
                                        
                                        response = await SafeReadFromPortAsync(2000);
                                        if (response != null && response.Length > 0)
                                        {
                                            try
                                            {
                                                parsed = _hdlc.ParseFrame(response);
                                                if (parsed.Type == (byte)BslCommand.BSL_REP_ACK)
                                                {
                                                    Log("[FDL] CONNECT ACK 收到");
                                                }
                                            }
                                            catch { }
                                        }
                                        
                    _stage = stage;
                                        SetState(SprdDeviceState.Fdl1Loaded);
                                        Log("[FDL] FDL1 下载并执行成功");
                    return true;
                                    }
                                    else if (parsed.Type == (byte)BslCommand.BSL_REP_ACK)
                                    {
                                        Log("[FDL] 收到 ACK，FDL1 已加载");
                                        _stage = stage;
                                        SetState(SprdDeviceState.Fdl1Loaded);
                                        return true;
                                    }
                                    else if (parsed.Type == (byte)BslCommand.BSL_REP_VERIFY_ERROR)
                                    {
                                        // 校验失败，可能需要切换校验模式
                                        Log("[FDL] 校验错误，尝试切换校验模式");
                                        _hdlc.ToggleChecksumMode();
                                        continue;
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    Log("[FDL] 解析响应失败: {0}", parseEx.Message);
                                }
                            }
                            
                            // 没有响应，等待后重发
                            await Task.Delay(150);
                        }
                        catch (Exception ex)
                        {
                            Log("[FDL] 异常: {0}", ex.Message);
                            await Task.Delay(200);
                        }
                    }
                    
                    Log("[FDL] FDL1 执行验证失败 (20次尝试)");
                    Log("[FDL] 提示: FDL1 文件可能与芯片不兼容，或 FDL1 地址错误");
                return false;
                }
                else
                {
                    // FDL2 执行后的验证
                    await Task.Delay(500);
                    
                    // FDL2 可能直接返回响应
                    var fdl2Resp = await ReadFrameAsyncSafe(2000);
                    if (fdl2Resp != null)
                    {
                        try
                        {
                            var parsed = _hdlc.ParseFrame(fdl2Resp);
                            if (parsed.Type == (byte)BslCommand.BSL_REP_ACK ||
                                parsed.Type == (byte)BslCommand.BSL_REP_INCOMPATIBLE_PARTITION)
                            {
                                if (parsed.Type == (byte)BslCommand.BSL_REP_INCOMPATIBLE_PARTITION)
                                {
                                    Log("[FDL] FDL2: 分区不兼容警告 (正常)");
                                }
                                
                                // 发送 DISABLE_TRANSCODE 禁用转码 (参考 spd_dump.c 和 SPRDClientCore)
                                // 这是 FDL2 必需的步骤，否则后续命令可能会失败
                                await DisableTranscodeAsync();
                                
                                _stage = stage;
                                SetState(SprdDeviceState.Fdl2Loaded);
                                Log("[FDL] FDL2 下载并执行成功");
                                return true;
                            }
                        }
                        catch { }
                    }
                    
                    Log("[FDL] FDL2 执行验证失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log("[FDL] 下载异常: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 从文件下载 FDL
        /// </summary>
        public async Task<bool> DownloadFdlFromFileAsync(string filePath, uint baseAddr, FdlStage stage)
        {
            if (!File.Exists(filePath))
            {
                Log("[FDL] 文件不存在: {0}", filePath);
                return false;
            }

            byte[] data = File.ReadAllBytes(filePath);
            return await DownloadFdlAsync(data, baseAddr, stage);
        }

        #endregion

        #region 分区操作

        /// <summary>
        /// 写入分区 (带重试机制，参考 SPRDClientCore)
        /// </summary>
        public async Task<bool> WritePartitionAsync(string partitionName, byte[] data, CancellationToken cancellationToken = default)
        {
            if (_stage != FdlStage.FDL2)
            {
                Log("[FDL] 需要先加载 FDL2");
                return false;
            }

            Log("[FDL] 写入分区: {0}, 大小: {1}", partitionName, FormatSize((uint)data.Length));

            try
            {
                // 判断是否需要 64 位模式
                ulong size = (ulong)data.Length;
                bool useMode64 = (size >> 32) != 0;
                
                // 构建 START_DATA payload (参考 SPRDClientCore)
                // 格式: [分区名 72字节 Unicode] + [大小 4字节 LE] + [大小高32位 4字节 LE, 仅64位]
                int payloadSize = useMode64 ? 80 : 76;
                var startPayload = new byte[payloadSize];
                
                // 分区名: Unicode 编码
                var nameBytes = Encoding.Unicode.GetBytes(partitionName);
                Array.Copy(nameBytes, 0, startPayload, 0, Math.Min(nameBytes.Length, 72));
                
                // 大小: Little Endian
                BitConverter.GetBytes((uint)(size & 0xFFFFFFFF)).CopyTo(startPayload, 72);
                if (useMode64)
                {
                    BitConverter.GetBytes((uint)(size >> 32)).CopyTo(startPayload, 76);
                }

                // START_DATA 带重试
                if (!await SendCommandWithRetryAsync((byte)BslCommand.BSL_CMD_START_DATA, startPayload))
                {
                    Log("[FDL] 分区 {0} START 失败", partitionName);
                    return false;
                }

                // 分块写入
                int totalChunks = (data.Length + DataChunkSize - 1) / DataChunkSize;
                int failedChunks = 0;
                const int maxConsecutiveFailures = 3;

                for (int i = 0; i < totalChunks; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log("[FDL] 写入取消");
                        return false;
                    }

                    int offset = i * DataChunkSize;
                    int length = Math.Min(DataChunkSize, data.Length - offset);

                    var chunk = new byte[length];
                    Array.Copy(data, offset, chunk, 0, length);

                    // 数据块带重试
                    if (!await SendDataWithRetryAsync((byte)BslCommand.BSL_CMD_MIDST_DATA, chunk))
                    {
                        failedChunks++;
                        Log("[FDL] 分区 {0} 块 {1}/{2} 写入失败 (累计失败: {3})", 
                            partitionName, i + 1, totalChunks, failedChunks);
                        
                        if (failedChunks >= maxConsecutiveFailures)
                        {
                            Log("[FDL] 连续失败次数过多，终止写入");
                            return false;
                        }
                        
                        // 尝试跳过此块继续 (某些设备可能支持)
                        continue;
                    }
                    else
                    {
                        failedChunks = 0;  // 重置连续失败计数
                    }

                    OnProgress?.Invoke(i + 1, totalChunks);
                }

                // END_DATA 带重试
                if (!await SendCommandWithRetryAsync((byte)BslCommand.BSL_CMD_END_DATA, null))
                {
                    Log("[FDL] 分区 {0} END 失败", partitionName);
                    return false;
                }

                Log("[FDL] 分区 {0} 写入成功", partitionName);
                return true;
            }
            catch (Exception ex)
            {
                Log("[FDL] 写入分区异常: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 读取分区 (参考 spd_dump 和 SPRDClientCore)
        /// </summary>
        public async Task<byte[]> ReadPartitionAsync(string partitionName, uint size, CancellationToken cancellationToken = default)
        {
            if (_stage != FdlStage.FDL2)
            {
                Log("[FDL] 需要先加载 FDL2");
                return null;
            }

            Log("[FDL] 读取分区: {0}, 大小: {1}", partitionName, FormatSize(size));

            try
            {
                // 判断是否需要 64 位模式
                bool useMode64 = (size >> 32) != 0;
                
                // 构建 READ_START payload (参考 SPRDClientCore)
                // 格式: [分区名 72字节 Unicode] + [大小 4字节 LE] + [大小高32位 4字节 LE, 仅64位]
                int payloadSize = useMode64 ? 80 : 76;
                var payload = new byte[payloadSize];
                
                // 分区名: Unicode 编码, 最多 36 个字符 (72 字节)
                var nameBytes = Encoding.Unicode.GetBytes(partitionName);
                Array.Copy(nameBytes, 0, payload, 0, Math.Min(nameBytes.Length, 72));
                
                // 大小: Little Endian
                BitConverter.GetBytes((uint)(size & 0xFFFFFFFF)).CopyTo(payload, 72);
                if (useMode64)
                {
                    BitConverter.GetBytes((uint)(size >> 32)).CopyTo(payload, 76);
                }

                Log("[FDL] READ_START payload: 分区={0}, 大小=0x{1:X}, 模式={2}", 
                    partitionName, size, useMode64 ? "64位" : "32位");

                // 启动读取 (带重试)
                if (!await SendCommandWithRetryAsync((byte)BslCommand.BSL_CMD_READ_START, payload))
                {
                    Log("[FDL] 读取 {0} 启动失败", partitionName);
                    return null;
                }

                // 接收数据
                using (var ms = new MemoryStream())
                {
                    ulong offset = 0;
                    int consecutiveErrors = 0;
                    const int maxConsecutiveErrors = 5;
                    uint readChunkSize = (uint)DataChunkSize;  // 每次读取的块大小

                    while (offset < size)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Log("[FDL] 读取已取消");
                            break;
                        }

                        // 计算本次读取大小
                        uint nowReadSize = (uint)Math.Min(readChunkSize, size - offset);
                        
                        // 构建 READ_MIDST payload (参考 spd_dump)
                        // 格式: [读取大小 4字节 LE] + [偏移量 4字节 LE] + [偏移量高32位 4字节 LE, 仅64位]
                        int midstPayloadSize = useMode64 ? 12 : 8;
                        var midstPayload = new byte[midstPayloadSize];
                        BitConverter.GetBytes(nowReadSize).CopyTo(midstPayload, 0);
                        BitConverter.GetBytes((uint)(offset & 0xFFFFFFFF)).CopyTo(midstPayload, 4);
                        if (useMode64)
                        {
                            BitConverter.GetBytes((uint)(offset >> 32)).CopyTo(midstPayload, 8);
                        }

                        // 带重试的数据块读取
                        byte[] chunkData = null;
                        for (int retry = 0; retry <= CommandRetries; retry++)
                        {
                            if (retry > 0)
                            {
                                Log("[FDL] 重试读取 offset=0x{0:X} ({1}/{2})", offset, retry, CommandRetries);
                                await Task.Delay(RetryDelayMs / 2);
                                try { _port?.DiscardInBuffer(); } catch { }
                            }

                            var midstFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_MIDST, midstPayload);
                            if (!await WriteFrameAsyncSafe(midstFrame))
                            {
                                continue;  // 写入失败，重试
                            }

                            var response = await ReadFrameAsyncSafe(15000);  // 读取数据可能需要较长时间
                            if (response != null)
                            {
                                HdlcFrame frame;
                                HdlcParseError parseError;
                                if (_hdlc.TryParseFrame(response, out frame, out parseError))
                                {
                                    // 响应类型: BSL_REP_READ_FLASH (0xBD)
                                    if (frame.Type == (byte)BslCommand.BSL_REP_READ_FLASH && frame.Payload != null)
                                    {
                                        chunkData = frame.Payload;
                                        break;  // 成功
                                    }
                                    else if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                                    {
                                        // 有些 FDL 返回 ACK 表示读取结束
                                        Log("[FDL] 收到 ACK，可能已读取完毕");
                                        break;
                                    }
                                    else
                                    {
                                        Log("[FDL] 意外响应: 0x{0:X2}", frame.Type);
                                    }
                                }
                                else if (parseError == HdlcParseError.CrcMismatch)
                                {
                                    Log("[FDL] CRC 错误，重试...");
                                    continue;
                                }
                            }
                        }

                        if (chunkData == null)
                        {
                            consecutiveErrors++;
                            Log("[FDL] 读取 offset=0x{0:X} 失败 (连续错误: {1})", offset, consecutiveErrors);
                            
                            if (consecutiveErrors >= maxConsecutiveErrors)
                            {
                                Log("[FDL] 连续错误过多，终止读取");
                                break;
                            }
                            // 尝试继续下一个块
                            offset += nowReadSize;
                            continue;
                        }
                        
                        consecutiveErrors = 0;  // 重置错误计数
                        ms.Write(chunkData, 0, chunkData.Length);
                        offset += (uint)chunkData.Length;

                        // 进度回调
                        OnProgress?.Invoke((int)offset, (int)size);
                        
                        // 每 10% 输出日志
                        int progressPercent = (int)(offset * 100 / size);
                        if (progressPercent % 10 == 0 && progressPercent > 0)
                        {
                            Log("[FDL] 读取进度: {0}% ({1}/{2})", progressPercent, FormatSize((uint)offset), FormatSize(size));
                        }
                    }

                    // 结束读取
                    var endFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_END);
                    await WriteFrameAsyncSafe(endFrame);
                    await WaitAckAsyncSafe(3000);

                    Log("[FDL] 分区 {0} 读取完成, 实际大小: {1}", partitionName, FormatSize((uint)ms.Length));
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取分区异常: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 擦除分区 (参考 SPRDClientCore)
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName)
        {
            if (_stage != FdlStage.FDL2)
            {
                Log("[FDL] 需要先加载 FDL2");
                return false;
            }

            Log("[FDL] 擦除分区: {0}", partitionName);

            // 擦除命令 payload: [分区名 72字节 Unicode]
            var payload = new byte[72];
            var nameBytes = Encoding.Unicode.GetBytes(partitionName);
            Array.Copy(nameBytes, 0, payload, 0, Math.Min(nameBytes.Length, 72));

            if (!await SendCommandWithRetryAsync((byte)BslCommand.BSL_CMD_ERASE_FLASH, payload, 60000))  // 擦除可能需要很长时间
            {
                Log("[FDL] 分区 {0} 擦除失败", partitionName);
                return false;
            }

                Log("[FDL] 分区 {0} 擦除成功", partitionName);
                return true;
            }

        #endregion

        #region FDL2 初始化

        /// <summary>
        /// 禁用转码 (FDL2 必需步骤)
        /// 参考: spd_dump.c disable_transcode 命令, SPRDClientCore
        /// 转码会将 0x7D 和 0x7E 字节前面加上 0x7D 进行转义
        /// FDL2 执行后必须禁用转码，否则后续命令可能失败
        /// </summary>
        public async Task<bool> DisableTranscodeAsync()
        {
            Log("[FDL] 禁用转码...");
            
            try
            {
                var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_DISABLE_TRANSCODE);
                await WriteFrameAsync(frame);
                
                var response = await ReadFrameAsyncSafe(3000);
                if (response != null)
                {
                    try
                    {
                        var parsed = _hdlc.ParseFrame(response);
                        if (parsed.Type == (byte)BslCommand.BSL_REP_ACK)
                        {
                            _hdlc.DisableTranscode();
                            Log("[FDL] 转码已禁用");
                            return true;
                        }
                        else if (parsed.Type == (byte)BslCommand.BSL_REP_UNSUPPORTED_COMMAND)
                        {
                            // 某些 FDL 不支持此命令，这是正常的
                            Log("[FDL] FDL 不支持 DISABLE_TRANSCODE (正常)");
                            return true;
                        }
                        else
                        {
                            Log("[FDL] DISABLE_TRANSCODE 响应: 0x{0:X2}", parsed.Type);
                        }
                    }
                    catch { }
                }
                
                // 即使没有响应，也尝试禁用转码
                _hdlc.DisableTranscode();
                Log("[FDL] 转码状态已设置为禁用 (无响应)");
                return true;
            }
            catch (Exception ex)
            {
                Log("[FDL] 禁用转码异常: {0}", ex.Message);
                // 继续尝试
                _hdlc.DisableTranscode();
                return true;
            }
        }

        #endregion

        #region 设备信息

        /// <summary>
        /// 读取版本信息
        /// </summary>
        public async Task<string> ReadVersionAsync()
        {
            Log("[FDL] 读取版本信息...");

            try
            {
            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_VERSION);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(5000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Type == (byte)BslCommand.BSL_REP_VER && parsed.Payload != null)
                    {
                        string version = Encoding.UTF8.GetString(parsed.Payload).TrimEnd('\0');
                        Log("[FDL] 版本: {0}", version);
                        return version;
                    }
                }
                catch { }
            }

            Log("[FDL] 读取版本失败");
            return null;
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取版本异常: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 读取芯片类型
        /// </summary>
        public async Task<uint> ReadChipTypeAsync()
        {
            Log("[FDL] 读取芯片类型...");

            try
            {
            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_CHIP_TYPE);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(5000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Payload != null && parsed.Payload.Length >= 4)
                    {
                        uint chipId = BitConverter.ToUInt32(parsed.Payload, 0);
                        Log("[FDL] 芯片类型: 0x{0:X8} ({1})", chipId, SprdPlatform.GetPlatformName(chipId));
                        return chipId;
                    }
                }
                catch { }
            }

            Log("[FDL] 读取芯片类型失败");
            return 0;
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取芯片类型异常: {0}", ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// 读取分区表
        /// </summary>
        /// <summary>
        /// 常见分区名列表 (参考 SPRDClientCore)
        /// 当 READ_PARTITION 命令不支持时，使用此列表遍历检测
        /// </summary>
        private static readonly string[] CommonPartitions = {
            "splloader", "prodnv", "miscdata", "recovery", "misc", "trustos", "trustos_bak",
            "sml", "sml_bak", "uboot", "uboot_bak", "logo", "fbootlogo",
            "l_fixnv1", "l_fixnv2", "l_runtimenv1", "l_runtimenv2",
            "gpsgl", "gpsbd", "wcnmodem", "persist", "l_modem",
            "l_deltanv", "l_gdsp", "l_ldsp", "pm_sys", "boot",
            "system", "cache", "vendor", "uboot_log", "userdata", "dtb", "socko", "vbmeta",
            "super", "metadata", "user_partition"
        };

        public async Task<List<SprdPartitionInfo>> ReadPartitionTableAsync()
        {
            Log("[FDL] 读取分区表...");

            try
            {
                // 方法一: 使用 READ_PARTITION 命令
            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_PARTITION);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(10000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Type == (byte)BslCommand.BSL_REP_PARTITION && parsed.Payload != null)
                    {
                            Log("[FDL] READ_PARTITION 成功");
                        return ParsePartitionTable(parsed.Payload);
                    }
                        else if (parsed.Type == (byte)BslCommand.BSL_REP_UNSUPPORTED_COMMAND)
                        {
                            // FDL2 不支持 READ_PARTITION 命令，使用备选方法
                            Log("[FDL] READ_PARTITION 不支持，使用遍历方法...");
                            return await ReadPartitionTableByTraverseAsync();
                        }
                        else
                        {
                            Log("[FDL] 分区表响应类型: 0x{0:X2}", parsed.Type);
                    }
                }
                catch (Exception ex)
                {
                    Log("[FDL] 解析分区表失败: {0}", ex.Message);
                }
            }

                // 方法二: 遍历常见分区名
                Log("[FDL] 尝试遍历方法...");
                return await ReadPartitionTableByTraverseAsync();
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取分区表异常: {0}", ex.Message);
            return null;
            }
        }

        /// <summary>
        /// 通过遍历常见分区名获取分区表 (参考 SPRDClientCore TraverseCommonPartitions)
        /// </summary>
        private async Task<List<SprdPartitionInfo>> ReadPartitionTableByTraverseAsync()
        {
            var partitions = new List<SprdPartitionInfo>();
            Log("[FDL] 遍历检测常见分区...");

            // 全局超时保护 (最多 30 秒)
            using (var globalCts = new CancellationTokenSource(30000))
            {
                return await ReadPartitionTableByTraverseInternalAsync(partitions, globalCts.Token);
            }
        }

        /// <summary>
        /// 分区遍历内部实现 (带取消支持)
        /// </summary>
        private async Task<List<SprdPartitionInfo>> ReadPartitionTableByTraverseInternalAsync(
            List<SprdPartitionInfo> partitions, CancellationToken cancellationToken)
        {
            int failCount = 0;
            int maxConsecutiveFails = 5;  // 连续失败5次则认为不支持
            
            // 优先检测的常见分区（按出现概率排序）
            string[] priorityPartitions = { "boot", "system", "userdata", "cache", "recovery", "misc" };
            
            // 先检测优先分区，确认设备是否支持此命令
            foreach (var partName in priorityPartitions)
            {
                // 检查取消令牌
                if (cancellationToken.IsCancellationRequested)
                {
                    Log("[FDL] 分区遍历被取消 (全局超时)");
                    break;
                }

                try
                {
                    Log("[FDL] 检测分区: {0}...", partName);
                    var result = await CheckPartitionExistWithTimeoutAsync(partName, 3000);
                    if (result == true)
                    {
                        partitions.Add(new SprdPartitionInfo { Name = partName, Offset = 0, Size = 0 });
                        Log("[FDL] + 发现分区: {0}", partName);
                        failCount = 0;
                    }
                    else if (result == false)
                    {
                        Log("[FDL] - 分区不存在: {0}", partName);
                        failCount = 0;  // 明确返回 false 不算失败
                    }
                    else  // result == null
                    {
                        // 超时或通信错误
                        failCount++;
                        Log("[FDL] ? 分区检测超时: {0} (失败 {1}/{2})", partName, failCount, maxConsecutiveFails);
                        if (failCount >= maxConsecutiveFails)
                        {
                            Log("[FDL] 分区遍历不支持 (连续超时)");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    Log("[FDL] 分区检测异常: {0} - {1}", partName, ex.Message);
                    if (failCount >= maxConsecutiveFails)
                    {
                        Log("[FDL] 分区遍历异常中止");
                        break;
                    }
                }
            }

            // 如果连续失败太多次，不再继续
            if (failCount >= maxConsecutiveFails || cancellationToken.IsCancellationRequested)
            {
                if (partitions.Count > 0)
                {
                    Log("[FDL] 部分遍历完成，发现 {0} 个分区", partitions.Count);
                    return partitions;
                }
                Log("[FDL] 设备不支持分区遍历命令");
                return null;
            }

            // 检测剩余分区
            foreach (var partName in CommonPartitions)
            {
                // 检查取消令牌
                if (cancellationToken.IsCancellationRequested)
                {
                    Log("[FDL] 分区遍历被取消");
                    break;
                }

                if (priorityPartitions.Contains(partName))
                    continue;  // 跳过已检测的
                    
                try
                {
                    var result = await CheckPartitionExistWithTimeoutAsync(partName, 1500);
                    if (result == true)
                    {
                        partitions.Add(new SprdPartitionInfo { Name = partName, Offset = 0, Size = 0 });
                        Log("[FDL] 发现分区: {0}", partName);
                    }
                }
                catch
                {
                    // 忽略单个分区检测错误
                }
            }

            if (partitions.Count > 0)
            {
                Log("[FDL] 遍历完成，发现 {0} 个分区", partitions.Count);
                return partitions;
            }

            Log("[FDL] 未发现任何分区");
            return null;
        }
        
        /// <summary>
        /// 带超时的分区存在检测
        /// </summary>
        /// <returns>true=存在, false=不存在, null=超时/错误</returns>
        private async Task<bool?> CheckPartitionExistWithTimeoutAsync(string partitionName, int timeoutMs)
        {
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
                {
                    var task = CheckPartitionExistAsync(partitionName);
                    var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token));
                    
                    if (completedTask == task)
                    {
                        cts.Cancel();
                        return await task;
                    }
                    
                    return null;  // 超时
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查分区是否存在 (参考 SPRDClientCore CheckPartitionExist)
        /// 完整流程: READ_START -> READ_MIDST -> READ_END
        /// </summary>
        public async Task<bool> CheckPartitionExistAsync(string partitionName)
        {
            try
            {
                // 1. 构建 READ_START 请求: 分区名(Unicode, 72字节) + 大小(4字节)
                var payload = new byte[76];
                var nameBytes = Encoding.Unicode.GetBytes(partitionName);
                Array.Copy(nameBytes, 0, payload, 0, Math.Min(nameBytes.Length, 72));
                // 大小设为 8 字节，只是测试是否存在
                BitConverter.GetBytes((uint)8).CopyTo(payload, 72);

                var startFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_START, payload);
                if (!await WriteFrameAsyncSafe(startFrame))
                    return false;

                var startResponse = await ReadFrameAsyncSafe(2000);
                if (startResponse == null)
                    return false;
                
                var startParsed = _hdlc.ParseFrame(startResponse);
                
                if (startParsed.Type == (byte)BslCommand.BSL_REP_ACK)
                {
                    // 2. READ_START 成功，发送 READ_MIDST 读取 8 字节验证
                    // 格式: [读取大小 4字节 LE] + [偏移量 4字节 LE]
                    var midstPayload = new byte[8];
                    BitConverter.GetBytes((uint)8).CopyTo(midstPayload, 0);  // 读取大小 = 8
                    BitConverter.GetBytes((uint)0).CopyTo(midstPayload, 4);  // 偏移量 = 0
                    
                    var midstFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_MIDST, midstPayload);
                    if (!await WriteFrameAsyncSafe(midstFrame))
                    {
                        // 发送 READ_END 清理
                        await SendReadEndAsync();
                        return false;
                    }
                    
                    var midstResponse = await ReadFrameAsyncSafe(2000);
                    
                    // 3. 发送 READ_END 结束
                    await SendReadEndAsync();
                    
                    if (midstResponse != null)
                    {
                        var midstParsed = _hdlc.ParseFrame(midstResponse);
                        // 如果返回 READ_FLASH 数据，说明分区存在
                        return midstParsed.Type == (byte)BslCommand.BSL_REP_READ_FLASH;
                    }
                }
                else
                {
                    // READ_START 失败，也要发送 READ_END
                    await SendReadEndAsync();
                }

                return false;
            }
            catch
            {
                // 异常时尝试发送 READ_END 清理状态
                try { await SendReadEndAsync(); } catch { }
                return false;
            }
        }
        
        /// <summary>
        /// 发送 READ_END 命令
        /// </summary>
        private async Task SendReadEndAsync()
        {
            try
            {
                var endFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_END);
                await WriteFrameAsyncSafe(endFrame);
                await ReadFrameAsyncSafe(500);
            }
            catch { }
        }

        /// <summary>
        /// 解析分区表数据 (参考 SPRDClientCore)
        /// 格式: [分区名 72字节 Unicode] + [分区大小 4字节 LE] = 76 字节每条
        /// </summary>
        private List<SprdPartitionInfo> ParsePartitionTable(byte[] data)
        {
            var partitions = new List<SprdPartitionInfo>();
            
            if (data == null || data.Length == 0)
            {
                Log("[FDL] 分区表数据为空");
                return partitions;
            }
            
            // 分区表格式 (参考 SPRDClientCore):
            // Name: 72 字节 (Unicode, 36 个字符)
            // Size: 4 字节 (Little Endian)
            // 每条记录 76 字节
            const int NameSize = 72;  // 36 chars * 2 bytes (Unicode)
            const int SizeFieldSize = 4;
            const int EntrySize = NameSize + SizeFieldSize;  // 76 bytes
            
            int count = data.Length / EntrySize;
            Log("[FDL] 分区表数据大小: {0} 字节, 预计 {1} 个分区", data.Length, count);

            for (int i = 0; i < count; i++)
            {
                int offset = i * EntrySize;
                
                // 分区名: Unicode 编码
                string name = Encoding.Unicode.GetString(data, offset, NameSize).TrimEnd('\0');
                if (string.IsNullOrEmpty(name))
                    continue;

                // 分区大小: Little Endian
                uint size = BitConverter.ToUInt32(data, offset + NameSize);

                var partition = new SprdPartitionInfo
                {
                    Name = name,
                    Offset = 0,  // READ_PARTITION 响应不包含偏移量
                    Size = size
                };

                partitions.Add(partition);
                Log("[FDL] 分区: {0}, 大小: {1}", partition.Name, FormatSize(partition.Size));
            }

            Log("[FDL] 解析完成, 共 {0} 个分区", partitions.Count);
            return partitions;
        }

        #endregion

        #region 安全功能

        /// <summary>
        /// 解锁/锁定设备
        /// </summary>
        /// <param name="unlockData">解锁数据</param>
        /// <param name="relock">true=重新锁定, false=解锁</param>
        public async Task<bool> UnlockAsync(byte[] unlockData = null, bool relock = false)
        {
            string action = relock ? "锁定" : "解锁";
            Log($"[FDL] {action}设备...");

            try
            {
            // 构建 payload: [1字节操作类型] + [解锁数据]
            byte[] payload;
            if (unlockData != null && unlockData.Length > 0)
            {
                payload = new byte[1 + unlockData.Length];
                payload[0] = relock ? (byte)0x00 : (byte)0x01;  // 0=lock, 1=unlock
                Array.Copy(unlockData, 0, payload, 1, unlockData.Length);
            }
            else
            {
                payload = new byte[] { relock ? (byte)0x00 : (byte)0x01 };
            }

            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_UNLOCK, payload);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(10000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Type == (byte)BslCommand.BSL_REP_ACK)
                    {
                        Log($"[FDL] {action}成功");
                        return true;
                    }
                    Log("[FDL] {0}响应: 0x{1:X2}", action, parsed.Type);
                }
                catch { }
            }

            Log($"[FDL] {action}失败");
            return false;
            }
            catch (Exception ex)
            {
                Log("[FDL] {0}异常: {1}", action, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 读取公钥
        /// </summary>
        public async Task<byte[]> ReadPublicKeyAsync()
        {
            Log("[FDL] 读取公钥...");

            try
            {
            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_PUBKEY);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(5000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Payload != null && parsed.Payload.Length > 0)
                    {
                        Log("[FDL] 公钥长度: {0} bytes", parsed.Payload.Length);
                        return parsed.Payload;
                    }
                }
                catch { }
            }

            Log("[FDL] 读取公钥失败");
            return null;
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取公钥异常: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 发送签名
        /// </summary>
        public async Task<bool> SendSignatureAsync(byte[] signature)
        {
            if (signature == null || signature.Length == 0)
            {
                Log("[FDL] 签名数据为空");
                return false;
            }

            Log("[FDL] 发送签名, 长度: {0} bytes", signature.Length);

            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_SEND_SIGNATURE, signature);
            await WriteFrameAsync(frame);

            if (await WaitAckAsync(10000))
            {
                Log("[FDL] 签名验证成功");
                return true;
            }

            Log("[FDL] 签名验证失败");
            return false;
        }

        /// <summary>
        /// 读取 eFuse
        /// </summary>
        public async Task<byte[]> ReadEfuseAsync(uint blockId = 0)
        {
            Log("[FDL] 读取 eFuse, Block: {0}", blockId);

            try
            {
            var payload = BitConverter.GetBytes(blockId);
            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_EFUSE, payload);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(5000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Payload != null && parsed.Payload.Length > 0)
                    {
                        Log("[FDL] eFuse 数据: {0} bytes", parsed.Payload.Length);
                        return parsed.Payload;
                    }
                }
                catch { }
            }

            Log("[FDL] 读取 eFuse 失败");
            return null;
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取 eFuse 异常: {0}", ex.Message);
                return null;
            }
        }

        #endregion

        #region NV 操作

        /// <summary>
        /// 读取 NV 项
        /// </summary>
        public async Task<byte[]> ReadNvItemAsync(ushort itemId)
        {
            Log("[FDL] 读取 NV 项: {0}", itemId);

            try
            {
            var payload = BitConverter.GetBytes(itemId);
            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_NVITEM, payload);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(5000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Type == (byte)BslCommand.BSL_REP_DATA && parsed.Payload != null)
                    {
                        Log("[FDL] NV 项 {0} 数据: {1} bytes", itemId, parsed.Payload.Length);
                        return parsed.Payload;
                    }
                }
                catch { }
            }

            Log("[FDL] 读取 NV 项 {0} 失败", itemId);
            return null;
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取 NV 项异常: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 写入 NV 项
        /// </summary>
        public async Task<bool> WriteNvItemAsync(ushort itemId, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Log("[FDL] NV 数据为空");
                return false;
            }

            Log("[FDL] 写入 NV 项: {0}, 长度: {1} bytes", itemId, data.Length);

            // Payload: ItemId(2) + Data(N)
            var payload = new byte[2 + data.Length];
            BitConverter.GetBytes(itemId).CopyTo(payload, 0);
            Array.Copy(data, 0, payload, 2, data.Length);

            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_WRITE_NVITEM, payload);
            await WriteFrameAsync(frame);

            if (await WaitAckAsync(5000))
            {
                Log("[FDL] NV 项 {0} 写入成功", itemId);
                return true;
            }

            Log("[FDL] NV 项 {0} 写入失败", itemId);
            return false;
        }

        /// <summary>
        /// 读取 IMEI (NV 项 0)
        /// </summary>
        public async Task<string> ReadImeiAsync()
        {
            var data = await ReadNvItemAsync(0);
            if (data != null && data.Length >= 8)
            {
                // IMEI 格式转换
                var imei = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    imei.AppendFormat("{0:X2}", data[i]);
                }
                string result = imei.ToString().TrimStart('0').Substring(0, 15);
                Log("[FDL] IMEI: {0}", result);
                return result;
            }
            return null;
        }

        #endregion

        #region Flash 信息

        /// <summary>
        /// 读取 Flash 信息
        /// </summary>
        public async Task<SprdFlashInfo> ReadFlashInfoAsync()
        {
            Log("[FDL] 读取 Flash 信息...");

            try
            {
            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_READ_FLASH_INFO);
            await WriteFrameAsync(frame);

                var response = await ReadFrameAsyncSafe(5000);
            if (response != null)
            {
                try
                {
                    var parsed = _hdlc.ParseFrame(response);
                    if (parsed.Type == (byte)BslCommand.BSL_REP_FLASH_INFO && parsed.Payload != null)
                    {
                        return ParseFlashInfo(parsed.Payload);
                    }
                }
                catch { }
            }

            Log("[FDL] 读取 Flash 信息失败");
            return null;
            }
            catch (Exception ex)
            {
                Log("[FDL] 读取 Flash 信息异常: {0}", ex.Message);
                return null;
            }
        }

        private SprdFlashInfo ParseFlashInfo(byte[] data)
        {
            if (data.Length < 16)
                return null;

            var info = new SprdFlashInfo
            {
                FlashType = data[0],
                ManufacturerId = data[1],
                DeviceId = BitConverter.ToUInt16(data, 2),
                BlockSize = BitConverter.ToUInt32(data, 4),
                BlockCount = BitConverter.ToUInt32(data, 8),
                TotalSize = BitConverter.ToUInt32(data, 12)
            };

            Log("[FDL] Flash: 类型={0}, 厂商=0x{1:X2}, 设备=0x{2:X4}, 大小={3}",
                info.FlashTypeName, info.ManufacturerId, info.DeviceId, FormatSize(info.TotalSize));

            return info;
        }

        #endregion

        #region 分区表操作

        /// <summary>
        /// 重新分区
        /// </summary>
        public async Task<bool> RepartitionAsync(byte[] partitionTableData)
        {
            if (_stage != FdlStage.FDL2)
            {
                Log("[FDL] 需要先加载 FDL2");
                return false;
            }

            if (partitionTableData == null || partitionTableData.Length == 0)
            {
                Log("[FDL] 分区表数据为空");
                return false;
            }

            Log("[FDL] 重新分区, 数据长度: {0} bytes", partitionTableData.Length);

            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_REPARTITION, partitionTableData);
            await WriteFrameAsync(frame);

            if (await WaitAckAsync(30000))
            {
                Log("[FDL] 重新分区成功");
                return true;
            }

            Log("[FDL] 重新分区失败");
            return false;
        }

        #endregion

        #region 波特率

        /// <summary>
        /// 设置波特率
        /// </summary>
        public async Task<bool> SetBaudRateAsync(int baudRate)
        {
            Log("[FDL] 设置波特率: {0}", baudRate);

            var payload = BitConverter.GetBytes((uint)baudRate);
            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_SET_BAUD, payload);
            await WriteFrameAsync(frame);

            if (await WaitAckAsync(2000))
            {
                // 等待设备切换
                await Task.Delay(100);
                
                // 更新本地波特率
                if (_port != null && _port.IsOpen)
                {
                    _port.BaudRate = baudRate;
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();
                }

                Log("[FDL] 波特率切换成功");
                return true;
            }

            Log("[FDL] 波特率切换失败");
            return false;
        }

        /// <summary>
        /// 检测波特率
        /// </summary>
        public async Task<bool> CheckBaudRateAsync()
        {
            Log("[FDL] 检测波特率...");

            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_CHECK_BAUD);
            await WriteFrameAsync(frame);

            if (await WaitAckAsync(2000))
            {
                Log("[FDL] 波特率检测成功");
                return true;
            }

            Log("[FDL] 波特率检测失败");
            return false;
        }

        #endregion

        #region 强制下载模式

        /// <summary>
        /// 进入强制下载模式
        /// </summary>
        public async Task<bool> EnterForceDownloadAsync()
        {
            Log("[FDL] 进入强制下载模式...");

            try
            {
                // 发送特殊命令进入强制下载模式
                // 1. 发送同步帧
                byte[] syncFrame = new byte[] { 0x7E, 0x7E, 0x7E, 0x7E };
                await _port.BaseStream.WriteAsync(syncFrame, 0, syncFrame.Length);
                await Task.Delay(100);

                // 2. 发送强制下载命令 (使用 BSL_CMD_CONNECT 带特殊标志)
                byte[] payload = new byte[] { 0x00, 0x00, 0x00, 0x01 };  // 强制模式标志
                var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_CONNECT, payload);
                await WriteFrameAsync(frame);

                // 3. 等待响应
                if (await WaitAckAsync(5000))
                {
                    Log("[FDL] 强制下载模式激活成功");
                    return true;
                }

                // 4. 尝试另一种方式：发送复位命令后立即重连
                var resetFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_RESET);
                await WriteFrameAsync(resetFrame);
                
                await Task.Delay(1000);

                // 重新同步
                for (int i = 0; i < 10; i++)
                {
                    await _port.BaseStream.WriteAsync(syncFrame, 0, syncFrame.Length);
                    await Task.Delay(100);
                    
                    if (_port.BytesToRead > 0)
                    {
                        var response = await ReadFrameAsync(1000);
                        if (response != null)
                        {
                            Log("[FDL] 设备响应，强制下载模式可能已激活");
                            return true;
                        }
                    }
                }

                Log("[FDL] 强制下载模式激活失败");
                return false;
            }
            catch (Exception ex)
            {
                Log("[FDL] 强制下载模式异常: {0}", ex.Message);
                return false;
            }
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task<bool> ResetDeviceAsync()
        {
            Log("[FDL] 重启设备...");

            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_RESET);
            await WriteFrameAsync(frame);

            bool success = await WaitAckAsync(2000);
            if (success)
            {
                Log("[FDL] 重启命令发送成功");
                _stage = FdlStage.None;
                SetState(SprdDeviceState.Disconnected);
            }

            return success;
        }

        /// <summary>
        /// 关机
        /// </summary>
        public async Task<bool> PowerOffAsync()
        {
            Log("[FDL] 关机...");

            var frame = _hdlc.BuildCommand((byte)BslCommand.BSL_CMD_POWER_OFF);
            await WriteFrameAsync(frame);

            bool success = await WaitAckAsync(2000);
            if (success)
            {
                Log("[FDL] 关机命令发送成功");
                _stage = FdlStage.None;
                SetState(SprdDeviceState.Disconnected);
            }

            return success;
        }

        /// <summary>
        /// 保持充电 (参考 spreadtrum_flash: keep_charge)
        /// </summary>
        public async Task<bool> KeepChargeAsync(bool enable = true)
        {
            Log("[FDL] 设置保持充电: {0}", enable ? "开启" : "关闭");

            byte[] payload = new byte[4];
            BitConverter.GetBytes(enable ? 1 : 0).CopyTo(payload, 0);

            var frame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_KEEP_CHARGE, payload);
            await WriteFrameAsync(frame);

            bool success = await WaitAckAsync(2000);
            if (success)
            {
                Log("[FDL] 保持充电设置成功");
            }
            return success;
        }

        /// <summary>
        /// 读取原始 Flash (使用特殊分区名称)
        /// 参考 spreadtrum_flash: read_flash
        /// 特殊分区名称:
        ///   - 0x80000001: boot0
        ///   - 0x80000002: boot1
        ///   - 0x80000003: kernel (NOR Flash)
        ///   - 0x80000004: user
        ///   - user_partition: 原始 Flash 访问 (忽略分区)
        ///   - splloader: Bootloader (类似 FDL1)
        ///   - uboot: FDL2 别名
        /// </summary>
        public async Task<byte[]> ReadFlashAsync(uint flashId, uint offset, uint size, CancellationToken cancellationToken = default)
        {
            Log("[FDL] 读取 Flash: ID=0x{0:X8}, 偏移=0x{1:X}, 大小={2}", flashId, offset, FormatSize(size));

            if (_stage != FdlStage.FDL2)
            {
                Log("[FDL] 错误: 需要先加载 FDL2");
                return null;
            }

            // 检查是否使用 DHTB 自动大小
            if (size == 0xFFFFFFFF)  // auto
            {
                var dhtbSize = await ReadDhtbSizeAsync(flashId);
                if (dhtbSize > 0)
                {
                    size = dhtbSize;
                    Log("[FDL] DHTB 自动检测大小: {0}", FormatSize(size));
                }
                else
                {
                    Log("[FDL] DHTB 解析失败，使用默认大小 4MB");
                    size = 4 * 1024 * 1024;
                }
            }

            using (var ms = new MemoryStream())
            {
                // 构建 READ_FLASH 命令 payload
                // [Flash ID (4)] [Offset (4)] [Size (4)]
                byte[] payload = new byte[12];
                BitConverter.GetBytes(flashId).CopyTo(payload, 0);
                BitConverter.GetBytes(offset).CopyTo(payload, 4);
                BitConverter.GetBytes(size).CopyTo(payload, 8);

                var startFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_FLASH, payload);
                if (!await WriteFrameAsyncSafe(startFrame))
                {
                    Log("[FDL] 发送 READ_FLASH 失败");
                    return null;
                }

                // 读取数据块
                uint received = 0;
                int consecutiveErrors = 0;

                while (received < size && !cancellationToken.IsCancellationRequested)
                {
                    var response = await ReadFrameAsyncSafe(30000);
                    if (response == null)
                    {
                        consecutiveErrors++;
                        if (consecutiveErrors > 5)
                        {
                            Log("[FDL] 读取超时");
                            break;
                        }
                        await Task.Delay(100);
                        continue;
                    }

                    try
                    {
                        var frame = _hdlc.ParseFrame(response);

                        if (frame.Type == (byte)BslCommand.BSL_REP_READ_FLASH && frame.Payload != null)
                        {
                            ms.Write(frame.Payload, 0, frame.Payload.Length);
                            received += (uint)frame.Payload.Length;
                            consecutiveErrors = 0;

                            // 发送确认
                            var ackFrame = _hdlc.BuildCommand((byte)BslCommand.BSL_REP_ACK);
                            await WriteFrameAsyncSafe(ackFrame);

                            int progress = (int)(received * 100 / size);
                            if (progress % 10 == 0)
                                Log("[FDL] 读取进度: {0}%", progress);
                        }
                        else if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                        {
                            // 读取完成
                            break;
                        }
                        else
                        {
                            Log("[FDL] 收到意外响应: 0x{0:X2}", frame.Type);
                            consecutiveErrors++;
                        }
                    }
                    catch
                    {
                        consecutiveErrors++;
                    }
                }

                Log("[FDL] Flash 读取完成, 大小: {0}", FormatSize((uint)ms.Length));
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 读取 DHTB 头部获取 Flash 大小 (参考 spreadtrum_flash)
        /// DHTB = Download Header Table Block
        /// </summary>
        private async Task<uint> ReadDhtbSizeAsync(uint flashId)
        {
            try
            {
                // 读取 DHTB 头部 (通常在偏移 0, 大小 512 字节)
                byte[] payload = new byte[12];
                BitConverter.GetBytes(flashId).CopyTo(payload, 0);
                BitConverter.GetBytes((uint)0).CopyTo(payload, 4);  // offset = 0
                BitConverter.GetBytes((uint)512).CopyTo(payload, 8);  // size = 512

                var startFrame = _hdlc.BuildFrame((byte)BslCommand.BSL_CMD_READ_FLASH, payload);
                if (!await WriteFrameAsyncSafe(startFrame))
                    return 0;

                var response = await ReadFrameAsyncSafe(5000);
                if (response == null || response.Length < 16)
                    return 0;

                var frame = _hdlc.ParseFrame(response);
                if (frame.Type != (byte)BslCommand.BSL_REP_READ_FLASH || frame.Payload == null)
                    return 0;

                // 解析 DHTB 头部
                // 格式: [Magic "DHTB" (4)] [Size (4)] [...]
                if (frame.Payload.Length >= 8)
                {
                    string magic = Encoding.ASCII.GetString(frame.Payload, 0, 4);
                    if (magic == "DHTB")
                    {
                        uint size = BitConverter.ToUInt32(frame.Payload, 4);
                        return size;
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 解析特殊分区名称为 Flash ID
        /// 参考 spreadtrum_flash 的特殊分区处理
        /// </summary>
        public static uint ParseSpecialPartitionName(string name)
        {
            switch (name.ToLower())
            {
                case "boot0":
                    return 0x80000001;
                case "boot1":
                    return 0x80000002;
                case "kernel":
                case "nor":
                    return 0x80000003;
                case "user":
                case "user_partition":
                    return 0x80000004;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 判断是否为特殊分区名称
        /// </summary>
        public static bool IsSpecialPartition(string name)
        {
            string lower = name.ToLower();
            return lower == "boot0" || lower == "boot1" || lower == "kernel" ||
                   lower == "nor" || lower == "user" || lower == "user_partition" ||
                   lower == "splloader" || lower == "spl_loader_bak" || lower == "uboot";
        }

        /// <summary>
        /// 获取分区列表 (XML 格式) - 参考 spreadtrum_flash partition_list
        /// </summary>
        public async Task<string> GetPartitionListXmlAsync()
        {
            var partitions = await ReadPartitionTableAsync();
            if (partitions == null || partitions.Count == 0)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<partitions>");

            foreach (var p in partitions)
            {
                sb.AppendLine($"  <partition name=\"{p.Name}\" size=\"0x{p.Size:X}\" />");
            }

            sb.AppendLine("</partitions>");
            return sb.ToString();
        }

        #endregion

        #region 底层通信

        private async Task WriteFrameAsync(byte[] frame)
        {
            if (_isDisposed || _port == null || !_port.IsOpen)
                throw new InvalidOperationException("端口未打开");

            // 使用超时防止死锁
            using (var cts = new CancellationTokenSource(MaxOperationTimeout))
            {
                try
                {
                    await _portLock.WaitAsync(cts.Token);
                    try
                    {
                        if (_port != null && _port.IsOpen)
                {
                    _port.Write(frame, 0, frame.Length);
                }
                    }
                    finally
                    {
                        _portLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("写入操作超时");
                }
            }
        }

        /// <summary>
        /// 安全写入帧 (捕获异常，带超时)
        /// </summary>
        private async Task<bool> WriteFrameAsyncSafe(byte[] frame, int timeout = 0)
        {
            if (_isDisposed || _port == null || !_port.IsOpen)
                return false;

            if (timeout <= 0)
                timeout = DefaultTimeout;

            try
            {
                using (var cts = new CancellationTokenSource(timeout))
                {
                    if (!await _portLock.WaitAsync(timeout, cts.Token))
                        return false;

                    try
                    {
                        if (_port != null && _port.IsOpen)
                        {
                            _port.Write(frame, 0, frame.Length);
                            return true;
                        }
                        return false;
                    }
                    finally
                    {
                        _portLock.Release();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全读取帧 (捕获所有异常)
        /// </summary>
        private async Task<byte[]> ReadFrameAsyncSafe(int timeout = 0)
        {
            try
            {
                return await ReadFrameAsync(timeout);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 安全写入串口 (参考 SPRDClientCore) - 同步方式，带锁
        /// </summary>
        private bool SafeWriteToPort(byte[] data)
        {
            if (_isDisposed)
            {
                Log("[FDL] SafeWriteToPort: 已释放");
                return false;
            }
                
            // 同步获取锁（最多等待 3 秒）
            bool lockAcquired = false;
            try
            {
                lockAcquired = _portLock.Wait(3000);
                if (!lockAcquired)
                {
                    Log("[FDL] SafeWriteToPort: 获取锁超时");
                    return false;
                }
                
                if (_port == null)
                {
                    Log("[FDL] SafeWriteToPort: 端口为 null");
                    return false;
                }
                
                if (!_port.IsOpen)
                {
                    Log("[FDL] SafeWriteToPort: 端口已关闭，尝试重新打开...");
                    try
                    {
                        _port.Open();
                        _port.DiscardInBuffer();
                        _port.DiscardOutBuffer();
                        Log("[FDL] SafeWriteToPort: 端口重新打开成功");
                    }
                    catch (Exception ex)
                    {
                        Log("[FDL] SafeWriteToPort: 重新打开失败 - {0}", ex.Message);
                        return false;
                    }
                }
                    
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
                _port.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                Log("[FDL] SafeWriteToPort 异常: {0}", ex.Message);
                return false;
            }
            finally
            {
                if (lockAcquired)
                    _portLock.Release();
            }
        }

        /// <summary>
        /// 安全从串口读取 (使用轮询方式，避免信号灯超时)
        /// </summary>
        private async Task<byte[]> SafeReadFromPortAsync(int timeout, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return null;

            // 创建组合取消令牌 (外部取消 + 超时)
            using (var timeoutCts = new CancellationTokenSource(timeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                try
                {
            return await Task.Run(() =>
            {
                var ms = new MemoryStream();
                bool inFrame = false;
                        int retryCount = 0;

                        while (!linkedCts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                if (_isDisposed || _port == null || !_port.IsOpen)
                                    return null;

                                int available = 0;
                                try
                                {
                                    available = _port.BytesToRead;
                                }
                                catch
                                {
                                    Thread.Sleep(10);
                                    retryCount++;
                                    if (retryCount > 10) return null;
                                    continue;
                                }

                                if (available > 0)
                                {
                                    byte[] buffer = new byte[available];
                                    int read = 0;
                                    try
                                    {
                                        read = _port.Read(buffer, 0, available);
                                    }
                                    catch
                                    {
                                        Thread.Sleep(10);
                                        retryCount++;
                                        if (retryCount > 10) return null;
                                        continue;
                                    }

                                    for (int i = 0; i < read; i++)
                                    {
                                        byte b = buffer[i];

                                        if (b == HdlcProtocol.HDLC_FLAG)
                                        {
                                            if (inFrame && ms.Length > 0)
                                            {
                                                ms.WriteByte(b);
                                                return ms.ToArray();
                                            }
                                            inFrame = true;
                                            ms = new MemoryStream();
                                        }

                                        if (inFrame)
                                        {
                                            ms.WriteByte(b);
                                        }
                                    }
                                    
                                    retryCount = 0;  // 重置重试计数
                                }
                                else
                                {
                                    Thread.Sleep(5);
                                }
                            }
                            catch
                            {
                                Thread.Sleep(10);
                                retryCount++;
                                if (retryCount > 10) return null;
                            }
                        }

                        // 超时但有部分数据
                        if (ms.Length > 0)
                            return ms.ToArray();
                            
                        return null;
                    }, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 使用轮询方式读取帧 (避免信号灯超时问题)
        /// </summary>
        private async Task<byte[]> ReadWithPollingAsync(int timeout)
        {
            return await Task.Run(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var ms = new MemoryStream();
                bool inFrame = false;

                while (stopwatch.ElapsedMilliseconds < timeout)
                {
                    try
                    {
                        if (_port == null || !_port.IsOpen)
                            return null;

                        // 使用 BytesToRead 检查是否有数据，避免阻塞
                        int available = 0;
                        try
                        {
                            available = _port.BytesToRead;
                        }
                        catch
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        if (available > 0)
                        {
                            byte[] buffer = new byte[available];
                            int read = 0;
                            try
                            {
                                read = _port.Read(buffer, 0, available);
                            }
                            catch
                            {
                                Thread.Sleep(10);
                                continue;
                            }

                            for (int i = 0; i < read; i++)
                            {
                                byte b = buffer[i];

                            if (b == HdlcProtocol.HDLC_FLAG)
                            {
                                if (inFrame && ms.Length > 0)
                                {
                                        ms.WriteByte(b);
                                    return ms.ToArray();
                                }
                                inFrame = true;
                                ms = new MemoryStream();
                            }

                            if (inFrame)
                            {
                                    ms.WriteByte(b);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }
                    catch
                    {
                        Thread.Sleep(10);
                }
                }

                return null;
            });
        }

        private async Task<byte[]> ReadFrameAsync(int timeout = 0, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return null;

            if (timeout == 0)
                timeout = DefaultTimeout;

            // 创建组合取消令牌
            using (var timeoutCts = new CancellationTokenSource(timeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                try
                {
                    return await Task.Run(() =>
                    {
                        var ms = new MemoryStream();
                        bool inFrame = false;

                        try
                        {
                            while (!linkedCts.Token.IsCancellationRequested)
                            {
                                if (_isDisposed || _port == null || !_port.IsOpen)
                                    return null;

                                try
                                {
                                    int bytesToRead = _port.BytesToRead;
                                    if (bytesToRead > 0)
                                    {
                                        // 读取所有可用字节
                                        byte[] buffer = new byte[bytesToRead];
                                        int read = _port.Read(buffer, 0, bytesToRead);
                                        
                                        for (int i = 0; i < read; i++)
                                        {
                                            byte b = buffer[i];
                                            
                                            if (b == HdlcProtocol.HDLC_FLAG)
                                            {
                                                if (inFrame && ms.Length > 0)
                                                {
                                                    ms.WriteByte(b);
                                                    return ms.ToArray();
                                                }
                                                inFrame = true;
                                                ms = new MemoryStream();
                                            }

                                            if (inFrame)
                                            {
                                                ms.WriteByte(b);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Thread.Sleep(5);
                                    }
                                }
                                catch (TimeoutException)
                                {
                                    // 串口超时，继续等待
                                    Thread.Sleep(10);
                                }
                                catch (InvalidOperationException)
                                {
                                    // 端口可能已关闭
                                    return null;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // 其他异常，返回已收到的数据（如果有）
                            if (ms.Length > 0)
                                return ms.ToArray();
                        }

                        return null;
                    }, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        private async Task<bool> WaitAckAsync(int timeout = 0, bool verbose = false)
        {
            return await WaitAckWithDetailAsync(timeout, null, verbose);
        }
        
        /// <summary>
        /// 等待 ACK 响应 (安全版本，捕获所有异常)
        /// </summary>
        private async Task<bool> WaitAckAsyncSafe(int timeout = 0, bool verbose = false)
        {
            try
            {
                return await WaitAckWithDetailAsync(timeout, null, verbose);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 等待 ACK 响应 (带详细日志)
        /// </summary>
        private async Task<bool> WaitAckWithDetailAsync(int timeout = 0, string context = null, bool verbose = true)
        {
            if (timeout == 0)
                timeout = DefaultTimeout;

            var response = await ReadFrameAsync(timeout);
            if (response != null)
            {
                if (verbose)
                {
                    Log("[FDL] {0}收到响应 ({1} bytes): {2}", 
                        context != null ? context + " " : "",
                        response.Length, 
                        BitConverter.ToString(response).Replace("-", " "));
                }
                
                try
                {
                    var frame = _hdlc.ParseFrame(response);
                    if (frame.Type == (byte)BslCommand.BSL_REP_ACK)
                    {
                        return true;
                    }
                    else
                    {
                        // 记录非 ACK 响应以便调试
                        string errorMsg = GetBslErrorMessage(frame.Type);
                        Log("[FDL] 收到非 ACK 响应: 0x{0:X2} ({1})", frame.Type, errorMsg);
                        if (frame.Payload != null && frame.Payload.Length > 0)
                        {
                            Log("[FDL] 响应数据: {0}", BitConverter.ToString(frame.Payload).Replace("-", " "));
                        }
                        
                        // 提供具体的错误提示
                        switch (frame.Type)
                        {
                            case 0x8B: // BSL_REP_VERIFY_ERROR
                                Log("[FDL] 校验错误: 可能原因:");
                                Log("[FDL]   1. FDL 文件与芯片不匹配");
                                Log("[FDL]   2. 加载地址错误 (当前: 0x{0:X8})", CustomFdl1Address > 0 ? CustomFdl1Address : SprdPlatform.GetFdl1Address(ChipId));
                                Log("[FDL]   3. FDL 文件损坏或格式错误");
                                break;
                            case 0x89: // BSL_REP_DOWN_DEST_ERROR
                                Log("[FDL] 目标地址错误");
                                break;
                            case 0x8A: // BSL_REP_DOWN_SIZE_ERROR
                                Log("[FDL] 数据大小错误");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("[FDL] 解析响应异常: {0}", ex.Message);
                    Log("[FDL] 原始响应: {0}", BitConverter.ToString(response).Replace("-", " "));
                }
            }
            else
            {
                if (verbose)
                Log("[FDL] 等待响应超时 ({0}ms)", timeout);
            }
            return false;
        }

        /// <summary>
        /// 发送命令并等待 ACK (带重试)
        /// </summary>
        private async Task<bool> SendCommandWithRetryAsync(byte command, byte[] payload = null, int timeout = 0, int retries = -1)
        {
            if (retries < 0)
                retries = CommandRetries;
            if (timeout == 0)
                timeout = DefaultTimeout;

            for (int attempt = 0; attempt <= retries; attempt++)
            {
                if (attempt > 0)
                {
                    Log("[FDL] 重试 {0}/{1}...", attempt, retries);
                    await Task.Delay(RetryDelayMs);
                    
                    // 重试前清空缓冲区
                    try { _port?.DiscardInBuffer(); } catch { }
                }

                try
                {
                    var frame = _hdlc.BuildFrame(command, payload ?? new byte[0]);
                    
                    // 使用安全写入，避免抛出异常
                    if (!await WriteFrameAsyncSafe(frame))
                    {
                        Log("[FDL] 帧写入失败，重试...");
                        continue;
                    }
                    
                    if (await WaitAckAsyncSafe(timeout))
                        return true;
                }
                catch (Exception ex)
                {
                    Log("[FDL] 命令发送异常: {0}", ex.Message);
                    // 不再抛出异常，继续重试
                }
            }
            return false;
        }

        /// <summary>
        /// 发送数据帧并等待 ACK (带重试，用于数据块传输)
        /// </summary>
        private async Task<bool> SendDataWithRetryAsync(byte command, byte[] data, int timeout = 0, int maxRetries = 2)
        {
            if (timeout == 0)
                timeout = DefaultTimeout;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    Log("[FDL] 数据重传 {0}/{1}...", attempt, maxRetries);
                    await Task.Delay(RetryDelayMs / 2);  // 数据块重试间隔更短
                }

                try
                {
                    var frame = _hdlc.BuildFrame(command, data);
                    await WriteFrameAsync(frame);
                    
                    if (await WaitAckAsync(timeout))
                        return true;
                }
                catch (TimeoutException)
                {
                    // 超时继续重试
                }
                catch (IOException)
                {
                    // IO 错误继续重试
                }
            }
            return false;
        }

        #endregion

        #region 辅助方法

        private void SetState(SprdDeviceState state)
        {
            if (_state != state)
            {
                _state = state;
                OnStateChanged?.Invoke(state);
            }
        }

        private void Log(string format, params object[] args)
        {
            string message = string.Format(format, args);
            OnLog?.Invoke(message);
        }

        /// <summary>
        /// 调试日志 (仅输出到调试窗口，不显示在 UI)
        /// </summary>
        private void LogDebug(string format, params object[] args)
        {
            string message = string.Format(format, args);
            System.Diagnostics.Debug.WriteLine(message);
        }

        /// <summary>
        /// 获取 BSL 错误码说明
        /// </summary>
        private string GetBslErrorMessage(byte errorCode)
        {
            switch (errorCode)
            {
                case 0x80: return "成功";
                case 0x81: return "版本信息";
                case 0x82: return "无效命令";
                case 0x83: return "数据错误";
                case 0x84: return "操作失败";
                case 0x85: return "不支持的波特率";
                case 0x86: return "下载未开始";
                case 0x87: return "重复开始下载";
                case 0x88: return "下载提前结束";
                case 0x89: return "下载目标地址错误";
                case 0x8A: return "下载大小错误";
                case 0x8B: return "校验错误 - FDL 文件可能不匹配";
                case 0x8C: return "未校验";
                case 0x8D: return "内存不足";
                case 0x8E: return "等待输入超时";
                case 0x8F: return "操作成功";
                case 0xA6: return "签名校验失败";
                case 0xFE: return "不支持的命令";
                default: return "未知错误";
            }
        }

        private static string FormatSize(uint size)
        {
            if (size >= 1024 * 1024 * 1024)
                return string.Format("{0:F2} GB", size / (1024.0 * 1024 * 1024));
            if (size >= 1024 * 1024)
                return string.Format("{0:F2} MB", size / (1024.0 * 1024));
            if (size >= 1024)
                return string.Format("{0:F2} KB", size / 1024.0);
            return string.Format("{0} B", size);
        }

        /// <summary>
        /// 写入 32 位值 (Big-Endian 格式)
        /// </summary>
        private static void WriteBigEndian32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        /// <summary>
        /// 写入 16 位值 (Big-Endian 格式)
        /// </summary>
        private static void WriteBigEndian16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 取消所有挂起的操作
                try
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;
                }
                catch (ObjectDisposedException) { /* 已释放，忽略 */ }

                // 安全断开连接
                SafeDisconnect();
                
                // 释放 SemaphoreSlim
                try
                {
                    _portLock?.Dispose();
                }
                catch (ObjectDisposedException) { /* 已释放，忽略 */ }
            }

            _disposed = true;
        }

        /// <summary>
        /// 安全断开连接 (带超时保护)
        /// </summary>
        private void SafeDisconnect()
        {
            try
            {
                if (_port != null)
                {
                    // 标记为已释放，阻止新操作
                    _isDisposed = true;

                    // 先清空缓冲区 (忽略异常，确保后续清理继续)
                    if (_port.IsOpen)
                    {
                        try
                        {
                            _port.DiscardInBuffer();
                            _port.DiscardOutBuffer();
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FDL] 清空缓冲区异常: {ex.Message}"); }
                    }

                    // 异步关闭端口 (带超时，避免死锁)
                    try
                    {
                        using (var cts = new CancellationTokenSource(2000))
                        {
                            var closeTask = Task.Run(() =>
                            {
                                try
                                {
                                    if (_port != null && _port.IsOpen)
                                        _port.Close();
                                }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FDL] 关闭端口异常: {ex.Message}"); }
                            });

                            // 使用同步等待，最多等待 2 秒
                            bool completed = closeTask.Wait(2000);
                            if (!completed)
                            {
                                Log("[FDL] 警告: 端口关闭超时");
                            }
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FDL] 端口关闭任务异常: {ex.Message}"); }

                    try
                    {
                        _port?.Dispose();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FDL] 端口释放异常: {ex.Message}"); }
                    _port = null;
                }
            }
            catch (Exception ex)
            {
                Log("[FDL] 断开连接异常: {0}", ex.Message);
            }
            finally
            {
                _stage = FdlStage.None;
                SetState(SprdDeviceState.Disconnected);
            }
        }

        ~FdlClient()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// 展讯分区信息
    /// </summary>
    public class SprdPartitionInfo
    {
        public string Name { get; set; }
        public uint Offset { get; set; }
        public uint Size { get; set; }

        public override string ToString()
        {
            return string.Format("{0} (0x{1:X8}, {2} bytes)", Name, Offset, Size);
        }
    }

    /// <summary>
    /// 展讯 Flash 信息
    /// </summary>
    public class SprdFlashInfo
    {
        public byte FlashType { get; set; }
        public byte ManufacturerId { get; set; }
        public ushort DeviceId { get; set; }
        public uint BlockSize { get; set; }
        public uint BlockCount { get; set; }
        public uint TotalSize { get; set; }
        public string ChipModel { get; set; }

        public string FlashTypeName
        {
            get
            {
                switch (FlashType)
                {
                    case 0: return "Unknown";
                    case 1: return "NAND";
                    case 2: return "NOR";
                    case 3: return "eMMC";
                    case 4: return "UFS";
                    default: return string.Format("Type_{0}", FlashType);
                }
            }
        }

        public string ManufacturerName
        {
            get
            {
                switch (ManufacturerId)
                {
                    case 0x15: return "Samsung";
                    case 0x45: return "SanDisk";
                    case 0x90: return "Hynix";
                    case 0xFE: return "Micron";
                    case 0x13: return "Toshiba";
                    case 0x70: return "Kingston";
                    default: return string.Format("0x{0:X2}", ManufacturerId);
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", FlashTypeName, ManufacturerName, 
                TotalSize >= 1024 * 1024 * 1024 
                    ? string.Format("{0:F1} GB", TotalSize / (1024.0 * 1024 * 1024))
                    : string.Format("{0} MB", TotalSize / (1024 * 1024)));
        }
    }

    /// <summary>
    /// 展讯安全信息
    /// </summary>
    public class SprdSecurityInfo
    {
        public bool IsLocked { get; set; }
        public bool RequiresSignature { get; set; }
        public byte[] PublicKey { get; set; }
        public uint SecurityVersion { get; set; }

        // 扩展属性
        public bool IsSecureBootEnabled { get; set; }
        public bool IsEfuseLocked { get; set; }
        public bool IsAntiRollbackEnabled { get; set; }
        public string PublicKeyHash { get; set; }
        public byte[] RawEfuseData { get; set; }

        public override string ToString()
        {
            return string.Format("安全启动: {0}, eFuse锁定: {1}, 防回滚: {2}, 版本: {3}",
                IsSecureBootEnabled ? "是" : "否",
                IsEfuseLocked ? "是" : "否",
                IsAntiRollbackEnabled ? "是" : "否",
                SecurityVersion);
        }
    }

    /// <summary>
    /// NV 项 ID 常量
    /// </summary>
    public static class SprdNvItems
    {
        public const ushort NV_IMEI = 0;
        public const ushort NV_IMEI2 = 1;
        public const ushort NV_BT_ADDR = 2;
        public const ushort NV_WIFI_ADDR = 3;
        public const ushort NV_SERIAL_NUMBER = 4;
        public const ushort NV_CALIBRATION = 100;
        public const ushort NV_RF_CALIBRATION = 101;
        public const ushort NV_GPS_CONFIG = 200;
        public const ushort NV_AUDIO_PARAM = 300;
    }
}
