// ============================================================================
// SakuraEDL - MediaTek BROM 协议客户端
// MediaTek Boot ROM Protocol Client
// ============================================================================
// 参考: mtkclient 项目 mtk_preloader.py
// ============================================================================

using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Models;
using SakuraEDL.MediaTek.DA;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// BROM 协议客户端 - 负责握手、设备信息读取和 DA 上传
    /// </summary>
    public class BromClient : IDisposable, IBromClient
    {
        private SerialPort _port;
        private readonly Action<string> _log;
        private readonly Action<string> _logDetail;
        private readonly Action<double> _progressCallback;
        private readonly MtkLogger _logger;
        private bool _disposed;
        
        // 线程安全: 端口锁
        private readonly SemaphoreSlim _portLock = new SemaphoreSlim(1, 1);

        // 配置
        private const int DEFAULT_TIMEOUT_MS = 5000;
        private const int HANDSHAKE_TIMEOUT_MS = 30000;
        private const int MAX_PACKET_SIZE = 4096;

        // 协议状态
        public bool IsConnected { get; private set; }
        public bool IsBromMode { get; private set; }
        public MtkDeviceState State { get; internal set; }
        
        /// <summary>
        /// 最后一次上传的状态码
        /// 0x0000 = 正常成功
        /// 0x7015 = DAA 签名验证失败
        /// 0x7017 = DAA 安全错误 (设备启用了 DAA 保护)
        /// </summary>
        public ushort LastUploadStatus { get; private set; }

        // 设备信息
        public ushort HwCode { get; private set; }
        public ushort HwVer { get; private set; }
        public ushort HwSubCode { get; private set; }
        public ushort SwVer { get; private set; }
        public byte BromVer { get; private set; }
        public byte BlVer { get; private set; }
        public byte[] MeId { get; private set; }
        public byte[] SocId { get; private set; }
        public TargetConfigFlags TargetConfig { get; private set; }
        public MtkChipInfo ChipInfo { get; private set; }

        public BromClient(Action<string> log = null, Action<string> logDetail = null, Action<double> progressCallback = null)
        {
            _log = log ?? delegate { };
            _logDetail = logDetail ?? _log;
            _progressCallback = progressCallback;
            _logger = null;  // 使用传统回调
            State = MtkDeviceState.Disconnected;
            ChipInfo = new MtkChipInfo();
        }

        /// <summary>
        /// 构造函数 (使用MtkLogger)
        /// </summary>
        public BromClient(MtkLogger logger, Action<double> progressCallback = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _log = msg => _logger.Info(msg, LogCategory.Brom);
            _logDetail = msg => _logger.Verbose(msg, LogCategory.Brom);
            _progressCallback = progressCallback;
            State = MtkDeviceState.Disconnected;
            ChipInfo = new MtkChipInfo();
        }

        #region 连接管理

        /// <summary>
        /// 连接到串口
        /// </summary>
        public async Task<bool> ConnectAsync(string portName, int baudRate = 921600, CancellationToken ct = default)
        {
            try
            {
                if (_port != null && _port.IsOpen)
                {
                    _port.Close();
                }

                _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = DEFAULT_TIMEOUT_MS,
                    WriteTimeout = DEFAULT_TIMEOUT_MS,
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadBufferSize = 16 * 1024 * 1024,  // 16MB 缓冲区
                    WriteBufferSize = 16 * 1024 * 1024
                };

                _port.Open();
                await Task.Delay(100, ct);

                // 清空缓冲区
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();

                IsConnected = true;
                State = MtkDeviceState.Handshaking;
                _log($"[MTK] 串口已打开: {portName}");

                return true;
            }
            catch (Exception ex)
            {
                _log($"[MTK] 连接失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_port != null && _port.IsOpen)
                {
                    _port.Close();
                }
            }
            catch (Exception ex)
            {
                _logDetail($"[MTK] 断开连接时异常: {ex.Message}");
            }

            IsConnected = false;
            State = MtkDeviceState.Disconnected;
        }

        /// <summary>
        /// 获取内部串口 (供漏洞利用使用)
        /// </summary>
        public SerialPort GetPort() => _port;

        /// <summary>
        /// 检查端口是否打开
        /// </summary>
        public bool IsPortOpen => _port != null && _port.IsOpen;

        /// <summary>
        /// 获取当前端口名
        /// </summary>
        public string PortName => _port?.PortName ?? "";

        #endregion

        #region 握手 (BROM/Preloader 通用)

        /// <summary>
        /// 执行握手 (BROM 和 Preloader 模式使用相同的握手序列)
        /// </summary>
        public async Task<bool> HandshakeAsync(int maxTries = 100, CancellationToken ct = default)
        {
            if (!IsConnected || _port == null)
                return false;

            _log("[MTK] 开始握手...");
            State = MtkDeviceState.Handshaking;
            
            // 握手前清空缓冲区，防止残留数据干扰
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();

            for (int tries = 0; tries < maxTries; tries++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                try
                {
                    // 发送握手字节 0xA0
                    _port.Write(new byte[] { BromHandshake.HANDSHAKE_SEND }, 0, 1);
                    
                    await Task.Delay(10, ct);

                    // 检查响应
                    if (_port.BytesToRead > 0)
                    {
                        byte[] response = new byte[_port.BytesToRead];
                        _port.Read(response, 0, response.Length);

                        // 检查是否收到 0x5F
                        foreach (byte b in response)
                        {
                            if (b == BromHandshake.HANDSHAKE_RESPONSE)
                            {
                                // 继续发送剩余握手序列
                                bool success = await CompleteHandshakeAsync(ct);
                                if (success)
                                {
                                    _log("[MTK] ✓ 握手成功");
                                    // 握手成功后清空缓冲区，准备接收后续命令
                                    _port.DiscardInBuffer();
                                    _port.DiscardOutBuffer();
                                    // 注意: 实际模式 (BROM/Preloader) 在 InitializeAsync 中根据 BL Ver 设置
                                    return true;
                                }
                            }
                        }
                    }

                    if (tries % 20 == 0 && tries > 0)
                    {
                        _logDetail($"[MTK] 握手重试中... ({tries}/{maxTries})");
                        // 每20次重试清空一次缓冲区，避免数据堆积
                        _port.DiscardInBuffer();
                        _port.DiscardOutBuffer();
                    }

                    // 动态调整重试间隔: 初期快速重试，后期延长间隔
                    int delayMs = tries < 20 ? 50 : (tries < 50 ? 100 : 200);
                    await Task.Delay(delayMs, ct);
                }
                catch (TimeoutException)
                {
                    // 超时，继续重试
                }
                catch (Exception ex)
                {
                    _logDetail($"[MTK] 握手异常: {ex.Message}");
                }
            }

            _log("[MTK] ❌ 握手超时");
            State = MtkDeviceState.Error;
            
            // 失败后清空缓冲区
            try
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BROM] 清空缓冲区异常: {ex.Message}"); }
            
            return false;
        }

        /// <summary>
        /// 完成握手序列
        /// </summary>
        private async Task<bool> CompleteHandshakeAsync(CancellationToken ct)
        {
            try
            {
                // 已收到 0x5F，继续发送 0x0A
                _port.Write(new byte[] { 0x0A }, 0, 1);
                await Task.Delay(10, ct);

                // 期望收到 0xF5
                byte[] resp1 = await ReadBytesAsync(1, 1000, ct);
                if (resp1 == null || resp1[0] != 0xF5)
                {
                    _logDetail($"[MTK] 握手序列错误: 期望 0xF5, 收到 0x{resp1?[0]:X2}");
                    return false;
                }

                // 发送 0x50
                _port.Write(new byte[] { 0x50 }, 0, 1);
                await Task.Delay(10, ct);

                // 期望收到 0xAF
                byte[] resp2 = await ReadBytesAsync(1, 1000, ct);
                if (resp2 == null || resp2[0] != 0xAF)
                {
                    _logDetail($"[MTK] 握手序列错误: 期望 0xAF, 收到 0x{resp2?[0]:X2}");
                    return false;
                }

                // 发送 0x05
                _port.Write(new byte[] { 0x05 }, 0, 1);
                await Task.Delay(10, ct);

                // 期望收到 0xFA
                byte[] resp3 = await ReadBytesAsync(1, 1000, ct);
                if (resp3 == null || resp3[0] != 0xFA)
                {
                    _logDetail($"[MTK] 握手序列错误: 期望 0xFA, 收到 0x{resp3?[0]:X2}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logDetail($"[MTK] 握手序列异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 设备信息读取

        /// <summary>
        /// 初始化设备 (读取芯片信息)
        /// </summary>
        public async Task<bool> InitializeAsync(bool skipWdt = false, CancellationToken ct = default)
        {
            // 握手成功后才能初始化
            if (State == MtkDeviceState.Disconnected || State == MtkDeviceState.Error)
                return false;

            try
            {
                // 1. 获取硬件代码
                var hwInfo = await GetHwCodeAsync(ct);
                if (hwInfo != null)
                {
                    HwCode = hwInfo.Value.hwCode;
                    HwVer = hwInfo.Value.hwVer;
                    _log($"[MTK] HW Code: 0x{HwCode:X4}");
                    _log($"[MTK] HW Ver: 0x{HwVer:X4}");
                    
                    // 从数据库加载完整芯片信息
                    var chipRecord = Database.MtkChipDatabase.GetChip(HwCode);
                    if (chipRecord != null)
                    {
                        ChipInfo = Database.MtkChipDatabase.ToChipInfo(chipRecord);
                        ChipInfo.HwVer = HwVer;  // 保留设备报告的版本
                        
                        // 输出格式参考 mtkclient
                        _log($"\tCPU:\t{ChipInfo.ChipName}({ChipInfo.Description})");
                        _log($"\tHW version:\t0x{HwVer:X}");
                        _log($"\tWDT:\t\t0x{ChipInfo.WatchdogAddr:X}");
                        _log($"\tUART:\t\t0x{ChipInfo.UartAddr:X}");
                        _log($"\tBrom Payload 地址:\t0x{ChipInfo.BromPayloadAddr:X}");
                        _log($"\tDA Payload 地址:\t0x{ChipInfo.DaPayloadAddr:X}");
                        if (ChipInfo.CqDmaBase.HasValue)
                            _log($"\tCQ_DMA 地址:\t0x{ChipInfo.CqDmaBase.Value:X}");
                        _log($"\tVar1:\t\t0xA");  // 默认值
                    }
                    else
                    {
                        // 未知芯片，使用默认值
                        ChipInfo.HwCode = HwCode;
                        ChipInfo.HwVer = HwVer;
                        ChipInfo.WatchdogAddr = 0x10007000;
                        ChipInfo.UartAddr = 0x11002000;
                        ChipInfo.BromPayloadAddr = 0x100A00;
                        ChipInfo.DaPayloadAddr = 0x200000;  // 默认地址
                        
                        _log($"[MTK] 未知芯片: 0x{HwCode:X4} (使用默认配置)");
                        _log($"\tWDT:\t\t0x{ChipInfo.WatchdogAddr:X}");
                        _log($"\tDA Payload 地址:\t0x{ChipInfo.DaPayloadAddr:X}");
                    }
                }

                // 2. 发送心跳/同步 (ChimeraTool 发送 a0 * 20)
                _log("[MTK] 发送同步心跳...");
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        _port.Write(new byte[] { 0xA0 }, 0, 1);
                        await Task.Delay(5, ct);
                        if (_port.BytesToRead > 0)
                        {
                            byte[] resp = new byte[_port.BytesToRead];
                            _port.Read(resp, 0, resp.Length);
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BROM] 清空状态异常: {ex.Message}"); }
                }
                
                // 3. 获取目标配置 (ChimeraTool 执行)
                var config = await GetTargetConfigAsync(ct);
                if (config != null)
                {
                    TargetConfig = config.Value;
                    _log($"[MTK] Target Config: 0x{(uint)TargetConfig:X8}");
                    LogTargetConfig(TargetConfig);
                }

                // 4. 获取 BL 版本 (判断模式)
                BlVer = await GetBlVerAsync(ct);
                IsBromMode = (BlVer == BromCommands.CMD_GET_BL_VER);
                
                if (IsBromMode)
                {
                    _log("[MTK] 模式: BROM (Boot ROM)");
                    State = MtkDeviceState.Brom;
                }
                else
                {
                    _log($"[MTK] 模式: Preloader (BL Ver: {BlVer})");
                    State = MtkDeviceState.Preloader;
                }

                // 5. 获取 ME ID (ChimeraTool 执行)
                MeId = await GetMeIdAsync(ct);
                if (MeId != null && MeId.Length > 0)
                {
                    _logDetail($"[MTK] ME ID: {BitConverter.ToString(MeId).Replace("-", "")}");
                }
                
                // 6. 其他信息 (可选，用于显示)
                BromVer = await GetBromVerAsync(ct);
                var hwSwVer = await GetHwSwVerAsync(ct);
                if (hwSwVer != null)
                {
                    HwSubCode = hwSwVer.Value.hwSubCode;
                    HwVer = hwSwVer.Value.hwVer;
                    SwVer = hwSwVer.Value.swVer;
                }
                SocId = await GetSocIdAsync(ct);

                return true;
            }
            catch (Exception ex)
            {
                _log($"[MTK] 初始化失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取硬件代码
        /// </summary>
        public async Task<(ushort hwCode, ushort hwVer)?> GetHwCodeAsync(CancellationToken ct = default)
        {
            if (!await EchoAsync(BromCommands.CMD_GET_HW_CODE, ct))
                return null;

            var response = await ReadBytesAsync(4, DEFAULT_TIMEOUT_MS, ct);
            if (response == null || response.Length < 4)
                return null;

            ushort hwCode = MtkDataPacker.UnpackUInt16BE(response, 0);
            ushort hwVer = MtkDataPacker.UnpackUInt16BE(response, 2);

            return (hwCode, hwVer);
        }

        /// <summary>
        /// 获取目标配置
        /// </summary>
        public async Task<TargetConfigFlags?> GetTargetConfigAsync(CancellationToken ct = default)
        {
            if (!await EchoAsync(BromCommands.CMD_GET_TARGET_CONFIG, ct))
                return null;

            var response = await ReadBytesAsync(6, DEFAULT_TIMEOUT_MS, ct);
            if (response == null || response.Length < 6)
                return null;

            uint config = MtkDataPacker.UnpackUInt32BE(response, 0);
            ushort status = MtkDataPacker.UnpackUInt16BE(response, 4);

            if (status > 0xFF)
                return null;

            return (TargetConfigFlags)config;
        }

        /// <summary>
        /// 获取 BL 版本 (带线程安全)
        /// </summary>
        public async Task<byte> GetBlVerAsync(CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(new byte[] { BromCommands.CMD_GET_BL_VER }, 0, 1);
                var response = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                return response != null && response.Length > 0 ? response[0] : (byte)0;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 获取 BROM 版本 (带线程安全)
        /// </summary>
        public async Task<byte> GetBromVerAsync(CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(new byte[] { BromCommands.CMD_GET_VERSION }, 0, 1);
                var response = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                return response != null && response.Length > 0 ? response[0] : (byte)0;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 获取硬件/软件版本
        /// </summary>
        public async Task<(ushort hwSubCode, ushort hwVer, ushort swVer, ushort reserved)?> GetHwSwVerAsync(CancellationToken ct = default)
        {
            var response = await SendCmdAsync(BromCommands.CMD_GET_HW_SW_VER, 8, ct);
            if (response == null || response.Length < 8)
                return null;

            return (
                MtkDataPacker.UnpackUInt16BE(response, 0),
                MtkDataPacker.UnpackUInt16BE(response, 2),
                MtkDataPacker.UnpackUInt16BE(response, 4),
                MtkDataPacker.UnpackUInt16BE(response, 6)
            );
        }

        /// <summary>
        /// 获取 ME ID (带线程安全)
        /// </summary>
        public async Task<byte[]> GetMeIdAsync(CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                // 先检查 BL 版本
                _port.Write(new byte[] { BromCommands.CMD_GET_BL_VER }, 0, 1);
                var blResp = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                if (blResp == null) return null;

                // 发送 GET_ME_ID 命令
                _port.Write(new byte[] { BromCommands.CMD_GET_ME_ID }, 0, 1);
                var cmdResp = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                if (cmdResp == null || cmdResp[0] != BromCommands.CMD_GET_ME_ID)
                    return null;

                // 读取长度
                var lenResp = await ReadBytesInternalAsync(4, DEFAULT_TIMEOUT_MS, ct);
                if (lenResp == null) return null;

                uint length = MtkDataPacker.UnpackUInt32BE(lenResp, 0);
                if (length == 0 || length > 64) return null;

                // 读取 ME ID
                var meId = await ReadBytesInternalAsync((int)length, DEFAULT_TIMEOUT_MS, ct);
                if (meId == null) return null;

                // 读取状态
                var statusResp = await ReadBytesInternalAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (statusResp == null) return null;

                ushort status = MtkDataPacker.UnpackUInt16LE(statusResp, 0);
                if (status != 0) return null;

                return meId;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 获取 SOC ID (带线程安全)
        /// </summary>
        public async Task<byte[]> GetSocIdAsync(CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                // 先检查 BL 版本
                _port.Write(new byte[] { BromCommands.CMD_GET_BL_VER }, 0, 1);
                var blResp = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                if (blResp == null) 
                {
                    // 清空可能的残留数据
                    await Task.Delay(50, ct);
                    if (_port.BytesToRead > 0)
                    {
                        byte[] junk = new byte[_port.BytesToRead];
                        _port.Read(junk, 0, junk.Length);
                    }
                    return null;
                }

                // 发送 GET_SOC_ID 命令
                _port.Write(new byte[] { BromCommands.CMD_GET_SOC_ID }, 0, 1);
                var cmdResp = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                if (cmdResp == null || cmdResp[0] != BromCommands.CMD_GET_SOC_ID)
                {
                    // 设备可能不支持此命令，清空残留数据
                    await Task.Delay(50, ct);
                    if (_port.BytesToRead > 0)
                    {
                        byte[] junk = new byte[_port.BytesToRead];
                        _port.Read(junk, 0, junk.Length);
                    }
                    return null;
                }

                // 读取长度
                var lenResp = await ReadBytesInternalAsync(4, DEFAULT_TIMEOUT_MS, ct);
                if (lenResp == null) return null;

                uint length = MtkDataPacker.UnpackUInt32BE(lenResp, 0);
                if (length == 0 || length > 64) return null;

                // 读取 SOC ID
                var socId = await ReadBytesInternalAsync((int)length, DEFAULT_TIMEOUT_MS, ct);
                if (socId == null) return null;

                // 读取状态
                var statusResp = await ReadBytesInternalAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (statusResp == null) return null;

                ushort status = MtkDataPacker.UnpackUInt16LE(statusResp, 0);
                if (status != 0) return null;

                return socId;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 输出目标配置详情
        /// </summary>
        private void LogTargetConfig(TargetConfigFlags config)
        {
            bool sbc = config.HasFlag(TargetConfigFlags.SbcEnabled);
            bool sla = config.HasFlag(TargetConfigFlags.SlaEnabled);
            bool daa = config.HasFlag(TargetConfigFlags.DaaEnabled);
            
            // 输出主要安全状态
            _log($"\tSBC (Secure Boot):\t{sbc}");
            _log($"\tSLA (Secure Link Auth):\t{sla}");
            _log($"\tDAA (Download Agent Auth):\t{daa}");
            
            // 检测保护状态
            if (sbc || daa)
            {
                _log("设备处于保护状态");
            }
        }

        #endregion

        #region 内存读写

        /// <summary>
        /// 读取 32 位数据
        /// </summary>
        public async Task<uint[]> Read32Async(uint address, int count = 1, CancellationToken ct = default)
        {
            if (!await EchoAsync(BromCommands.CMD_READ32, ct))
                return null;

            // 发送地址
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE(address), ct))
                return null;

            // 发送数量
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)count), ct))
                return null;

            // 读取状态
            var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            if (statusResp == null) return null;

            ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
            if (!BromErrorHelper.IsSuccess(status))
                return null;

            // 读取数据
            uint[] result = new uint[count];
            for (int i = 0; i < count; i++)
            {
                var data = await ReadBytesAsync(4, DEFAULT_TIMEOUT_MS, ct);
                if (data == null) return null;
                result[i] = MtkDataPacker.UnpackUInt32BE(data, 0);
            }

            // 读取最终状态
            var status2Resp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            if (status2Resp == null) return null;

            return result;
        }

        /// <summary>
        /// 写入 32 位数据
        /// </summary>
        public async Task<bool> Write32Async(uint address, uint[] values, CancellationToken ct = default)
        {
            if (!await EchoAsync(BromCommands.CMD_WRITE32, ct))
                return false;

            // 发送地址
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE(address), ct))
                return false;

            // 发送数量
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)values.Length), ct))
                return false;

            // 读取状态
            var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            if (statusResp == null) return false;

            ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
            if (!BromErrorHelper.IsSuccess(status))
            {
                _log($"[MTK] Write32 状态错误: {BromErrorHelper.GetErrorMessage(status)}");
                return false;
            }

            // 写入数据
            foreach (uint value in values)
            {
                if (!await EchoAsync(MtkDataPacker.PackUInt32BE(value), ct))
                    return false;
            }

            // 读取最终状态
            var status2Resp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            if (status2Resp == null) return false;

            ushort status2 = MtkDataPacker.UnpackUInt16BE(status2Resp, 0);
            return BromErrorHelper.IsSuccess(status2);
        }

        /// <summary>
        /// 禁用看门狗
        /// </summary>
        private async Task<bool> DisableWatchdogAsync(CancellationToken ct = default)
        {
            // 默认看门狗地址和值 (MT6765 等常见芯片)
            uint wdtAddr = 0x10007000;
            uint wdtValue = 0x22000000;

            // 根据 HW Code 调整
            switch (HwCode)
            {
                case 0x6261:  // MT6261
                case 0x2523:  // MT2523
                case 0x7682:  // MT7682
                case 0x7686:  // MT7686
                    // 16 位写入
                    return await Write16Async(0xA2050000, new ushort[] { 0x2200 }, ct);
                    
                default:
                    // 32 位写入
                    return await Write32Async(wdtAddr, new uint[] { wdtValue }, ct);
            }
        }

        /// <summary>
        /// 写入 16 位数据
        /// </summary>
        public async Task<bool> Write16Async(uint address, ushort[] values, CancellationToken ct = default)
        {
            if (!await EchoAsync(BromCommands.CMD_WRITE16, ct))
                return false;

            // 发送地址
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE(address), ct))
                return false;

            // 发送数量
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)values.Length), ct))
                return false;

            // 读取状态
            var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            if (statusResp == null) return false;

            ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
            if (!BromErrorHelper.IsSuccess(status))
                return false;

            // 写入数据
            foreach (ushort value in values)
            {
                if (!await EchoAsync(MtkDataPacker.PackUInt16BE(value), ct))
                    return false;
            }

            // 读取最终状态
            var status2Resp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            return status2Resp != null;
        }

        #endregion

        #region Exploit Payload 操作

        /// <summary>
        /// 发送 BROM Exploit Payload (使用 SEND_CERT 命令)
        /// 参考: SP Flash Tool 和 mtkclient 的 send_root_cert
        /// </summary>
        /// <param name="payload">Exploit payload 数据</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SendExploitPayloadAsync(byte[] payload, CancellationToken ct = default)
        {
            try
            {
                _log($"[MTK] 发送 Exploit Payload, 大小: {payload.Length} 字节 (0x{payload.Length:X})");

                // 1. 发送 SEND_CERT 命令 (0xE0)
                if (!await EchoAsync(BromCommands.CMD_SEND_CERT, ct))
                {
                    _log("[MTK] SEND_CERT 命令回显失败");
                    return false;
                }

                // 2. 发送 payload 长度 (大端序)
                if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)payload.Length), ct))
                {
                    _log("[MTK] Payload 长度回显失败");
                    return false;
                }

                // 3. 读取状态
                var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (statusResp == null)
                {
                    _log("[MTK] 未能读取 SEND_CERT 状态");
                    return false;
                }

                ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
                _log($"[MTK] SEND_CERT 状态: 0x{status:X4}");

                if (status > 0xFF)
                {
                    _log($"[MTK] SEND_CERT 被拒绝: {BromErrorHelper.GetErrorMessage(status)}");
                    return false;
                }

                // 4. 计算校验和
                ushort checksum = 0;
                foreach (byte b in payload)
                {
                    checksum += b;
                }

                // 5. 上传 payload 数据 (参考 mtkclient upload_data)
                int chunkSize = 0x400;  // 1KB chunks
                int pos = 0;
                int flushCounter = 0;

                await _portLock.WaitAsync(ct);
                try
                {
                    while (pos < payload.Length)
                    {
                        int remaining = payload.Length - pos;
                        int size = Math.Min(remaining, chunkSize);

                        _port.Write(payload, pos, size);
                        pos += size;
                        flushCounter += size;

                        // 每 0x2000 字节刷新一次
                        if (flushCounter >= 0x2000)
                        {
                            _port.Write(new byte[0], 0, 0);  // Empty packet flush
                            flushCounter = 0;
                        }
                    }

                    // 6. 发送空包作为结束标志
                    _port.Write(new byte[0], 0, 0);
                }
                finally
                {
                    _portLock.Release();
                }

                // 7. 等待一段时间 (Mtk 参考: 10ms 足够)
                await Task.Delay(10, ct);

                // 8. 读取校验和响应
                var checksumResp = await ReadBytesAsync(2, 2000, ct);
                if (checksumResp != null)
                {
                    ushort receivedChecksum = MtkDataPacker.UnpackUInt16BE(checksumResp, 0);
                    _log($"[MTK] Payload 校验和: 收到 0x{receivedChecksum:X4}, 期望 0x{checksum:X4}");
                }

                // 9. 读取最终状态
                var finalStatusResp = await ReadBytesAsync(2, 2000, ct);
                if (finalStatusResp != null)
                {
                    ushort finalStatus = MtkDataPacker.UnpackUInt16BE(finalStatusResp, 0);
                    _log($"[MTK] Payload 上传状态: 0x{finalStatus:X4}");

                    if (finalStatus <= 0xFF)
                    {
                        _log("[MTK] ✓ Exploit Payload 上传成功");
                        return true;
                    }
                    else
                    {
                        _log($"[MTK] Payload 上传失败: {BromErrorHelper.GetErrorMessage(finalStatus)}");
                    }
                }

                return true;  // 有些设备可能不返回状态但仍然执行了 payload
            }
            catch (Exception ex)
            {
                _log($"[MTK] SendExploitPayloadAsync 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载并发送 BROM Exploit Payload
        /// </summary>
        /// <param name="payloadPath">Payload 文件路径</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> SendExploitPayloadFromFileAsync(string payloadPath, CancellationToken ct = default)
        {
            try
            {
                if (!System.IO.File.Exists(payloadPath))
                {
                    _log($"[MTK] Payload 文件不存在: {payloadPath}");
                    return false;
                }

                byte[] payload = System.IO.File.ReadAllBytes(payloadPath);
                _log($"[MTK] 加载 Payload: {System.IO.Path.GetFileName(payloadPath)}, {payload.Length} 字节");

                return await SendExploitPayloadAsync(payload, ct);
            }
            catch (Exception ex)
            {
                _log($"[MTK] 加载 Payload 失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region DA 操作

        /// <summary>
        /// 发送 DA (Download Agent)
        /// </summary>
        public async Task<bool> SendDaAsync(uint address, byte[] data, int sigLen = 0, CancellationToken ct = default)
        {
            try
            {
                _log($"[MTK] 发送 DA 到地址 0x{address:X8}, 大小 {data.Length} 字节, 签名长度: 0x{sigLen:X}");

                // 准备数据和校验和
                byte[] dataWithoutSig = data;
                byte[] signature = null;
                if (sigLen > 0)
                {
                    if (data.Length < sigLen)
                    {
                        _log($"[MTK] 错误: 数据长度 {data.Length} 小于签名长度 {sigLen}");
                        return false;
                    }
                    dataWithoutSig = new byte[data.Length - sigLen];
                    Array.Copy(data, 0, dataWithoutSig, 0, data.Length - sigLen);
                    signature = new byte[sigLen];
                    Array.Copy(data, data.Length - sigLen, signature, 0, sigLen);
                    _log($"[MTK] 数据分割: 主体 {dataWithoutSig.Length} 字节, 签名 {signature.Length} 字节");
                }
                var (checksum, processedData) = MtkChecksum.PrepareData(
                    dataWithoutSig,
                    signature,
                    data.Length - sigLen
                );
                _log($"[MTK] 处理后数据: {processedData.Length} 字节, XOR校验和: 0x{checksum:X4}");

                // 发送 SEND_DA 命令 (参考 MtkPreloader.cs)
                _log("[MTK] 发送 SEND_DA 命令 (0xD7)...");
                
                // 清空缓冲区中的残留数据
                if (_port.BytesToRead > 0)
                {
                    byte[] junk = new byte[_port.BytesToRead];
                    _port.Read(junk, 0, junk.Length);
                    _log($"[MTK] 清空缓冲区: {junk.Length} 字节 ({BitConverter.ToString(junk)})");
                }
                
                // 发送命令并检查响应
                await WriteBytesAsync(new byte[] { BromCommands.CMD_SEND_DA }, ct);
                var cmdResp = await ReadBytesAsync(1, DEFAULT_TIMEOUT_MS, ct);
                if (cmdResp == null || cmdResp.Length == 0)
                {
                    _log("[MTK] 无命令响应");
                    return false;
                }
                
                bool useAlternativeProtocol = false;
                
                if (cmdResp[0] == BromCommands.CMD_SEND_DA)
                {
                    // 标准回显，继续正常流程
                    _log("[MTK] ✓ SEND_DA 命令已确认 (标准回显)");
                }
                else if (cmdResp[0] == 0xE7)
                {
                    // 可能是状态响应 (0xE7 可能表示命令已接受)
                    _log("[MTK] 收到响应 0xE7，检查后续状态...");
                    
                    // 读取状态码
                    var statusData1 = await ReadBytesAsync(2, 500, ct);
                    if (statusData1 != null && statusData1.Length >= 2)
                    {
                        ushort respStatus1 = MtkDataPacker.UnpackUInt16BE(statusData1, 0);
                        _log($"[MTK] 状态码: 0x{respStatus1:X4}");
                        
                        if (respStatus1 == 0x0000)
                        {
                            // 状态 0x0000 表示命令接受，尝试替代协议
                            _log("[MTK] 状态 0x0000，尝试替代协议流程...");
                            useAlternativeProtocol = true;
                        }
                        else
                        {
                            _log($"[MTK] 命令被拒绝，状态: 0x{respStatus1:X4}");
                            LastUploadStatus = respStatus1;
                            return false;
                        }
                    }
                }
                else if (cmdResp[0] == 0x00)
                {
                    // 设备可能直接返回状态
                    var statusData2 = await ReadBytesAsync(1, 500, ct);
                    if (statusData2 != null && statusData2.Length >= 1)
                    {
                        ushort respStatus2 = (ushort)((cmdResp[0] << 8) | statusData2[0]);
                        _log($"[MTK] 设备返回状态: 0x{respStatus2:X4}");
                        LastUploadStatus = respStatus2;
                        
                        if (respStatus2 == 0x0000)
                        {
                            _log("[MTK] 状态 0x0000，尝试替代协议流程...");
                            useAlternativeProtocol = true;
                        }
                        else
                        {
                            _log($"[MTK] 命令失败: 0x{respStatus2:X4}");
                            return false;
                        }
                    }
                    return false;
                }
                else
                {
                    _log($"[MTK] 未知响应: 0x{cmdResp[0]:X2}");
                    // 尝试读取更多数据进行诊断
                    var moreData = await ReadBytesAsync(4, 200, ct);
                    if (moreData != null && moreData.Length > 0)
                    {
                        _log($"[MTK] 额外数据: {BitConverter.ToString(moreData)}");
                    }
                    return false;
                }
                
                if (useAlternativeProtocol)
                {
                    // 替代协议: 设备可能已经在等待数据
                    // 尝试直接发送参数 (不期待回显)
                    _log("[MTK] 使用替代协议: 直接发送参数");
                    
                    // 发送地址
                    await WriteBytesAsync(MtkDataPacker.PackUInt32BE(address), ct);
                    await Task.Delay(10, ct);
                    
                    // 发送大小
                    await WriteBytesAsync(MtkDataPacker.PackUInt32BE((uint)processedData.Length), ct);
                    await Task.Delay(10, ct);
                    
                    // 发送签名长度
                    await WriteBytesAsync(MtkDataPacker.PackUInt32BE((uint)sigLen), ct);
                    await Task.Delay(10, ct);
                    
                    // 读取状态
                    var altStatus = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
                    if (altStatus != null && altStatus.Length >= 2)
                    {
                        ushort altRespStatus = MtkDataPacker.UnpackUInt16BE(altStatus, 0);
                        _log($"[MTK] 替代协议状态: 0x{altRespStatus:X4}");
                        
                        if (altRespStatus != 0x0000)
                        {
                            LastUploadStatus = altRespStatus;
                            _log($"[MTK] 替代协议失败: 0x{altRespStatus:X4}");
                            return false;
                        }
                    }
                    
                    // 发送数据
                    _log($"[MTK] 发送 DA 数据 ({processedData.Length} 字节)...");
                    await WriteBytesAsync(processedData, ct);
                    await Task.Delay(100, ct);
                    
                    // 读取校验和/状态
                    var finalResp = await ReadBytesAsync(4, DEFAULT_TIMEOUT_MS, ct);
                    if (finalResp != null && finalResp.Length >= 2)
                    {
                        ushort recvChecksum = MtkDataPacker.UnpackUInt16BE(finalResp, 0);
                        _log($"[MTK] 设备校验和: 0x{recvChecksum:X4}, 期望: 0x{checksum:X4}");
                        
                        if (finalResp.Length >= 4)
                        {
                            ushort finalStatus = MtkDataPacker.UnpackUInt16BE(finalResp, 2);
                            _log($"[MTK] 最终状态: 0x{finalStatus:X4}");
                            LastUploadStatus = finalStatus;
                            return finalStatus == 0x0000;
                        }
                        return recvChecksum == checksum;
                    }
                    
                    _log("[MTK] 替代协议: 无最终响应");
                    return false;
                }

            // 发送地址并等待回显
            _log($"[MTK] 发送地址: 0x{address:X8}");
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE(address), ct))
            {
                _log("[MTK] 发送地址失败");
                return false;
            }

            // 发送大小并等待回显
            _log($"[MTK] 发送大小: {processedData.Length} 字节");
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)processedData.Length), ct))
            {
                _log("[MTK] 发送大小失败");
                return false;
            }

            // 发送签名长度并等待回显
            _log($"[MTK] 发送签名长度: 0x{sigLen:X} ({sigLen} 字节)");
            if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)sigLen), ct))
            {
                _log("[MTK] 发送签名长度失败");
                return false;
            }

            // 读取状态 (2字节)
            _log("[MTK] 等待设备响应状态...");
            var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
            if (statusResp == null)
            {
                _log("[MTK] 读取状态失败 (超时或无响应)");
                return false;
            }

            ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
            _log($"[MTK] SEND_DA 状态: 0x{status:X4}");
            
            LastUploadStatus = status;  // 保存状态供上层使用

            // 检查状态码 0x0010 或 0x0011 (Preloader 模式 Auth 需求)
            if (status == (ushort)BromStatus.AuthRequired || status == (ushort)BromStatus.PreloaderAuth)
            {
                _log($"[MTK] ⚠ Preloader 模式需要 AUTH (状态: 0x{status:X4})");
                _log("[MTK] 设备在 Preloader 模式下启用了 DAA 保护");
                _log("[MTK] 需要官方签名的 DA 或使用 DA2 级别漏洞 (ALLINONE-SIGNATURE)");
                LastUploadStatus = status;
                return false;
            }

            // 检查是否需要 SLA 认证 (状态码 0x1D0D)
            if (status == (ushort)BromStatus.SlaRequired)
            {
                _log("[MTK] 需要 SLA 认证...");
                
                // 执行 SLA 认证
                var slaAuth = new MtkSlaAuth(msg => _log(msg));
                bool authSuccess = await slaAuth.AuthenticateAsync(
                    async (authData, len, token) => 
                    {
                        _port.Write(authData, 0, len);
                        return true;
                    },
                    async (count, timeout, token) => await ReadBytesInternalAsync(count, timeout, token),
                    HwCode,
                    ct
                );
                
                if (!authSuccess)
                {
                    _log("[MTK] SLA 认证失败");
                    return false;
                }
                
                _log("[MTK] ✓ SLA 认证成功");
                status = 0;  // 认证成功后重置状态
            }

            // 状态码检查 (mtkclient: 0 <= status <= 0xFF 表示成功)
            if (status > 0xFF)
            {
                _log($"[MTK] SEND_DA 状态错误: 0x{status:X4} ({BromErrorHelper.GetErrorMessage(status)})");
                return false;
            }

            _log($"[MTK] ✓ SEND_DA 状态正常: 0x{status:X4}");
            _log($"[MTK] 准备上传数据: {processedData.Length} 字节, 校验和: 0x{checksum:X4}");

            // 上传数据
            _log("[MTK] 开始调用 UploadDataAsync...");
            bool uploadResult = false;
            try
            {
                uploadResult = await UploadDataAsync(processedData, checksum, ct);
            }
            catch (Exception uploadEx)
            {
                _log($"[MTK] UploadDataAsync 异常: {uploadEx.Message}");
                return false;
            }
            _log($"[MTK] 上传数据结果: {uploadResult}");
            
            if (!uploadResult)
            {
                _log("[MTK] 数据上传失败");
                return false;
            }

            _log("[MTK] ✓ DA 发送成功");
            return true;
            }
            catch (Exception ex)
            {
                _log($"[MTK] SendDaAsync 异常: {ex.Message}");
                _log($"[MTK] 堆栈: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 跳转到 DA 执行
        /// 参考 mtkclient: jump_da()
        /// </summary>
        public async Task<bool> JumpDaAsync(uint address, CancellationToken ct = default)
        {
            _log($"[MTK] 跳转到 DA 地址 0x{address:X8}");

            try
            {
                // 1. 发送 JUMP_DA 命令并等待回显
                if (!await EchoAsync(BromCommands.CMD_JUMP_DA, ct))
                {
                    _log("[MTK] JUMP_DA 命令回显失败");
                    return false;
                }

                // 2. 发送地址 (mtkclient: usbwrite, 不是 echo)
                await _portLock.WaitAsync(ct);
                try
                {
                    _port.Write(MtkDataPacker.PackUInt32BE(address), 0, 4);
                }
                finally
                {
                    _portLock.Release();
                }

                // 3. 读取地址回显
                var addrResp = await ReadBytesAsync(4, DEFAULT_TIMEOUT_MS, ct);
                if (addrResp == null)
                {
                    _log("[MTK] 读取地址回显超时");
                    return false;
                }

                uint respAddr = MtkDataPacker.UnpackUInt32BE(addrResp, 0);
                if (respAddr != address)
                {
                    _log($"[MTK] 地址不匹配: 期望 0x{address:X8}, 收到 0x{respAddr:X8}");
                    return false;
                }

                // 4. 读取状态 (mtkclient: 读取状态后立即 sleep，不处理状态后的数据)
                var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (statusResp == null)
                {
                    _log("[MTK] 读取状态超时");
                    return false;
                }

                ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
                _log($"[MTK] JUMP_DA 状态: 0x{status:X4}");

                // mtkclient: if status == 0: return True
                if (status != 0)
                {
                    _log($"[MTK] JUMP_DA 状态错误: 0x{status:X4} ({BromErrorHelper.GetErrorMessage(status)})");
                    return false;
                }

                // 5. 等待 DA 启动 (mtkclient: time.sleep(0.1))
                await Task.Delay(100, ct);

                _log("[MTK] ✓ JUMP_DA 成功");
                State = MtkDeviceState.Da1Loaded;
                return true;
            }
            catch (Exception ex)
            {
                _log($"[MTK] JumpDaAsync 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试检测 DA 是否已就绪 (通过读取 sync 信号)
        /// </summary>
        public async Task<bool> TryDetectDaReadyAsync(CancellationToken ct = default)
        {
            try
            {
                await _portLock.WaitAsync(ct);
                try
                {
                    // 清空接收缓冲区
                    _port.DiscardInBuffer();
                    
                    // 尝试读取 DA sync 信号
                    // DA 启动后通常会发送 "SYNC" (0x434E5953) 或特定字节序列
                    byte[] buffer = new byte[64];
                    int totalRead = 0;
                    
                    // 等待最多 2 秒
                    var timeout = DateTime.Now.AddMilliseconds(2000);
                    while (DateTime.Now < timeout && totalRead < buffer.Length)
                    {
                        if (ct.IsCancellationRequested)
                            return false;
                            
                        if (_port.BytesToRead > 0)
                        {
                            int read = _port.Read(buffer, totalRead, Math.Min(_port.BytesToRead, buffer.Length - totalRead));
                            totalRead += read;
                            
                            // 检查是否收到 DA 就绪信号
                            // V6 DA 通常发送 "SYNC" 或 0xC0
                            if (totalRead >= 4)
                            {
                                // 检查 "SYNC" 魔数
                                if (buffer[0] == 'S' && buffer[1] == 'Y' && buffer[2] == 'N' && buffer[3] == 'C')
                                {
                                    _log("[MTK] 检测到 DA SYNC 信号");
                                    State = MtkDeviceState.Da1Loaded;
                                    return true;
                                }
                                
                                // 检查反向 SYNC
                                uint sync = (uint)(buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]);
                                if (sync == 0x434E5953)  // "CNYS" (little endian SYNC)
                                {
                                    _log("[MTK] 检测到 DA SYNC 信号 (LE)");
                                    State = MtkDeviceState.Da1Loaded;
                                    return true;
                                }
                            }
                            
                            // 检查单字节就绪信号
                            if (buffer[0] == 0xC0)
                            {
                                _log("[MTK] 检测到 DA 就绪信号 (0xC0)");
                                State = MtkDeviceState.Da1Loaded;
                                return true;
                            }
                        }
                        else
                        {
                            await Task.Delay(50, ct);
                        }
                    }
                    
                    if (totalRead > 0)
                    {
                        _log($"[MTK] 收到 {totalRead} 字节: {BitConverter.ToString(buffer, 0, Math.Min(totalRead, 16))}");
                    }
                    
                    return false;
                }
                finally
                {
                    _portLock.Release();
                }
            }
            catch (Exception ex)
            {
                _log($"[MTK] 检测 DA 就绪异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试发送 DA 同步命令
        /// DA 协议使用 0xEFEEEEFE 魔数
        /// </summary>
        public async Task<bool> TrySendDaSyncAsync(CancellationToken ct = default)
        {
            try
            {
                await _portLock.WaitAsync(ct);
                try
                {
                    _log("[MTK] 发送 DA 同步命令...");
                    
                    // DA 命令格式: EFEEEEFE + cmd(4B) + length(4B) + payload
                    // SYNC 命令: cmd=0x01, length=4, payload="SYNC"
                    byte[] syncCmd = new byte[]
                    {
                        0xEF, 0xEE, 0xEE, 0xFE,  // Magic
                        0x01, 0x00, 0x00, 0x00,  // CMD = 1 (SYNC)
                        0x04, 0x00, 0x00, 0x00   // Length = 4
                    };
                    byte[] syncPayload = System.Text.Encoding.ASCII.GetBytes("SYNC");
                    
                    // 发送命令头
                    _port.Write(syncCmd, 0, syncCmd.Length);
                    
                    // 发送 SYNC 载荷
                    _port.Write(syncPayload, 0, syncPayload.Length);
                    
                    // 等待响应
                    await Task.Delay(200, ct);
                    
                    // 读取响应
                    byte[] buffer = new byte[32];
                    int totalRead = 0;
                    
                    var timeout = DateTime.Now.AddMilliseconds(2000);
                    while (DateTime.Now < timeout && totalRead < buffer.Length)
                    {
                        if (_port.BytesToRead > 0)
                        {
                            int read = _port.Read(buffer, totalRead, Math.Min(_port.BytesToRead, buffer.Length - totalRead));
                            totalRead += read;
                            
                            // 检查是否收到 DA 响应
                            if (totalRead >= 4)
                            {
                                // 检查魔数
                                if (buffer[0] == 0xEF && buffer[1] == 0xEE)
                                {
                                    _log($"[MTK] 收到 DA 响应: {BitConverter.ToString(buffer, 0, Math.Min(totalRead, 12))}");
                                    State = MtkDeviceState.Da1Loaded;
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            await Task.Delay(50, ct);
                        }
                    }
                    
                    if (totalRead > 0)
                    {
                        _log($"[MTK] 收到响应但格式不匹配: {BitConverter.ToString(buffer, 0, totalRead)}");
                    }
                    else
                    {
                        _log("[MTK] DA 同步无响应");
                    }
                    
                    return false;
                }
                finally
                {
                    _portLock.Release();
                }
            }
            catch (Exception ex)
            {
                _log($"[MTK] DA 同步异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 上传数据 (带线程安全保护)
        /// </summary>
        private async Task<bool> UploadDataAsync(byte[] data, ushort expectedChecksum, CancellationToken ct = default)
        {
            _log($"[MTK] 开始上传数据: {data.Length} 字节, 预期校验和: 0x{expectedChecksum:X4}");
            
            await _portLock.WaitAsync(ct);
            try
            {
                int bytesWritten = 0;
                // mtkclient 使用 0x400 (1KB) 作为最大块大小
                int maxPacketSize = 0x400;

                while (bytesWritten < data.Length)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _log("[MTK] 数据上传被取消");
                        return false;
                    }

                    int chunkSize = Math.Min(maxPacketSize, data.Length - bytesWritten);
                    _port.Write(data, bytesWritten, chunkSize);
                    bytesWritten += chunkSize;

                    // mtkclient: 每 0x2000 字节刷新一次
                    if (bytesWritten % 0x2000 == 0)
                    {
                        _port.Write(new byte[0], 0, 0);  // 刷新
                    }

                    // 更新进度 (每 64KB 更新一次，避免过于频繁)
                    if (bytesWritten % 0x10000 == 0 || bytesWritten == data.Length)
                    {
                        double progress = (double)bytesWritten * 100 / data.Length;
                        _progressCallback?.Invoke(progress);
                    }
                }

                _log($"[MTK] 数据发送完成: {bytesWritten} 字节");
                
                // mtkclient: 发送完成后发送空字节并等待
                // 注意: Mtk 参考实现使用 10ms，mtkclient 使用 120ms
                // 这里使用 10ms 以提高速度，如果有问题可以调高
                _port.Write(new byte[0], 0, 0);
                await Task.Delay(10, ct);

                // 读取校验和 (2字节, Big-Endian)
                var checksumResp = await ReadBytesInternalAsync(2, DEFAULT_TIMEOUT_MS * 2, ct);
                if (checksumResp == null || checksumResp.Length < 2)
                {
                    _log($"[MTK] 读取校验和失败 (收到: {checksumResp?.Length ?? 0} 字节)");
                    return false;
                }
                
                ushort receivedChecksum = (ushort)((checksumResp[0] << 8) | checksumResp[1]);
                _log($"[MTK] 收到校验和: 0x{receivedChecksum:X4}, 期望: 0x{expectedChecksum:X4}");
                
                if (receivedChecksum != expectedChecksum && receivedChecksum != 0)
                {
                    _log($"[MTK] 警告: 校验和不匹配");
                }

                // 读取最终状态 (2字节)
                var statusResp = await ReadBytesInternalAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (statusResp == null || statusResp.Length < 2)
                {
                    _log($"[MTK] 读取状态失败 (收到: {statusResp?.Length ?? 0} 字节)");
                    return false;
                }
                
                ushort status = (ushort)((statusResp[0] << 8) | statusResp[1]);
                _log($"[MTK] 上传状态: 0x{status:X4}");
                
                // 保存上传状态供后续使用
                LastUploadStatus = status;

                // 使用改进的状态检查
                if (!BromErrorHelper.IsSuccess(status))
                {
                    _log($"[MTK] 上传状态错误: 0x{status:X4} ({BromErrorHelper.GetErrorMessage(status)})");
                    return false;
                }
                
                // 特殊处理: 0x7017/0x7015 表示 DAA 安全保护
                if (status == 0x7017 || status == 0x7015)
                {
                    _log($"[MTK] 数据传输完成 (状态 0x{status:X4})");
                    _log("[MTK] ⚠ DAA 安全保护触发 - 设备可能重新枚举");
                }
                else
                {
                    _log("[MTK] ✓ 数据上传成功");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _log($"[MTK] 数据上传异常: {ex.Message}");
                return false;
            }
            finally
            {
                _portLock.Release();
            }
        }

        #endregion

        #region EMI 配置

        /// <summary>
        /// 发送 EMI 配置 (用于 BROM 模式下的 DRAM 初始化)
        /// </summary>
        public async Task<bool> SendEmiConfigAsync(byte[] emiConfig, CancellationToken ct = default)
        {
            if (emiConfig == null || emiConfig.Length == 0)
            {
                _log("[MTK] EMI 配置数据为空");
                return false;
            }

            _log($"[MTK] 发送 EMI 配置: {emiConfig.Length} 字节");

            try
            {
                // 发送 SEND_ENV_PREPARE 命令 (0xD9)
                if (!await EchoAsync(BromCommands.CMD_SEND_ENV_PREPARE, ct))
                {
                    _log("[MTK] SEND_ENV_PREPARE 命令失败");
                    return false;
                }

                // 发送 EMI 配置长度
                if (!await EchoAsync(MtkDataPacker.PackUInt32BE((uint)emiConfig.Length), ct))
                {
                    _log("[MTK] 发送 EMI 配置长度失败");
                    return false;
                }

                // 读取状态
                var statusResp = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (statusResp == null)
                {
                    _log("[MTK] 读取状态失败");
                    return false;
                }

                ushort status = MtkDataPacker.UnpackUInt16BE(statusResp, 0);
                if (!BromErrorHelper.IsSuccess(status))
                {
                    _log($"[MTK] EMI 配置状态错误: 0x{status:X4} ({BromErrorHelper.GetErrorMessage(status)})");
                    return false;
                }

                // 发送 EMI 配置数据
                await _portLock.WaitAsync(ct);
                try
                {
                    _port.Write(emiConfig, 0, emiConfig.Length);
                    await Task.Delay(50, ct);
                }
                finally
                {
                    _portLock.Release();
                }

                // 读取最终状态
                var finalStatus = await ReadBytesAsync(2, DEFAULT_TIMEOUT_MS, ct);
                if (finalStatus == null)
                {
                    _log("[MTK] 读取最终状态失败");
                    return false;
                }

                ushort finalStatusCode = MtkDataPacker.UnpackUInt16BE(finalStatus, 0);
                if (!BromErrorHelper.IsSuccess(finalStatusCode))
                {
                    _log($"[MTK] EMI 配置最终状态错误: 0x{finalStatusCode:X4}");
                    return false;
                }

                _log("[MTK] ✓ EMI 配置发送成功");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[MTK] 发送 EMI 配置异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 发送命令并回显
        /// </summary>
        private async Task<bool> EchoAsync(byte cmd, CancellationToken ct = default)
        {
            return await EchoAsync(new byte[] { cmd }, ct);
        }

        /// <summary>
        /// 发送数据并回显 (带线程安全)
        /// </summary>
        private async Task<bool> EchoAsync(byte[] data, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(data, 0, data.Length);
                
                var response = await ReadBytesInternalAsync(data.Length, DEFAULT_TIMEOUT_MS, ct);
                if (response == null)
                {
                    _logDetail("[MTK] Echo: 读取响应超时");
                    return false;
                }

                // 比较回显
                for (int i = 0; i < data.Length; i++)
                {
                    if (response[i] != data[i])
                    {
                        _logDetail($"[MTK] Echo不匹配: 位置{i}, 期望0x{data[i]:X2}, 收到0x{response[i]:X2}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logDetail($"[MTK] Echo异常: {ex.Message}");
                return false;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 发送命令并回显 (带诊断信息)
        /// 注意: Preloader 模式可能不回显某些命令，直接返回状态码
        /// </summary>
        private async Task<bool> EchoAsyncWithDiag(byte cmd, string cmdName, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(new byte[] { cmd }, 0, 1);
                
                var response = await ReadBytesInternalAsync(1, DEFAULT_TIMEOUT_MS, ct);
                if (response == null)
                {
                    _log($"[MTK] {cmdName} 命令失败: 无响应");
                    return false;
                }

                if (response[0] == cmd)
                {
                    // 正常回显
                    return true;
                }

                // 检查是否是状态码响应 (设备没有回显命令)
                if (response[0] == 0x00)
                {
                    // 读取第二个字节看是否是状态码
                    var extra = await ReadBytesInternalAsync(1, 100, ct);
                    if (extra != null && extra.Length > 0)
                    {
                        ushort status = (ushort)((response[0] << 8) | extra[0]);
                        _log($"[MTK] {cmdName} 命令: 设备返回状态 0x{status:X4} (无回显)");
                        
                        if (status == (ushort)BromStatus.SlaRequired)
                            _log("[MTK] 设备需要 SLA 认证");
                        else if (status == (ushort)BromStatus.AuthRequired || status == (ushort)BromStatus.PreloaderAuth)
                            _log("[MTK] Preloader 需要 AUTH");
                    }
                    else
                    {
                        _log($"[MTK] {cmdName} 命令被拒绝 (可能需要 DAA)");
                    }
                }
                else
                {
                    _log($"[MTK] {cmdName} 回显不匹配: 期望 0x{cmd:X2}, 收到 0x{response[0]:X2}");
                }
                return false;
            }
            catch (Exception ex)
            {
                _log($"[MTK] {cmdName} 命令异常: {ex.Message}");
                return false;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 发送命令并读取响应 (带线程安全)
        /// </summary>
        private async Task<byte[]> SendCmdAsync(byte cmd, int responseLen, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(new byte[] { cmd }, 0, 1);
                return await ReadBytesInternalAsync(responseLen, DEFAULT_TIMEOUT_MS, ct);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 读取指定数量的字节 (公开方法，带线程安全)
        /// </summary>
        public async Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct = default)
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
        /// 读取指定数量的字节 (内部方法，不加锁)
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
        /// 获取端口锁 (供外部使用，如 XmlDaClient)
        /// </summary>
        public SemaphoreSlim GetPortLock() => _portLock;

        #endregion

        #region IBromClient 接口实现

        /// <summary>
        /// 发送boot_to命令加载代码到指定地址 (用于DA Extensions)
        /// </summary>
        public async Task SendBootTo(uint address, byte[] data)
        {
            if (_logger != null)
            {
                _logger.Info($"boot_to: 地址=0x{address:X8}, 大小={data?.Length ?? 0}", LogCategory.Protocol);
            }
            else
            {
                _log($"[MTK] boot_to: 0x{address:X8} ({data?.Length ?? 0} 字节)");
            }

            // TODO: 实际的boot_to命令实现
            // 这需要在DA模式下执行，不是BROM命令
            throw new NotImplementedException("boot_to命令需要在DA模式下实现");
        }

        /// <summary>
        /// 发送DA命令
        /// </summary>
        public async Task SendDaCommand(uint command, byte[] data = null)
        {
            if (_logger != null)
            {
                _logger.LogCommand($"DA命令", command, LogCategory.Da);
            }
            
            // TODO: 实际的DA命令发送逻辑
            throw new NotImplementedException("DA命令发送需要在DA模式下实现");
        }

        /// <summary>
        /// 接收DA响应
        /// </summary>
        public async Task<byte[]> ReceiveDaResponse(int length)
        {
            // TODO: 实际的DA响应接收逻辑
            throw new NotImplementedException("DA响应接收需要在DA模式下实现");
        }

        #endregion

        #region Kamakiri2 辅助方法 (公开给 exploit 使用)

        /// <summary>
        /// 公开的单字节 Echo (用于 Kamakiri2 exploit)
        /// </summary>
        public async Task<bool> EchoByteAsync(byte cmd, CancellationToken ct = default)
        {
            return await EchoAsync(cmd, ct);
        }

        /// <summary>
        /// 公开的多字节 Echo (用于 Kamakiri2 exploit)
        /// </summary>
        public async Task<bool> EchoBytesAsync(byte[] data, CancellationToken ct = default)
        {
            return await EchoAsync(data, ct);
        }

        // 注意: ReadBytesAsync 已在类中定义，不需要重复定义

        /// <summary>
        /// 公开的写入字节方法 (用于 Kamakiri2 exploit)
        /// </summary>
        public async Task WriteBytesAsync(byte[] data, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(data, 0, data.Length);
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 清空串口缓冲区 (用于 Kamakiri2 exploit)
        /// </summary>
        public void DiscardBuffers()
        {
            try
            {
                if (_port != null && _port.IsOpen)
                {
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BROM] DiscardBuffers 异常: {ex.Message}"); }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _port?.Dispose();
                _portLock?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
