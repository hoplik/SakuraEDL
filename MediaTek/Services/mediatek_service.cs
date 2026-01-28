// ============================================================================
// SakuraEDL - MediaTek 刷机服务
// MediaTek Flashing Service
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Database;
using SakuraEDL.MediaTek.Exploit;
using SakuraEDL.MediaTek.Models;
using SakuraEDL.MediaTek.Protocol;
using DaEntry = SakuraEDL.MediaTek.Models.DaEntry;

namespace SakuraEDL.MediaTek.Services
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public enum MtkProtocolType
    {
        Auto,       // 自动选择
        Xml,        // XML V6 协议
        XFlash      // XFlash 二进制协议
    }

    /// <summary>
    /// MediaTek 刷机服务 - 主服务类
    /// </summary>
    public class MediatekService : IDisposable
    {
        private BromClient _bromClient;
        private XmlDaClient _xmlClient;
        private XFlashClient _xflashClient;
        private DaLoader _daLoader;
        private CancellationTokenSource _cts;

        // 协议类型
        private MtkProtocolType _protocolType = MtkProtocolType.Auto;
        private bool _useXFlash = false;

        // 事件
        public event Action<string, Color> OnLog;
        public event Action<int, int> OnProgress;
        public event Action<MtkDeviceState> OnStateChanged;
        public event Action<MtkDeviceInfo> OnDeviceConnected;
        public event Action<MtkDeviceInfo> OnDeviceDisconnected;

        // 属性
        public bool IsConnected => _bromClient?.IsConnected ?? false;
        public bool IsBromMode => _bromClient?.IsBromMode ?? false;
        public MtkDeviceState State => _bromClient?.State ?? MtkDeviceState.Disconnected;
        public MtkChipInfo ChipInfo => _bromClient?.ChipInfo;
        public MtkProtocolType Protocol => _protocolType;
        public bool IsXFlashMode => _useXFlash;

        // 当前设备信息
        public MtkDeviceInfo CurrentDevice { get; private set; }

        // DA 文件路径
        public string DaFilePath { get; private set; }

        // 自定义 DA 配置
        public string CustomDa1Path { get; private set; }
        public string CustomDa2Path { get; private set; }

        public MediatekService()
        {
            _bromClient = new BromClient(
                msg => Log(msg, Color.White),
                msg => Log(msg, Color.Gray),
                progress => OnProgress?.Invoke((int)progress, 100)
            );

            _daLoader = new DaLoader(
                _bromClient,
                msg => Log(msg, Color.White),
                progress => OnProgress?.Invoke((int)progress, 100)
            );
        }

        #region 设备连接

        /// <summary>
        /// 连接设备
        /// </summary>
        public async Task<bool> ConnectAsync(string comPort, int baudRate = 115200, CancellationToken ct = default)
        {
            try
            {
                Log($"[MTK] 连接设备: {comPort}", Color.Cyan);

                if (!await _bromClient.ConnectAsync(comPort, baudRate, ct))
                {
                    Log("[MTK] 串口打开失败", Color.Red);
                    return false;
                }

                // 执行握手
                if (!await _bromClient.HandshakeAsync(100, ct))
                {
                    Log("[MTK] BROM 握手失败", Color.Red);
                    return false;
                }

                // 初始化设备
                if (!await _bromClient.InitializeAsync(false, ct))
                {
                    Log("[MTK] 设备初始化失败", Color.Red);
                    return false;
                }

                // 创建设备信息
                CurrentDevice = new MtkDeviceInfo
                {
                    ComPort = comPort,
                    IsDownloadMode = true,
                    ChipInfo = _bromClient.ChipInfo,
                    MeId = _bromClient.MeId,
                    SocId = _bromClient.SocId
                };

                Log($"[MTK] ✓ 连接成功: {_bromClient.ChipInfo.GetChipName()}", Color.Green);
                
                // 检查 DAA 状态并提示用户
                bool daaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.DaaEnabled);
                bool slaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SlaEnabled);
                bool sbcEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SbcEnabled);
                
                if (daaEnabled)
                {
                    Log("[MTK] ⚠ 警告: 设备启用了 DAA (Download Agent Authentication)", Color.Orange);
                    Log("[MTK] ⚠ 需要使用官方签名的 DA 或通过漏洞绕过", Color.Orange);
                }
                if (slaEnabled)
                {
                    Log("[MTK] ⚠ 警告: 设备启用了 SLA (Secure Link Auth)", Color.Yellow);
                }
                if (sbcEnabled && !_bromClient.IsBromMode)
                {
                    Log("[MTK] 提示: Preloader 模式 + SBC 启用，可能需要 Carbonara 漏洞", Color.Cyan);
                }
                
                OnDeviceConnected?.Invoke(CurrentDevice);
                OnStateChanged?.Invoke(_bromClient.State);

                return true;
            }
            catch (Exception ex)
            {
                Log($"[MTK] 连接异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _cts?.Cancel();
            _bromClient?.Disconnect();
            _xmlClient?.Dispose();
            _xmlClient = null;
            _xflashClient?.Dispose();
            _xflashClient = null;
            _useXFlash = false;

            if (CurrentDevice != null)
            {
                OnDeviceDisconnected?.Invoke(CurrentDevice);
                CurrentDevice = null;
            }

            Log("[MTK] 已断开连接", Color.Gray);
        }

        /// <summary>
        /// 设置协议类型
        /// </summary>
        public void SetProtocol(MtkProtocolType protocol)
        {
            _protocolType = protocol;
            Log($"[MTK] 协议设置: {protocol}", Color.Cyan);
        }

        /// <summary>
        /// 启用 CRC32 校验和 (仅 XFlash 协议)
        /// </summary>
        public async Task<bool> EnableChecksumAsync(CancellationToken ct = default)
        {
            if (_xflashClient != null)
            {
                return await _xflashClient.SetChecksumLevelAsync(ChecksumAlgorithm.CRC32, ct);
            }
            return false;
        }

        /// <summary>
        /// 初始化 XFlash 客户端
        /// </summary>
        private async Task InitializeXFlashClientAsync(CancellationToken ct = default)
        {
            // 根据协议设置决定是否使用 XFlash
            if (_protocolType == MtkProtocolType.Xml)
            {
                Log("[MTK] 使用 XML 协议 (用户指定)", Color.Gray);
                _useXFlash = false;
                return;
            }

            try
            {
                Log("[MTK] 初始化 XFlash 客户端...", Color.Gray);

                // 创建 XFlash 客户端 (共享端口锁)
                _xflashClient = new XFlashClient(
                    _bromClient.GetPort(),
                    msg => Log(msg, Color.White),
                    progress => OnProgress?.Invoke((int)progress, 100),
                    _bromClient.GetPortLock()
                );

                // 尝试检测存储类型
                if (await _xflashClient.DetectStorageAsync(ct))
                {
                    Log($"[MTK] ✓ XFlash 客户端就绪 (存储: {_xflashClient.Storage})", Color.Green);
                    
                    // 获取数据包长度
                    int packetLen = await _xflashClient.GetPacketLengthAsync(ct);
                    if (packetLen > 0)
                    {
                        Log($"[MTK] 数据包大小: {packetLen} bytes", Color.Gray);
                    }

                    // 如果是自动模式，启用 XFlash
                    if (_protocolType == MtkProtocolType.Auto || _protocolType == MtkProtocolType.XFlash)
                    {
                        _useXFlash = true;
                        Log("[MTK] ✓ 已启用 XFlash 二进制协议", Color.Cyan);
                    }
                }
                else
                {
                    Log("[MTK] XFlash 存储检测失败，使用 XML 协议", Color.Orange);
                    _useXFlash = false;
                    _xflashClient?.Dispose();
                    _xflashClient = null;
                }
            }
            catch (Exception ex)
            {
                Log($"[MTK] XFlash 初始化失败: {ex.Message}", Color.Orange);
                Log("[MTK] 回退到 XML 协议", Color.Gray);
                _useXFlash = false;
                _xflashClient?.Dispose();
                _xflashClient = null;
            }
        }

        /// <summary>
        /// 切换到 XFlash 协议
        /// </summary>
        public async Task<bool> SwitchToXFlashAsync(CancellationToken ct = default)
        {
            if (_xflashClient != null && _xflashClient.IsConnected)
            {
                _useXFlash = true;
                Log("[MTK] 已切换到 XFlash 协议", Color.Cyan);
                return true;
            }

            // 尝试初始化
            _protocolType = MtkProtocolType.XFlash;
            await InitializeXFlashClientAsync(ct);
            return _useXFlash;
        }

        /// <summary>
        /// 切换到 XML 协议
        /// </summary>
        public void SwitchToXml()
        {
            _useXFlash = false;
            _protocolType = MtkProtocolType.Xml;
            Log("[MTK] 已切换到 XML 协议", Color.Cyan);
        }

        #endregion

        #region BROM Exploit

        /// <summary>
        /// 执行 BROM Exploit 禁用安全保护
        /// 参考 SP Flash Tool 和 mtkclient 的流程
        /// 注意: SEND_CERT (0xE0) 命令只在 BROM 模式下有效
        /// </summary>
        public async Task<bool> RunBromExploitAsync(CancellationToken ct = default)
        {
            if (!IsConnected || _bromClient.HwCode == 0)
            {
                Log("[MTK] 设备未连接", Color.Red);
                return false;
            }

            ushort hwCode = _bromClient.HwCode;
            uint targetConfig = (uint)_bromClient.TargetConfig;

            Log($"[MTK] ═══════════════════════════════════════", Color.Yellow);
            Log($"[MTK] 当前 Target Config: 0x{targetConfig:X8}", Color.Yellow);
            Log($"[MTK] ═══════════════════════════════════════", Color.Yellow);

            // 检查设备模式 - BROM Exploit 只能在 BROM 模式下执行
            if (!_bromClient.IsBromMode)
            {
                Log("[MTK] ⚠ 设备在 Preloader 模式，BROM Exploit (SEND_CERT) 不适用", Color.Orange);
                Log("[MTK] 提示: Preloader 模式需要使用 DA2 级别漏洞 (如 ALLINONE-SIGNATURE)", Color.Yellow);
                Log("[MTK] 提示: 或者尝试让设备进入 BROM 模式 (短接测试点)", Color.Yellow);
                return false;  // 不是错误，只是不适用
            }

            // 检查是否需要 exploit
            if (targetConfig == 0)
            {
                Log("[MTK] ✓ 设备无安全保护，无需执行 Exploit", Color.Green);
                return true;
            }

            Log("[MTK] 设备在 BROM 模式，尝试执行 BROM Exploit...", Color.Cyan);

            // 设置 Payload Manager 日志
            ExploitPayloadManager.SetLogger(msg => Log(msg, Color.Gray));

            // 从嵌入资源或文件获取 payload
            byte[] payload = ExploitPayloadManager.GetPayload(hwCode);
            if (payload == null || payload.Length == 0)
            {
                Log($"[MTK] ⚠ 未找到 HW Code 0x{hwCode:X4} ({ExploitPayloadManager.GetChipName(hwCode)}) 的 Exploit Payload", Color.Orange);
                Log("[MTK] ⚠ 尝试继续，可能会失败", Color.Orange);
                return false;
            }

            Log($"[MTK] 使用 Payload: {ExploitPayloadManager.GetChipName(hwCode)} ({payload.Length} 字节)", Color.Cyan);

            // 保存当前端口信息
            string originalPort = _bromClient.PortName;

            // 发送 exploit payload
            bool sendResult = await _bromClient.SendExploitPayloadAsync(payload, ct);
            if (!sendResult)
            {
                Log("[MTK] Exploit Payload 发送失败", Color.Red);
                return false;
            }

            Log("[MTK] ✓ Exploit Payload 已发送，等待设备重新枚举...", Color.Yellow);

            // 断开当前连接
            _bromClient.Disconnect();

            // 等待设备重新枚举
            await Task.Delay(2000, ct);

            // 尝试重新连接
            Log("[MTK] 尝试重新连接...", Color.Cyan);
            
            string newPort = await WaitForNewMtkPortAsync(ct, 10000);
            if (string.IsNullOrEmpty(newPort))
            {
                Log("[MTK] ⚠ 未检测到新端口，尝试使用原端口重连", Color.Yellow);
                newPort = originalPort;
            }
            else
            {
                Log($"[MTK] 检测到新端口: {newPort}", Color.Cyan);
            }

            // 重新连接
            bool reconnected = await ConnectAsync(newPort, 115200, ct);
            if (!reconnected)
            {
                Log("[MTK] 重新连接失败", Color.Red);
                return false;
            }

            // 检查新的 Target Config
            uint newTargetConfig = (uint)_bromClient.TargetConfig;
            Log($"[MTK] ═══════════════════════════════════════", Color.Green);
            Log($"[MTK] 新 Target Config: 0x{newTargetConfig:X8}", Color.Green);
            Log($"[MTK] ═══════════════════════════════════════", Color.Green);

            if (newTargetConfig == 0)
            {
                Log("[MTK] ✓ Exploit 成功！安全保护已禁用", Color.Green);
                return true;
            }
            else if (newTargetConfig < targetConfig)
            {
                Log("[MTK] ✓ Exploit 部分成功，部分保护已禁用", Color.Yellow);
                return true;
            }
            else
            {
                Log("[MTK] ⚠ Exploit 可能未生效，Target Config 未改变", Color.Orange);
                return false;
            }
        }

        #endregion

        #region DA 加载

        /// <summary>
        /// 设置 DA 文件路径
        /// </summary>
        public void SetDaFilePath(string filePath)
        {
            if (File.Exists(filePath))
            {
                DaFilePath = filePath;
                MtkDaDatabase.SetDaFilePath(filePath);
                Log($"[MTK] DA 文件: {Path.GetFileName(filePath)}", Color.Cyan);
            }
        }

        /// <summary>
        /// 设置自定义 DA1
        /// </summary>
        public void SetCustomDa1(string filePath)
        {
            if (File.Exists(filePath))
            {
                CustomDa1Path = filePath;
                Log($"[MTK] 自定义 DA1: {Path.GetFileName(filePath)}", Color.Cyan);
            }
        }

        /// <summary>
        /// 设置自定义 DA2
        /// </summary>
        public void SetCustomDa2(string filePath)
        {
            if (File.Exists(filePath))
            {
                CustomDa2Path = filePath;
                Log($"[MTK] 自定义 DA2: {Path.GetFileName(filePath)}", Color.Cyan);
            }
        }

        /// <summary>
        /// 加载 DA
        /// </summary>
        public async Task<bool> LoadDaAsync(CancellationToken ct = default)
        {
            if (!IsConnected || _bromClient.HwCode == 0)
            {
                Log("[MTK] 设备未连接", Color.Red);
                return false;
            }

            ushort hwCode = _bromClient.HwCode;
            Log($"[MTK] 加载 DA (HW Code: 0x{hwCode:X4})", Color.Cyan);

            DaEntry da1 = null;
            DaEntry da2 = null;

            // 1. 尝试使用自定义 DA
            if (!string.IsNullOrEmpty(CustomDa1Path) && File.Exists(CustomDa1Path))
            {
                byte[] da1Data = File.ReadAllBytes(CustomDa1Path);
                
                // 处理 DA 数据 (不截取，发送完整文件)
                // 根据 ChimeraTool 抓包分析：虽然声明大小较小，但实际发送完整文件
                da1Data = ProcessDaData(da1Data);
                
                // 检测 DA 格式 (Legacy vs V6)
                // Legacy DA: 以 ARM 指令开头 (0xEA = B 指令, 0xEB = BL 指令)
                // V6 DA: 通常以 "MTK_", "hvea" 或其他特征开头
                // ELF DA: 以 0x7F 'E' 'L' 'F' 开头
                
                bool isLegacyDa = false;
                bool isElfDa = false;
                
                if (da1Data.Length > 4)
                {
                    // 检查是否为 ELF 格式
                    if (da1Data[0] == 0x7F && da1Data[1] == 'E' && da1Data[2] == 'L' && da1Data[3] == 'F')
                    {
                        isElfDa = true;
                        Log("[MTK] 检测到 ELF DA 格式", Color.Yellow);
                    }
                    // 检查是否为 ARM 分支指令 (Legacy DA 特征)
                    else if (da1Data[3] == 0xEA || da1Data[3] == 0xEB)
                    {
                        isLegacyDa = true;
                    }
                    // 检查是否有 V6 特征
                    else if (da1Data.Length > 8)
                    {
                        string header = System.Text.Encoding.ASCII.GetString(da1Data, 0, Math.Min(8, da1Data.Length));
                        if (header.Contains("MTK") || header.Contains("hvea"))
                        {
                            Log($"[MTK] 检测到 V6 DA 特征: {header.Substring(0, 4)}", Color.Yellow);
                        }
                    }
                }
                
                int sigLen;
                int daType;
                
                // 检测签名长度: 检查文件末尾是否有有效签名
                // 官方签名 DA 通常有 0x1000 (4096) 字节签名
                sigLen = DetectDaSignatureLength(da1Data);
                Log($"[MTK] 签名检测结果: 0x{sigLen:X} ({sigLen} 字节)", Color.Gray);
                
                // 优先根据签名长度判断格式
                if (sigLen == 0x1000)
                {
                    // 官方签名 DA (如从 SP Flash Tool 提取的)
                    // 即使头部像 Legacy (ARM branch)，也应该作为 V6 处理
                    daType = (int)DaMode.Xml;
                    Log($"[MTK] DA格式: 官方签名 DA (V6, 签名长度: 0x{sigLen:X})", Color.Yellow);
                }
                else if (isElfDa)
                {
                    // ELF 格式通常是 V6 或更新版本
                    if (sigLen == 0)
                        sigLen = MtkDaDatabase.GetSignatureLength(hwCode, false);
                    daType = (int)MtkDaDatabase.GetDaMode(hwCode);
                    Log($"[MTK] DA格式: ELF/V6 (签名长度: 0x{sigLen:X})", Color.Yellow);
                }
                else if (isLegacyDa && sigLen == 0)
                {
                    // 纯 Legacy DA (ARM 头部，无官方签名)
                    sigLen = 0x100;
                    daType = (int)DaMode.Legacy;
                    Log("[MTK] DA格式: Legacy (签名长度: 0x100)", Color.Yellow);
                }
                else
                {
                    // 默认使用芯片推荐的模式
                    if (sigLen == 0)
                        sigLen = MtkDaDatabase.GetSignatureLength(hwCode, false);
                    daType = (int)MtkDaDatabase.GetDaMode(hwCode);
                    Log($"[MTK] DA格式: 自动检测 {(DaMode)daType} (签名长度: 0x{sigLen:X})", Color.Yellow);
                }
                
                // 优先使用设备报告的地址，回退到数据库地址
                uint da1Addr = _bromClient.ChipInfo?.DaPayloadAddr ?? MtkDaDatabase.GetDa1Address(hwCode);
                if (da1Addr == 0)
                    da1Addr = MtkDaDatabase.GetDa1Address(hwCode);
                    
                da1 = new DaEntry
                {
                    Name = "Custom_DA1",
                    LoadAddr = da1Addr,
                    SignatureLen = sigLen,
                    Data = da1Data,
                    DaType = daType
                };
                Log($"[MTK] 使用自定义 DA1 (加载地址: 0x{da1Addr:X})", Color.Yellow);
            }

            if (!string.IsNullOrEmpty(CustomDa2Path) && File.Exists(CustomDa2Path))
            {
                byte[] da2Data = File.ReadAllBytes(CustomDa2Path);
                
                // DA2 格式由 DA1 模式决定，不由头部决定
                // V6/XML 协议: DA2 通过 XML 命令上传，无独立签名
                // Legacy 协议: DA2 通过 BROM 上传，可能有签名
                
                int sigLen;
                int daType;
                
                // 获取芯片的 DA 模式
                var da2ChipMode = MtkDaDatabase.GetDaMode(hwCode);
                
                if (da2ChipMode == DaMode.Xml || da2ChipMode == DaMode.XFlash)
                {
                    // V6/XFlash: DA2 通过 XML boot_to 或 UPLOAD_DA 命令上传
                    // 无独立签名 (签名验证在 DA1 中完成)
                    sigLen = 0;
                    daType = (int)da2ChipMode;
                    Log($"[MTK] DA2 格式: V6/XML (无独立签名，通过 XML 协议上传)", Color.Yellow);
                }
                else
                {
                    // Legacy: DA2 可能有签名
                    sigLen = MtkDaDatabase.GetSignatureLength(hwCode, true);
                    daType = (int)DaMode.Legacy;
                    Log($"[MTK] DA2 格式: Legacy (签名: 0x{sigLen:X})", Color.Yellow);
                }
                
                da2 = new DaEntry
                {
                    Name = "Custom_DA2",
                    LoadAddr = MtkDaDatabase.GetDa2Address(hwCode),
                    SignatureLen = sigLen,
                    Data = da2Data,
                    DaType = daType
                };
                Log($"[MTK] 使用自定义 DA2: {da2Data.Length} 字节, 地址: 0x{da2.LoadAddr:X8}", Color.Yellow);
            }

            // 2. 如果没有自定义 DA，从 AllInOne DA 文件提取
            if (da1 == null && !string.IsNullOrEmpty(DaFilePath))
            {
                var daResult = _daLoader.ParseDaFile(DaFilePath, hwCode);
                if (daResult.HasValue)
                {
                    da1 = daResult.Value.da1;
                    da2 = da2 ?? daResult.Value.da2;
                }
            }

            if (da1 == null)
            {
                Log("[MTK] 未找到可用的 DA1", Color.Red);
                return false;
            }

            // 检查 DA 模式是否匹配
            var chipDaMode = MtkDaDatabase.GetDaMode(hwCode);
            var daDaMode = (DaMode)da1.DaType;
            
            if (daDaMode != chipDaMode)
            {
                Log($"[MTK] ⚠ DA 模式不匹配: 芯片需要 {chipDaMode}, DA 文件为 {daDaMode}", Color.Orange);
                Log("[MTK] 建议使用正确格式的 DA 文件", Color.Orange);
            }
            
            Log($"[MTK] DA 模式: {daDaMode}, 加载地址: 0x{da1.LoadAddr:X8}", Color.Gray);

            // ═══════════════════════════════════════════════════════════════════
            // 正确流程 (参考 SP Flash Tool 和 mtkclient):
            // 0. 如果设备有安全保护 (Target Config != 0)，先执行 BROM Exploit 禁用保护
            // 1. 无论 BROM 还是 Preloader 模式，都要上传 DA1
            // 2. DA1 运行后，通过 get_connection_agent 检测设备来源
            // 3. 如果 connagent=="preloader" 且 SBC enabled，使用 Carbonara
            // ═══════════════════════════════════════════════════════════════════

            // 0. 检查是否需要执行 BROM Exploit
            uint targetConfig = (uint)_bromClient.TargetConfig;
            bool isBromMode = _bromClient.IsBromMode;
            
            if (targetConfig != 0)
            {
                Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                Log($"[MTK] 检测到安全保护 (Target Config: 0x{targetConfig:X8})", Color.Yellow);
                
                if (isBromMode)
                {
                    // BROM 模式：尝试 BROM Exploit
                    Log("[MTK] 尝试执行 BROM Exploit 禁用保护...", Color.Yellow);
                    Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                    
                    bool exploitResult = await RunBromExploitAsync(ct);
                    if (exploitResult)
                    {
                        // Exploit 成功后，targetConfig 应该变为 0
                        targetConfig = (uint)_bromClient.TargetConfig;
                        if (targetConfig == 0)
                        {
                            Log("[MTK] ✓ 安全保护已成功禁用！", Color.Green);
                        }
                    }
                    else
                    {
                        Log("[MTK] ⚠ BROM Exploit 未成功，继续尝试 DA 上传...", Color.Orange);
                        Log("[MTK] ⚠ 如果设备启用了 DAA，可能会失败", Color.Orange);
                    }
                }
                else
                {
                    // Preloader 模式：BROM Exploit 不适用
                    Log("[MTK] 设备模式: Preloader (BROM Exploit 不适用)", Color.Yellow);
                    Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                    
                    // 检查是否支持 DA2 级别漏洞
                    string exploitType = MtkChipDatabase.GetExploitType(_bromClient.HwCode);
                    if (!string.IsNullOrEmpty(exploitType))
                    {
                        Log($"[MTK] ✓ 此芯片支持 {exploitType} 漏洞 (DA2 级别)", Color.Green);
                        Log("[MTK] 尝试上传 DA，成功后可执行 DA2 漏洞...", Color.Cyan);
                    }
                    else
                    {
                        Log("[MTK] ⚠ Preloader 模式 + DAA 启用", Color.Orange);
                        Log("[MTK] 需要官方签名的 DA 或让设备进入 BROM 模式", Color.Orange);
                    }
                }
            }

            // 记录初始模式 (握手时检测的)
            bool initialIsBrom = _bromClient.IsBromMode;
            Log($"[MTK] 初始模式: {(initialIsBrom ? "BROM" : "Preloader")}", Color.Gray);

            // 1. 上传 DA1 (两种模式都需要!)
            Log("[MTK] 上传 Stage1 DA...", Color.Cyan);
            if (!await _daLoader.UploadDa1Async(da1, ct))
            {
                Log("[MTK] DA1 上传失败", Color.Red);
                return false;
            }

            Log("[MTK] ✓ Stage1 DA 上传成功", Color.Green);

            // 2. 检查端口是否仍然打开 (可能由于 USB 重新枚举而关闭)
            if (!_bromClient.IsPortOpen)
            {
                Log("[MTK] ⚠ 端口已关闭，设备正在重新枚举 USB...", Color.Yellow);
                Log("[MTK] 等待新的 COM 端口出现...", Color.Gray);
                
                // 等待新端口出现并重连
                string newPort = await WaitForNewMtkPortAsync(ct, 15000);
                if (string.IsNullOrEmpty(newPort))
                {
                    Log("[MTK] 未检测到新的 MTK 端口", Color.Red);
                    return false;
                }
                
                Log($"[MTK] 检测到新端口: {newPort}", Color.Cyan);
                
                // 重新连接到新端口 (不需要握手，直接连接到 DA)
                if (!await _bromClient.ConnectAsync(newPort, 115200, ct))
                {
                    Log("[MTK] 重新连接失败", Color.Red);
                    return false;
                }
                
                // 设置状态为 DA1 已加载
                _bromClient.State = MtkDeviceState.Da1Loaded;
                Log("[MTK] ✓ 重新连接成功 (DA 模式)", Color.Green);
            }

            // 3. 创建 XML DA 客户端 (共享端口锁以确保线程安全)
            _xmlClient = new XmlDaClient(
                _bromClient.GetPort(),
                msg => Log(msg, Color.White),
                msg => Log(msg, Color.Gray),
                progress => OnProgress?.Invoke((int)progress, 100),
                _bromClient.GetPortLock()  // 共享端口锁
            );

            // 4. 等待 DA1 就绪 (等待 sync 信号)
            Log("[MTK] 等待 DA1 就绪...", Color.Gray);
            if (!await _xmlClient.WaitForDaReadyAsync(30000, ct))
            {
                Log("[MTK] 等待 DA1 就绪超时", Color.Red);
                return false;
            }

            Log("[MTK] ✓ DA1 就绪", Color.Green);

            // 4. 发送运行时参数设置 (必须，参考 ChimeraTool)
            Log("[MTK] 发送运行时参数...", Color.Gray);
            bool runtimeParamsSet = await _xmlClient.SetRuntimeParametersAsync(ct);
            if (!runtimeParamsSet)
            {
                Log("[MTK] ⚠ 运行时参数设置失败，继续...", Color.Orange);
            }
            
            // 5. 基于初始握手模式判断设备来源
            // Preloader 模式意味着从 Preloader 启动
            bool isPreloaderSource = !_bromClient.IsBromMode;
            
            Log($"[MTK] 设备来源: {(isPreloaderSource ? "Preloader" : "BROM")}", Color.Cyan);
            
            if (isPreloaderSource)
            {
                Log("[MTK] DA1 检测: 设备从 Preloader 启动", Color.Yellow);
            }
            else
            {
                Log("[MTK] DA1 检测: 设备从 BROM 启动", Color.Cyan);
                
                // BROM 启动需要发送 EMI 配置 (初始化 DRAM)
                if (Common.MtkEmiConfig.IsRequired(hwCode))
                {
                    Log("[MTK] 检测到需要 EMI 配置...", Color.Yellow);
                    
                    var emiConfig = Common.MtkEmiConfig.GetConfig(hwCode);
                    if (emiConfig != null && emiConfig.ConfigData.Length > 0)
                    {
                        Log($"[MTK] 发送 EMI 配置: {emiConfig.ConfigLength} 字节", Color.Cyan);
                        
                        bool emiSuccess = await _bromClient.SendEmiConfigAsync(emiConfig.ConfigData, ct);
                        if (!emiSuccess)
                        {
                            Log("[MTK] 警告: EMI 配置发送失败，设备可能无法正常工作", Color.Orange);
                            // 不终止流程，因为某些设备即使EMI失败也能继续
                        }
                        else
                        {
                            Log("[MTK] ✓ EMI 配置发送成功", Color.Green);
                        }
                    }
                    else
                    {
                        Log("[MTK] 警告: 未找到 EMI 配置数据", Color.Orange);
                        Log("[MTK] 提示: 如果设备无法正常工作，请提供设备的 EMI 配置文件", Color.Gray);
                    }
                }
                else
                {
                    Log("[MTK] 此芯片不需要 EMI 配置", Color.Gray);
                }
            }

            // 5. 检查是否需要使用 Carbonara 漏洞利用
            // 条件: connagent=="preloader" AND SBC enabled AND 有 DA2
            bool sbcEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SbcEnabled);
            bool useExploit = isPreloaderSource && sbcEnabled && da2 != null;
            
            Log($"[MTK] SBC 状态: {(sbcEnabled ? "启用" : "禁用")}", Color.Gray);
            
            if (useExploit)
            {
                Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                Log("[MTK] 满足 Carbonara 条件: Preloader + SBC", Color.Yellow);
                Log("[MTK] 执行 Carbonara 运行时漏洞利用...", Color.Yellow);
                Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                
                var exploit = new CarbonaraExploit(msg => Log(msg, Color.Yellow));
                
                // 检查是否被厂商修补
                if (exploit.IsDevicePatched(da1.Data))
                {
                    Log("[MTK] ⚠ DA1 已被厂商修补，尝试普通 DA2 上传", Color.Orange);
                    useExploit = false;
                }
                else
                {
                    // 准备漏洞利用数据
                    var exploitData = exploit.PrepareExploit(
                        da1.Data,
                        da2.Data,
                        da1.LoadAddr,
                        da1.SignatureLen,
                        da2.SignatureLen,
                        isV6: true
                    );

                    if (exploitData != null)
                    {
                        var (newHash, hashOffset, patchedDa2) = exploitData.Value;

                        // 执行运行时漏洞利用
                        bool exploitSuccess = await _xmlClient.ExecuteCarbonaraAsync(
                            da1.LoadAddr,
                            hashOffset,
                            newHash,
                            da2.LoadAddr,
                            patchedDa2,
                            ct
                        );

                        if (exploitSuccess)
                        {
                            Log("[MTK] ✓ Carbonara 漏洞利用成功", Color.Green);
                            OnStateChanged?.Invoke(MtkDeviceState.Da2Loaded);
                            return true;
                        }
                        else
                        {
                            Log("[MTK] Carbonara 漏洞利用失败，尝试普通上传", Color.Orange);
                            useExploit = false;
                        }
                    }
                    else
                    {
                        Log("[MTK] 无法准备漏洞利用数据，尝试普通上传", Color.Orange);
                        useExploit = false;
                    }
                }
            }

            // 6. 普通上传 DA2 (如果漏洞利用未使用或失败)
            if (!useExploit && da2 != null)
            {
                Log("[MTK] 普通方式上传 Stage2...", Color.Cyan);
                if (!await _daLoader.UploadDa2Async(da2, _xmlClient, ct))
                {
                    Log("[MTK] DA2 上传失败", Color.Red);
                    return false;
                }
            }
            
            Log("[MTK] ✓ DA 加载完成", Color.Green);
            OnStateChanged?.Invoke(MtkDeviceState.Da2Loaded);

            // 7. 初始化 XFlash 客户端 (如果需要)
            await InitializeXFlashClientAsync(ct);
            
            // 8. 检查并执行 AllinoneSignature 漏洞 (DA2 级别)
            string chipExploitType = MtkChipDatabase.GetExploitType(_bromClient.HwCode);
            if (chipExploitType == "AllinoneSignature" && IsAllinoneSignatureVulnerable())
            {
                Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                Log("[MTK] 检测到支持 AllinoneSignature 漏洞", Color.Yellow);
                Log("[MTK] 尝试执行 DA2 级别漏洞利用...", Color.Yellow);
                Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
                
                bool exploitSuccess = await RunAllinoneSignatureExploitAsync(null, null, ct);
                if (exploitSuccess)
                {
                    Log("[MTK] ✓ AllinoneSignature 漏洞利用成功", Color.Green);
                    Log("[MTK] 设备安全限制已禁用", Color.Green);
                }
                else
                {
                    Log("[MTK] ⚠ AllinoneSignature 漏洞利用失败", Color.Orange);
                    Log("[MTK] 继续正常操作...", Color.Gray);
                }
            }
            
            return true;
        }

        #endregion

        #region Flash 操作

        /// <summary>
        /// 读取分区表 (支持 XML 和 XFlash 协议)
        /// </summary>
        public async Task<List<MtkPartitionInfo>> ReadPartitionTableAsync(CancellationToken ct = default)
        {
            // 优先使用 XFlash 二进制协议
            if (_useXFlash && _xflashClient != null)
            {
                Log("[MTK] 使用 XFlash 协议读取分区表...", Color.Gray);
                var xflashPartitions = await _xflashClient.ReadPartitionTableAsync(ct);
                if (xflashPartitions != null)
                {
                    Log($"[MTK] ✓ 读取到 {xflashPartitions.Count} 个分区 (XFlash)", Color.Cyan);
                    return xflashPartitions;
                }
            }

            // 回退到 XML 协议
            if (_xmlClient == null || !_xmlClient.IsConnected)
            {
                Log("[MTK] DA 未加载", Color.Red);
                return null;
            }

            Log("[MTK] 使用 XML 协议读取分区表...", Color.Gray);
            var partitions = await _xmlClient.ReadPartitionTableAsync(ct);
            if (partitions != null)
            {
                Log($"[MTK] ✓ 读取到 {partitions.Length} 个分区 (XML)", Color.Cyan);
                return new List<MtkPartitionInfo>(partitions);
            }

            return null;
        }

        /// <summary>
        /// 读取分区 (支持 XML 和 XFlash 协议)
        /// </summary>
        public async Task<bool> ReadPartitionAsync(string partitionName, string outputPath, ulong size, CancellationToken ct = default)
        {
            Log($"[MTK] 读取分区: {partitionName} ({FormatSize(size)})", Color.Cyan);

            byte[] data = null;

            // 优先使用 XFlash 二进制协议
            if (_useXFlash && _xflashClient != null)
            {
                Log("[MTK] 使用 XFlash 协议...", Color.Gray);
                data = await _xflashClient.ReadPartitionAsync(partitionName, 0, size, EmmcPartitionType.User, ct);
            }
            else if (_xmlClient != null && _xmlClient.IsConnected)
            {
                Log("[MTK] 使用 XML 协议...", Color.Gray);
                data = await _xmlClient.ReadPartitionAsync(partitionName, size, ct);
            }
            else
            {
                Log("[MTK] DA 未加载", Color.Red);
                return false;
            }

            if (data != null && data.Length > 0)
            {
                File.WriteAllBytes(outputPath, data);
                Log($"[MTK] ✓ 分区 {partitionName} 已保存 ({FormatSize((ulong)data.Length)})", Color.Green);
                return true;
            }

            Log($"[MTK] 读取分区 {partitionName} 失败", Color.Red);
            return false;
        }

        /// <summary>
        /// 写入分区 (支持 XML 和 XFlash 协议)
        /// </summary>
        public async Task<bool> WritePartitionAsync(string partitionName, string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
            {
                Log($"[MTK] 文件不存在: {filePath}", Color.Red);
                return false;
            }

            byte[] data = File.ReadAllBytes(filePath);
            Log($"[MTK] 写入分区: {partitionName} ({FormatSize((ulong)data.Length)})", Color.Cyan);

            bool success = false;

            // 优先使用 XFlash 二进制协议
            if (_useXFlash && _xflashClient != null)
            {
                Log("[MTK] 使用 XFlash 协议...", Color.Gray);
                success = await _xflashClient.WritePartitionAsync(partitionName, 0, data, EmmcPartitionType.User, ct);
            }
            else if (_xmlClient != null && _xmlClient.IsConnected)
            {
                Log("[MTK] 使用 XML 协议...", Color.Gray);
                success = await _xmlClient.WritePartitionAsync(partitionName, data, ct);
            }
            else
            {
                Log("[MTK] DA 未加载", Color.Red);
                return false;
            }

            if (success)
            {
                Log($"[MTK] ✓ 分区 {partitionName} 写入成功", Color.Green);
            }
            else
            {
                Log($"[MTK] 写入分区 {partitionName} 失败", Color.Red);
            }

            return success;
        }

        /// <summary>
        /// 格式化大小显示
        /// </summary>
        private string FormatSize(ulong size)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;
            double sizeD = size;

            while (sizeD >= 1024 && unitIndex < units.Length - 1)
            {
                sizeD /= 1024;
                unitIndex++;
            }

            return $"{sizeD:F2} {units[unitIndex]}";
        }

        /// <summary>
        /// 擦除分区 (支持 XML 和 XFlash 协议)
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName, CancellationToken ct = default)
        {
            Log($"[MTK] 擦除分区: {partitionName}", Color.Yellow);

            bool success = false;

            // 优先使用 XFlash 二进制协议
            if (_useXFlash && _xflashClient != null)
            {
                success = await _xflashClient.FormatPartitionAsync(partitionName, ct);
            }
            else if (_xmlClient != null && _xmlClient.IsConnected)
            {
                success = await _xmlClient.ErasePartitionAsync(partitionName, ct);
            }
            else
            {
                Log("[MTK] DA 未加载", Color.Red);
                return false;
            }

            if (success)
            {
                Log($"[MTK] ✓ 分区 {partitionName} 已擦除", Color.Green);
            }
            else
            {
                Log($"[MTK] 擦除分区 {partitionName} 失败", Color.Red);
            }

            return success;
        }

        /// <summary>
        /// 批量刷写
        /// </summary>
        public async Task<bool> FlashMultipleAsync(Dictionary<string, string> partitionFiles, CancellationToken ct = default)
        {
            if (_xmlClient == null || !_xmlClient.IsConnected)
            {
                Log("[MTK] DA 未加载", Color.Red);
                return false;
            }

            Log($"[MTK] 开始刷写 {partitionFiles.Count} 个分区...", Color.Cyan);

            int success = 0;
            int total = partitionFiles.Count;

            foreach (var kvp in partitionFiles)
            {
                if (ct.IsCancellationRequested)
                {
                    Log("[MTK] 刷写已取消", Color.Orange);
                    break;
                }

                if (await WritePartitionAsync(kvp.Key, kvp.Value, ct))
                {
                    success++;
                }

                OnProgress?.Invoke(success, total);
            }

            Log($"[MTK] 刷写完成: {success}/{total}", success == total ? Color.Green : Color.Orange);
            return success == total;
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task<bool> RebootAsync(CancellationToken ct = default)
        {
            if (_xmlClient != null && _xmlClient.IsConnected)
            {
                Log("[MTK] 重启设备...", Color.Cyan);
                return await _xmlClient.RebootAsync(ct);
            }
            return false;
        }

        /// <summary>
        /// 关闭设备
        /// </summary>
        public async Task<bool> ShutdownAsync(CancellationToken ct = default)
        {
            if (_xmlClient != null && _xmlClient.IsConnected)
            {
                Log("[MTK] 关闭设备...", Color.Cyan);
                return await _xmlClient.ShutdownAsync(ct);
            }
            return false;
        }

        /// <summary>
        /// 获取 Flash 信息
        /// </summary>
        public async Task<MtkFlashInfo> GetFlashInfoAsync(CancellationToken ct = default)
        {
            if (_xmlClient != null && _xmlClient.IsConnected)
            {
                return await _xmlClient.GetFlashInfoAsync(ct);
            }
            return null;
        }

        #endregion

        #region 安全功能

        /// <summary>
        /// 检测漏洞
        /// </summary>
        public bool CheckVulnerability()
        {
            if (_bromClient == null || _bromClient.HwCode == 0)
                return false;

            var exploit = new CarbonaraExploit();
            return exploit.IsVulnerable(_bromClient.HwCode);
        }

        /// <summary>
        /// 获取安全信息
        /// </summary>
        public MtkSecurityInfo GetSecurityInfo()
        {
            if (_bromClient == null)
                return null;

            return new MtkSecurityInfo
            {
                SecureBootEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SbcEnabled),
                SlaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SlaEnabled),
                DaaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.DaaEnabled),
                MeId = _bromClient.MeId != null ? BitConverter.ToString(_bromClient.MeId).Replace("-", "") : "",
                SocId = _bromClient.SocId != null ? BitConverter.ToString(_bromClient.SocId).Replace("-", "") : ""
            };
        }

        /// <summary>
        /// 执行 MT6989 ALLINONE-SIGNATURE 漏洞利用
        /// 适用于 DA2 已加载后禁用安全检查
        /// </summary>
        /// <param name="shellcodePath">Shellcode 文件路径 (可选)</param>
        /// <param name="pointerTablePath">指针表文件路径 (可选)</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>是否成功</returns>
        public async Task<bool> RunAllinoneSignatureExploitAsync(
            string shellcodePath = null,
            string pointerTablePath = null,
            CancellationToken ct = default)
        {
            if (_xmlClient == null || !_xmlClient.IsConnected)
            {
                Log("[MTK] DA2 未加载, 无法执行 ALLINONE-SIGNATURE 漏洞", Color.Red);
                return false;
            }

            Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
            Log("[MTK] 执行 ALLINONE-SIGNATURE 漏洞利用...", Color.Yellow);
            Log("[MTK] ═══════════════════════════════════════", Color.Yellow);

            try
            {
                // 创建漏洞利用实例
                var exploit = new AllinoneSignatureExploit(
                    _bromClient.GetPort(),
                    msg => Log(msg, Color.Yellow),
                    msg => Log(msg, Color.Gray),
                    progress => OnProgress?.Invoke((int)progress, 100),
                    _xmlClient.GetPortLock()
                );

                // 执行漏洞利用 (新版本自动加载 pointer_table 和 source_file_trigger)
                bool success = await exploit.ExecuteExploitAsync(ct);

                if (success)
                {
                    Log("[MTK] ✓ ALLINONE-SIGNATURE 漏洞利用成功", Color.Green);
                    Log("[MTK] 设备安全检查已禁用", Color.Green);
                }
                else
                {
                    Log("[MTK] ✗ ALLINONE-SIGNATURE 漏洞利用失败", Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log($"[MTK] ALLINONE-SIGNATURE 漏洞异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 检查是否支持 ALLINONE-SIGNATURE 漏洞
        /// 使用芯片数据库判断，不再硬编码芯片列表
        /// </summary>
        public bool IsAllinoneSignatureVulnerable()
        {
            if (_bromClient == null || _bromClient.HwCode == 0)
                return false;

            // 直接使用数据库判断
            return MtkChipDatabase.IsAllinoneSignatureSupported(_bromClient.HwCode);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 处理 DA 数据
        /// 注意: 根据 ChimeraTool 抓包分析，虽然 SEND_DA 声明的大小可能较小，
        /// 但实际发送的是完整的 DA 文件（包含尾部元数据）
        /// 校验和 0xDE21 = 整个文件 864752 字节的 XOR16
        /// </summary>
        private byte[] ProcessDaData(byte[] daData)
        {
            // 不截取，返回完整数据
            // ChimeraTool 虽然声明 863728 字节，但实际发送了 864752 字节
            Log($"[MTK] DA 数据大小: {daData.Length} 字节 (完整发送)", Color.Gray);
            return daData;
        }

        /// <summary>
        /// 检测 DA 文件的签名长度
        /// </summary>
        /// <remarks>
        /// 官方签名 DA 通常有以下签名格式:
        /// - 0x1000 (4096) bytes: 完整证书链签名 (SP Flash Tool DA)
        /// - 0x100 (256) bytes: RSA-2048 签名 (Legacy DA)
        /// - 0x30 (48) bytes: V6 简短签名
        /// </remarks>
        private int DetectDaSignatureLength(byte[] daData)
        {
            if (daData == null || daData.Length < 0x200)
                return 0;
            
            // 方法1: 检查文件大小是否匹配已知的 DA 大小 + 签名
            // 从 SP Flash Tool 提取的 DA 通常是: DA代码 + 0x1000签名
            // 我们提取的 DA1 是 216440 字节，其中签名 4096 字节
            
            // 方法2: 检查最后 0x1000 字节的特征
            if (daData.Length >= 0x1000)
            {
                int sigStart = daData.Length - 0x1000;
                
                // 统计签名区域特征
                int zeroCount = 0;
                int ffCount = 0;
                var seen = new System.Collections.Generic.HashSet<byte>();
                
                // 检查多个采样点
                int sampleSize = Math.Min(512, 0x1000);
                for (int i = 0; i < sampleSize; i++)
                {
                    byte b = daData[sigStart + i];
                    if (b == 0x00) zeroCount++;
                    if (b == 0xFF) ffCount++;
                    seen.Add(b);
                }
                
                int uniqueBytes = seen.Count;
                
                // 签名数据特征:
                // 1. 有足够的多样性 (uniqueBytes > 30)
                // 2. 不全是填充值 (0x00 或 0xFF 不超过 80%)
                bool looksLikeSignature = uniqueBytes > 30 && 
                                          zeroCount < sampleSize * 0.8 && 
                                          ffCount < sampleSize * 0.8;
                
                if (looksLikeSignature)
                {
                    // 额外检查: 签名前应该是代码结束或填充
                    // 但不要太严格，因为编译器输出各异
                    return 0x1000;
                }
            }
            
            // 方法3: 检查 0x100 签名 (Legacy)
            if (daData.Length >= 0x100)
            {
                int sigStart = daData.Length - 0x100;
                var seen = new System.Collections.Generic.HashSet<byte>();
                int zeroCount = 0;
                int ffCount = 0;
                
                for (int i = sigStart; i < daData.Length; i++)
                {
                    byte b = daData[i];
                    if (b == 0x00) zeroCount++;
                    if (b == 0xFF) ffCount++;
                    seen.Add(b);
                }
                
                int uniqueBytes = seen.Count;
                
                if (uniqueBytes > 20 && zeroCount < 200 && ffCount < 200)
                {
                    return 0x100;
                }
            }
            
            return 0;  // 无法检测，让调用者决定
        }

        /// <summary>
        /// 等待新的 MTK 端口出现 (USB 重新枚举后)
        /// </summary>
        private async Task<string> WaitForNewMtkPortAsync(CancellationToken ct, int timeoutMs = 15000)
        {
            var startTime = DateTime.Now;
            string oldPort = _bromClient.PortName;
            
            // 首先等待旧端口消失
            await Task.Delay(500, ct);
            
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (ct.IsCancellationRequested)
                    return null;
                
                // 获取当前所有 COM 端口
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                
                foreach (string port in ports)
                {
                    // 跳过旧端口
                    if (port == oldPort)
                        continue;
                    
                    try
                    {
                        // 尝试打开端口并检测是否是 MTK 设备
                        using (var testPort = new System.IO.Ports.SerialPort(port, 115200))
                        {
                            testPort.ReadTimeout = 500;
                            testPort.WriteTimeout = 500;
                            testPort.Open();
                            
                            // 等待一小段时间让设备稳定
                            await Task.Delay(100, ct);
                            
                            // 尝试发送简单的探测命令或直接返回端口
                            // DA 运行后会在特定端口上响应
                            // 这里简化处理，假设新出现的端口就是 DA 端口
                            testPort.Close();
                            return port;
                        }
                    }
                    catch
                    {
                        // 端口不可用，继续尝试下一个
                    }
                }
                
                await Task.Delay(500, ct);
            }
            
            return null;
        }

        private void Log(string message, Color color)
        {
            OnLog?.Invoke(message, color);
        }

        private void ResetCancellationToken()
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } 
                catch (ObjectDisposedException) { /* 已释放，忽略 */ }
                catch (Exception ex) { Log($"[MTK] 取消令牌异常: {ex.Message}", Color.Gray); }
                try { _cts.Dispose(); } 
                catch (Exception ex) { Log($"[MTK] 释放令牌异常: {ex.Message}", Color.Gray); }
            }
            _cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Disconnect();
            _bromClient?.Dispose();
        }

        #endregion
    }
}
