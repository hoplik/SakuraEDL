// ============================================================================
// SakuraEDL - Spreadtrum Service | 展讯服务
// ============================================================================
// [ZH] 展讯刷机服务 - 提供 SPD/Unisoc 设备的完整刷机功能
// [EN] Spreadtrum Flash Service - Complete flashing for SPD/Unisoc devices
// [JA] Spreadtrumフラッシュサービス - SPD/Unisocデバイスの完全なフラッシュ
// [KO] Spreadtrum 플래싱 서비스 - SPD/Unisoc 기기의 완전한 플래싱
// [RU] Сервис прошивки Spreadtrum - Полная функциональность для SPD/Unisoc
// [ES] Servicio de flasheo Spreadtrum - Funcionalidad completa para SPD/Unisoc
// ============================================================================
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.Common;
using SakuraEDL.Spreadtrum.Common;
using SakuraEDL.Spreadtrum.Exploit;
using SakuraEDL.Spreadtrum.Protocol;
using SakuraEDL.Spreadtrum.ISP;

namespace SakuraEDL.Spreadtrum.Services
{
    /// <summary>
    /// 展讯刷机服务 - 主服务类
    /// </summary>
    public class SpreadtrumService : IDisposable
    {
        private FdlClient _client;
        private SprdPortDetector _portDetector;
        private PacParser _pacParser;
        private CancellationTokenSource _cts;
        private SprdExploitService _exploitService;

        // 事件
        public event Action<string, Color> OnLog;
        public event Action<int, int> OnProgress;
        public event Action<SprdDeviceState> OnStateChanged;
        public event Action<SprdDeviceInfo> OnDeviceConnected;
        public event Action<SprdDeviceInfo> OnDeviceDisconnected;
        public event Action<SprdVulnerabilityCheckResult> OnVulnerabilityDetected;
        public event Action<SprdExploitResult> OnExploitCompleted;

        // 属性
        public bool IsConnected => _client?.IsConnected ?? false;
        public bool IsBromMode => _client?.IsBromMode ?? true;
        public FdlStage CurrentStage => _client?.CurrentStage ?? FdlStage.None;
        public SprdDeviceState State => _client?.State ?? SprdDeviceState.Disconnected;

        // 当前加载的 PAC 信息
        public PacInfo CurrentPac { get; private set; }

        // 设备分区表缓存
        public List<SprdPartitionInfo> CachedPartitions { get; private set; }

        // 芯片 ID (0 表示自动检测)
        public uint ChipId { get; private set; }

        // 自定义 FDL 配置
        public string CustomFdl1Path { get; private set; }
        public string CustomFdl2Path { get; private set; }
        public uint CustomFdl1Address { get; private set; }
        public uint CustomFdl2Address { get; private set; }

        /// <summary>
        /// 设置芯片 ID
        /// </summary>
        public void SetChipId(uint chipId)
        {
            ChipId = chipId;
            if (_client != null)
            {
                _client.SetChipId(chipId);
            }
        }

        /// <summary>
        /// 设置自定义 FDL1
        /// </summary>
        public void SetCustomFdl1(string filePath, uint address)
        {
            CustomFdl1Path = filePath;
            CustomFdl1Address = address;
            _client?.SetCustomFdl1(filePath, address);
        }

        /// <summary>
        /// 设置自定义 FDL2
        /// </summary>
        public void SetCustomFdl2(string filePath, uint address)
        {
            CustomFdl2Path = filePath;
            CustomFdl2Address = address;
            _client?.SetCustomFdl2(filePath, address);
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
            _client?.ClearCustomFdl();
        }

        // 看门狗
        private Watchdog _watchdog;
        
        public SpreadtrumService()
        {
            _pacParser = new PacParser(msg => Log(msg, Color.Gray));
            _portDetector = new SprdPortDetector();
            _exploitService = new SprdExploitService((msg, color) => Log(msg, color));
            
            _portDetector.OnLog += msg => Log(msg, Color.Gray);
            _portDetector.OnDeviceConnected += dev => OnDeviceConnected?.Invoke(dev);
            _portDetector.OnDeviceDisconnected += dev => OnDeviceDisconnected?.Invoke(dev);

            // 漏洞利用事件
            _exploitService.OnVulnerabilityDetected += result => OnVulnerabilityDetected?.Invoke(result);
            _exploitService.OnExploitCompleted += result => OnExploitCompleted?.Invoke(result);
            
            // 初始化看门狗
            _watchdog = new Watchdog("Spreadtrum", WatchdogManager.DefaultTimeouts.Spreadtrum, 
                msg => Log(msg, Color.Gray));
            _watchdog.OnTimeout += OnWatchdogTimeout;
        }
        
        /// <summary>
        /// 看门狗超时处理
        /// </summary>
        private void OnWatchdogTimeout(object sender, WatchdogTimeoutEventArgs e)
        {
            Log($"[展讯] 看门狗超时: {e.OperationName} (等待 {e.ElapsedTime.TotalSeconds:F1}秒)", Color.Orange);
            
            if (e.TimeoutCount >= 3)
            {
                Log("[展讯] 多次超时，断开连接", Color.Red);
                e.ShouldReset = false;
                Disconnect();
            }
        }
        
        /// <summary>
        /// 喂狗
        /// </summary>
        public void FeedWatchdog() => _watchdog?.Feed();
        
        /// <summary>
        /// 启动看门狗
        /// </summary>
        public void StartWatchdog(string operation) => _watchdog?.Start(operation);
        
        /// <summary>
        /// 停止看门狗
        /// </summary>
        public void StopWatchdog() => _watchdog?.Stop();

        #region 设备连接

        /// <summary>
        /// 开始监听设备
        /// </summary>
        public void StartDeviceMonitor()
        {
            _portDetector.StartWatching();
        }

        /// <summary>
        /// 停止监听设备
        /// </summary>
        public void StopDeviceMonitor()
        {
            _portDetector.StopWatching();
        }

        /// <summary>
        /// 获取当前连接的设备列表
        /// </summary>
        public IReadOnlyList<SprdDeviceInfo> GetConnectedDevices()
        {
            return _portDetector.ConnectedDevices;
        }

        /// <summary>
        /// 连接设备
        /// </summary>
        public async Task<bool> ConnectAsync(string comPort, int baudRate = 115200)
        {
            try
            {
                Disconnect();

                _client = new FdlClient();
                _client.OnLog += msg => Log(msg, Color.White);
                _client.OnProgress += (current, total) => OnProgress?.Invoke(current, total);
                _client.OnStateChanged += state => OnStateChanged?.Invoke(state);
                
                // 应用已保存的配置到新客户端
                ApplyClientConfiguration();

                Log(string.Format("[展讯] 连接设备: {0}", comPort), Color.Cyan);

                bool success = await _client.ConnectAsync(comPort, baudRate);
                
                if (success)
                {
                    Log("[展讯] 设备连接成功", Color.Green);
                }
                else
                {
                    Log("[展讯] 设备连接失败", Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 连接异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }
        
        /// <summary>
        /// 应用已保存的配置到 FdlClient
        /// </summary>
        private void ApplyClientConfiguration()
        {
            if (_client == null) return;
            
            // 应用芯片 ID (这会自动设置 exec_addr)
            if (ChipId > 0)
            {
                _client.SetChipId(ChipId);
            }
            
            // 应用自定义 FDL 配置
            if (!string.IsNullOrEmpty(CustomFdl1Path) || CustomFdl1Address > 0)
            {
                _client.SetCustomFdl1(CustomFdl1Path, CustomFdl1Address);
            }
            
            if (!string.IsNullOrEmpty(CustomFdl2Path) || CustomFdl2Address > 0)
            {
                _client.SetCustomFdl2(CustomFdl2Path, CustomFdl2Address);
            }
        }

        /// <summary>
        /// 等待设备并自动连接
        /// </summary>
        public async Task<bool> WaitAndConnectAsync(int timeoutMs = 30000)
        {
            Log("[展讯] 等待设备连接...", Color.Yellow);

            ResetCancellationToken();
            var device = await _portDetector.WaitForDeviceAsync(timeoutMs, _cts.Token);

            if (device != null)
            {
                return await ConnectAsync(device.ComPort);
            }

            Log("[展讯] 等待设备超时", Color.Orange);
            return false;
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _cts?.Cancel();
            _client?.Disconnect();
            _client?.Dispose();
            _client = null;
        }

        /// <summary>
        /// 初始化设备 - 自动检测模式并下载 FDL
        /// </summary>
        /// <returns>true: 已进入 FDL2 模式，可以操作分区; false: 初始化失败</returns>
        public async Task<bool> InitializeDeviceAsync()
        {
            if (!IsConnected)
            {
                Log("[展讯] 设备未连接", Color.Red);
                return false;
            }

            // 检查当前状态
            if (CurrentStage == FdlStage.FDL2)
            {
                Log("[展讯] 设备已在 FDL2 模式", Color.Green);
                return true;
            }

            // 如果是 BROM 模式，需要下载 FDL
            if (IsBromMode || CurrentStage == FdlStage.None)
            {
                Log("[展讯] 设备处于 BROM 模式，开始下载 FDL...", Color.Yellow);
                
                // 获取 FDL1 数据和地址
                byte[] fdl1Data = null;
                uint fdl1Addr = 0;
                
                // 优先使用自定义 FDL1
                if (!string.IsNullOrEmpty(CustomFdl1Path) && File.Exists(CustomFdl1Path))
                {
                    fdl1Data = File.ReadAllBytes(CustomFdl1Path);
                    fdl1Addr = CustomFdl1Address;
                    Log($"[展讯] 使用自定义 FDL1: {Path.GetFileName(CustomFdl1Path)}", Color.Cyan);
                }
                // 其次使用 PAC 中的 FDL1
                else if (CurrentPac != null)
                {
                    var fdl1Entry = _pacParser.GetFdl1(CurrentPac);
                    if (fdl1Entry != null)
                    {
                        string tempFdl1 = Path.Combine(Path.GetTempPath(), "fdl1_temp.bin");
                        _pacParser.ExtractFile(CurrentPac.FilePath, fdl1Entry, tempFdl1);
                        fdl1Data = File.ReadAllBytes(tempFdl1);
                        fdl1Addr = fdl1Entry.Address != 0 ? fdl1Entry.Address : CustomFdl1Address;
                        File.Delete(tempFdl1);
                        Log($"[展讯] 使用 PAC 内置 FDL1", Color.Cyan);
                    }
                }
                // 最后使用芯片默认地址
                else if (CustomFdl1Address != 0)
                {
                    fdl1Addr = CustomFdl1Address;
                    Log($"[展讯] 使用芯片默认 FDL1 地址: 0x{fdl1Addr:X}", Color.Yellow);
                }

                // 如果没有 FDL1 数据但有地址，尝试从数据库获取
                if (fdl1Data == null && ChipId != 0)
                {
                    var chipInfo = Database.SprdFdlDatabase.GetChipById(ChipId);
                    if (chipInfo != null)
                    {
                        fdl1Addr = chipInfo.Fdl1Address;
                        Log($"[展讯] 芯片 {chipInfo.ChipName} FDL1 地址: 0x{fdl1Addr:X}", Color.Cyan);
                        
                        // 尝试查找设备特定的 FDL
                        var deviceFdls = Database.SprdFdlDatabase.GetDeviceNames(chipInfo.ChipName);
                        if (deviceFdls.Length > 0)
                        {
                            Log($"[展讯] 提示: 数据库中有 {deviceFdls.Length} 个 {chipInfo.ChipName} 设备的 FDL 可用", Color.Gray);
                        }
                    }
                }

                // 下载 FDL1
                if (fdl1Data != null && fdl1Addr != 0)
                {
                    Log("[展讯] 下载 FDL1...", Color.White);
                    if (!await _client.DownloadFdlAsync(fdl1Data, fdl1Addr, FdlStage.FDL1))
                    {
                        Log("[展讯] FDL1 下载失败", Color.Red);
                        return false;
                    }
                    Log("[展讯] FDL1 下载成功", Color.Green);
                }
                else
                {
                    Log("[展讯] 缺少 FDL1 数据或地址，请加载 PAC 或选择芯片型号", Color.Orange);
                    return false;
                }
            }

            // 下载 FDL2
            if (CurrentStage == FdlStage.FDL1)
            {
                byte[] fdl2Data = null;
                uint fdl2Addr = 0;
                
                // 优先使用自定义 FDL2
                if (!string.IsNullOrEmpty(CustomFdl2Path) && File.Exists(CustomFdl2Path))
                {
                    fdl2Data = File.ReadAllBytes(CustomFdl2Path);
                    fdl2Addr = CustomFdl2Address;
                    Log($"[展讯] 使用自定义 FDL2: {Path.GetFileName(CustomFdl2Path)}", Color.Cyan);
                }
                // 其次使用 PAC 中的 FDL2
                else if (CurrentPac != null)
                {
                    var fdl2Entry = _pacParser.GetFdl2(CurrentPac);
                    if (fdl2Entry != null)
                    {
                        string tempFdl2 = Path.Combine(Path.GetTempPath(), "fdl2_temp.bin");
                        _pacParser.ExtractFile(CurrentPac.FilePath, fdl2Entry, tempFdl2);
                        fdl2Data = File.ReadAllBytes(tempFdl2);
                        fdl2Addr = fdl2Entry.Address != 0 ? fdl2Entry.Address : CustomFdl2Address;
                        File.Delete(tempFdl2);
                        Log($"[展讯] 使用 PAC 内置 FDL2", Color.Cyan);
                    }
                }
                else if (CustomFdl2Address != 0)
                {
                    fdl2Addr = CustomFdl2Address;
                }

                // 从数据库获取 FDL2 地址
                if (fdl2Data == null && ChipId != 0)
                {
                    var chipInfo = Database.SprdFdlDatabase.GetChipById(ChipId);
                    if (chipInfo != null)
                    {
                        fdl2Addr = chipInfo.Fdl2Address;
                        Log($"[展讯] 芯片 {chipInfo.ChipName} FDL2 地址: 0x{fdl2Addr:X}", Color.Cyan);
                    }
                }

                // 下载 FDL2
                if (fdl2Data != null && fdl2Addr != 0)
                {
                    Log("[展讯] 下载 FDL2...", Color.White);
                    if (!await _client.DownloadFdlAsync(fdl2Data, fdl2Addr, FdlStage.FDL2))
                    {
                        Log("[展讯] FDL2 下载失败", Color.Red);
                        return false;
                    }
                    Log("[展讯] FDL2 下载成功", Color.Green);
                }
                else
                {
                    Log("[展讯] 缺少 FDL2 数据或地址，请加载 PAC 或选择芯片型号", Color.Orange);
                    return false;
                }
            }

            // 验证最终状态
            if (CurrentStage == FdlStage.FDL2)
            {
                Log("[展讯] 设备初始化完成，已进入 FDL2 模式", Color.Green);
                OnStateChanged?.Invoke(SprdDeviceState.Fdl2Loaded);
                return true;
            }

            Log("[展讯] 设备初始化失败", Color.Red);
            return false;
        }

        /// <summary>
        /// 连接并初始化设备 (一键操作)
        /// </summary>
        public async Task<bool> ConnectAndInitializeAsync(string comPort, int baudRate = 115200)
        {
            if (!await ConnectAsync(comPort, baudRate))
            {
                return false;
            }

            return await InitializeDeviceAsync();
        }

        #endregion

        #region PAC 固件操作

        /// <summary>
        /// 加载 PAC 固件包
        /// </summary>
        public PacInfo LoadPac(string pacFilePath)
        {
            try
            {
                Log(string.Format("[展讯] 加载 PAC: {0}", Path.GetFileName(pacFilePath)), Color.Cyan);

                CurrentPac = _pacParser.Parse(pacFilePath);

                Log(string.Format("[展讯] 产品: {0}", CurrentPac.Header.ProductName), Color.White);
                Log(string.Format("[展讯] 固件: {0}", CurrentPac.Header.FirmwareName), Color.White);
                Log(string.Format("[展讯] 版本: {0}", CurrentPac.Header.Version), Color.White);
                Log(string.Format("[展讯] 文件数: {0}", CurrentPac.Files.Count), Color.White);

                // 解析 XML 配置
                _pacParser.ParseXmlConfigs(CurrentPac);

                if (CurrentPac.XmlConfig != null)
                {
                    Log(string.Format("[展讯] XML 配置: {0}", CurrentPac.XmlConfig.ConfigType), Color.Gray);
                    
                    if (CurrentPac.XmlConfig.Fdl1Config != null)
                    {
                        Log(string.Format("[展讯] FDL1: {0} @ 0x{1:X}", 
                            CurrentPac.XmlConfig.Fdl1Config.FileName, 
                            CurrentPac.XmlConfig.Fdl1Config.Address), Color.Gray);
                    }
                    
                    if (CurrentPac.XmlConfig.Fdl2Config != null)
                    {
                        Log(string.Format("[展讯] FDL2: {0} @ 0x{1:X}", 
                            CurrentPac.XmlConfig.Fdl2Config.FileName, 
                            CurrentPac.XmlConfig.Fdl2Config.Address), Color.Gray);
                    }

                    if (CurrentPac.XmlConfig.EraseConfig != null)
                    {
                        Log(string.Format("[展讯] 擦除配置: 全部={0}, 用户数据={1}", 
                            CurrentPac.XmlConfig.EraseConfig.EraseAll,
                            CurrentPac.XmlConfig.EraseConfig.EraseUserData), Color.Gray);
                    }
                }

                return CurrentPac;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 加载 PAC 失败: {0}", ex.Message), Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 提取 PAC 文件
        /// </summary>
        public async Task ExtractPacAsync(string outputDir, CancellationToken cancellationToken = default)
        {
            if (CurrentPac == null)
            {
                Log("[展讯] 未加载 PAC 文件", Color.Orange);
                return;
            }

            await Task.Run(() =>
            {
                _pacParser.ExtractAll(CurrentPac, outputDir, (current, total, name) =>
                {
                    Log(string.Format("[展讯] 提取 ({0}/{1}): {2}", current, total, name), Color.Gray);
                    OnProgress?.Invoke(current, total);
                });
            }, cancellationToken);

            Log("[展讯] PAC 提取完成", Color.Green);
        }

        #endregion

        #region 刷机操作

        /// <summary>
        /// 完整刷机流程
        /// </summary>
        public async Task<bool> FlashPacAsync(List<string> selectedPartitions = null, CancellationToken cancellationToken = default)
        {
            if (CurrentPac == null)
            {
                Log("[展讯] 未加载 PAC 文件", Color.Orange);
                return false;
            }

            if (!IsConnected)
            {
                Log("[展讯] 设备未连接", Color.Orange);
                return false;
            }

            try
            {
                Log("[展讯] 开始刷机...", Color.Cyan);

                // 1. 下载 FDL1
                var fdl1Entry = _pacParser.GetFdl1(CurrentPac);
                if (fdl1Entry != null)
                {
                    Log("[展讯] 下载 FDL1...", Color.White);
                    
                    string tempFdl1 = Path.Combine(Path.GetTempPath(), "fdl1.bin");
                    _pacParser.ExtractFile(CurrentPac.FilePath, fdl1Entry, tempFdl1);
                    
                    byte[] fdl1Data = File.ReadAllBytes(tempFdl1);
                    uint fdl1Addr = fdl1Entry.Address != 0 ? fdl1Entry.Address : SprdPlatform.GetFdl1Address(0);

                    if (!await _client.DownloadFdlAsync(fdl1Data, fdl1Addr, FdlStage.FDL1))
                    {
                        Log("[展讯] FDL1 下载失败", Color.Red);
                        return false;
                    }
                    
                    File.Delete(tempFdl1);
                }

                // 2. 下载 FDL2
                var fdl2Entry = _pacParser.GetFdl2(CurrentPac);
                if (fdl2Entry != null)
                {
                    Log("[展讯] 下载 FDL2...", Color.White);
                    
                    string tempFdl2 = Path.Combine(Path.GetTempPath(), "fdl2.bin");
                    _pacParser.ExtractFile(CurrentPac.FilePath, fdl2Entry, tempFdl2);
                    
                    byte[] fdl2Data = File.ReadAllBytes(tempFdl2);
                    uint fdl2Addr = fdl2Entry.Address != 0 ? fdl2Entry.Address : SprdPlatform.GetFdl2Address(0);

                    if (!await _client.DownloadFdlAsync(fdl2Data, fdl2Addr, FdlStage.FDL2))
                    {
                        Log("[展讯] FDL2 下载失败", Color.Red);
                        return false;
                    }
                    
                    File.Delete(tempFdl2);
                }

                // 3. 读取设备信息
                string version = await _client.ReadVersionAsync();
                if (!string.IsNullOrEmpty(version))
                {
                    Log(string.Format("[展讯] 设备版本: {0}", version), Color.Cyan);
                }

                // 4. 刷写分区
                int totalPartitions = 0;
                int currentPartition = 0;

                // 筛选要刷写的分区
                var partitionsToFlash = new List<PacFileEntry>();
                foreach (var entry in CurrentPac.Files)
                {
                    // 跳过 FDL、XML 等
                    if (entry.Type == PacFileType.FDL1 || 
                        entry.Type == PacFileType.FDL2 ||
                        entry.Type == PacFileType.XML ||
                        entry.Size == 0)
                    {
                        continue;
                    }

                    // 如果指定了分区列表，检查是否包含
                    if (selectedPartitions != null && 
                        !selectedPartitions.Contains(entry.PartitionName))
                    {
                        continue;
                    }

                    partitionsToFlash.Add(entry);
                }

                totalPartitions = partitionsToFlash.Count;
                Log(string.Format("[展讯] 准备刷写 {0} 个分区", totalPartitions), Color.White);

                foreach (var entry in partitionsToFlash)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log("[展讯] 刷机已取消", Color.Orange);
                        return false;
                    }

                    currentPartition++;
                    Log(string.Format("[展讯] 刷写分区 ({0}/{1}): {2}", 
                        currentPartition, totalPartitions, entry.PartitionName), Color.White);

                    // 提取分区数据
                    string tempFile = Path.Combine(Path.GetTempPath(), entry.FileName);
                    _pacParser.ExtractFile(CurrentPac.FilePath, entry, tempFile);

                    // 处理 Sparse Image
                    string dataFile = tempFile;
                    bool isSparse = SparseHandler.IsSparseImage(tempFile);
                    
                    if (isSparse)
                    {
                        Log(string.Format("[展讯] 检测到 Sparse Image, 解压中..."), Color.Gray);
                        string rawFile = tempFile + ".raw";
                        var sparseHandler = new SparseHandler(msg => Log(msg, Color.Gray));
                        sparseHandler.Decompress(tempFile, rawFile, (current, total) =>
                        {
                            // 解压进度
                        });
                        dataFile = rawFile;
                        File.Delete(tempFile);
                    }

                    byte[] partitionData = File.ReadAllBytes(dataFile);

                    // 写入分区
                    bool success = await _client.WritePartitionAsync(entry.PartitionName, partitionData, cancellationToken);
                    
                    // 清理临时文件
                    if (isSparse)
                    {
                        if (File.Exists(dataFile))
                            File.Delete(dataFile);
                    }
                    else
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }

                    if (!success)
                    {
                        Log(string.Format("[展讯] 分区 {0} 刷写失败", entry.PartitionName), Color.Red);
                        return false;
                    }
                }

                Log("[展讯] 刷机完成！", Color.Green);
                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 刷机异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 刷写单个分区
        /// </summary>
        public async Task<bool> FlashPartitionAsync(string partitionName, string filePath)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪 (需要加载 FDL2)", Color.Orange);
                return false;
            }

            if (!File.Exists(filePath))
            {
                Log(string.Format("[展讯] 文件不存在: {0}", filePath), Color.Red);
                return false;
            }

            try
            {
                Log(string.Format("[展讯] 刷写分区: {0}", partitionName), Color.White);

                byte[] data = File.ReadAllBytes(filePath);
                bool success = await _client.WritePartitionAsync(partitionName, data);

                if (success)
                {
                    Log(string.Format("[展讯] 分区 {0} 刷写成功", partitionName), Color.Green);
                }
                else
                {
                    Log(string.Format("[展讯] 分区 {0} 刷写失败", partitionName), Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 刷写异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 读取分区
        /// </summary>
        public async Task<bool> ReadPartitionAsync(string partitionName, string outputPath, uint size)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            try
            {
                Log(string.Format("[展讯] 读取分区: {0}", partitionName), Color.White);

                byte[] data = await _client.ReadPartitionAsync(partitionName, size);
                
                if (data != null)
                {
                    File.WriteAllBytes(outputPath, data);
                    Log(string.Format("[展讯] 分区 {0} 读取完成: {1}", partitionName, outputPath), Color.Green);
                    return true;
                }

                Log(string.Format("[展讯] 分区 {0} 读取失败", partitionName), Color.Red);
                return false;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 读取异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 擦除分区
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            try
            {
                Log(string.Format("[展讯] 擦除分区: {0}", partitionName), Color.White);
                
                bool success = await _client.ErasePartitionAsync(partitionName);
                
                if (success)
                {
                    Log(string.Format("[展讯] 分区 {0} 擦除成功", partitionName), Color.Green);
                }
                else
                {
                    Log(string.Format("[展讯] 分区 {0} 擦除失败", partitionName), Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 擦除异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task<bool> RebootAsync()
        {
            if (!IsConnected)
                return false;

            Log("[展讯] 重启设备...", Color.White);
            return await _client.ResetDeviceAsync();
        }

        /// <summary>
        /// 关机
        /// </summary>
        public async Task<bool> PowerOffAsync()
        {
            if (!IsConnected)
                return false;

            Log("[展讯] 关闭设备...", Color.White);
            return await _client.PowerOffAsync();
        }

        /// <summary>
        /// 读取分区表
        /// </summary>
        public async Task<List<SprdPartitionInfo>> ReadPartitionTableAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return null;
            }

            var partitions = await _client.ReadPartitionTableAsync();
            if (partitions != null && partitions.Count > 0)
            {
                CachedPartitions = partitions;
                Log($"[展讯] 分区表已缓存: {partitions.Count} 个分区", Color.Cyan);
            }
            return partitions;
        }

        /// <summary>
        /// 获取分区大小 (从缓存)
        /// </summary>
        public uint GetPartitionSize(string partitionName)
        {
            if (CachedPartitions == null)
                return 0;
            
            var partition = CachedPartitions.FirstOrDefault(p => 
                p.Name.Equals(partitionName, StringComparison.OrdinalIgnoreCase));
            return partition?.Size ?? 0;
        }

        /// <summary>
        /// 读取芯片信息
        /// </summary>
        public async Task<uint> ReadChipTypeAsync()
        {
            if (!IsConnected)
                return 0;

            return await _client.ReadChipTypeAsync();
        }

        #endregion

        #region 安全功能

        /// <summary>
        /// 解锁设备
        /// </summary>
        public async Task<bool> UnlockAsync(byte[] unlockData = null)
        {
            if (!IsConnected)
                return false;

            Log("[展讯] 解锁设备...", Color.Yellow);
            return await _client.UnlockAsync(unlockData);
        }

        /// <summary>
        /// 读取公钥
        /// </summary>
        public async Task<byte[]> ReadPublicKeyAsync()
        {
            if (!IsConnected)
                return null;

            return await _client.ReadPublicKeyAsync();
        }

        /// <summary>
        /// 发送签名
        /// </summary>
        public async Task<bool> SendSignatureAsync(byte[] signature)
        {
            if (!IsConnected)
                return false;

            Log("[展讯] 发送签名验证...", Color.Yellow);
            return await _client.SendSignatureAsync(signature);
        }

        /// <summary>
        /// 读取 eFuse
        /// </summary>
        public async Task<byte[]> ReadEfuseAsync(uint blockId = 0)
        {
            if (!IsConnected)
                return null;

            return await _client.ReadEfuseAsync(blockId);
        }

        #endregion

        #region NV 操作

        /// <summary>
        /// 读取 NV 项
        /// </summary>
        public async Task<byte[]> ReadNvItemAsync(ushort itemId)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
                return null;

            return await _client.ReadNvItemAsync(itemId);
        }

        /// <summary>
        /// 写入 NV 项
        /// </summary>
        public async Task<bool> WriteNvItemAsync(ushort itemId, byte[] data)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
                return false;

            return await _client.WriteNvItemAsync(itemId, data);
        }

        /// <summary>
        /// 读取 IMEI
        /// </summary>
        public async Task<string> ReadImeiAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
                return null;

            return await _client.ReadImeiAsync();
        }

        /// <summary>
        /// 写入 IMEI
        /// </summary>
        public async Task<bool> WriteImeiAsync(string newImei)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            if (string.IsNullOrEmpty(newImei) || newImei.Length != 15)
            {
                Log("[展讯] IMEI 格式无效", Color.Red);
                return false;
            }

            Log(string.Format("[展讯] 写入 IMEI: {0}", newImei), Color.Yellow);

            // 将 IMEI 字符串转换为 NV 数据格式
            byte[] imeiData = ConvertImeiToNvData(newImei);
            
            // 写入 NV 项 0 (IMEI)
            bool result = await _client.WriteNvItemAsync(0, imeiData);
            
            if (result)
            {
                Log("[展讯] IMEI 写入成功", Color.Green);
            }
            else
            {
                Log("[展讯] IMEI 写入失败", Color.Red);
            }

            return result;
        }

        /// <summary>
        /// 将 IMEI 字符串转换为 NV 数据格式
        /// </summary>
        private byte[] ConvertImeiToNvData(string imei)
        {
            // IMEI 存储格式: BCD 编码
            // 15位 IMEI -> 8 bytes (首字节为长度或标志)
            byte[] data = new byte[9];
            data[0] = 0x08;  // 长度标志

            for (int i = 0; i < 15; i += 2)
            {
                int high = imei[i] - '0';
                int low = (i + 1 < 15) ? (imei[i + 1] - '0') : 0xF;
                data[1 + i / 2] = (byte)((low << 4) | high);
            }

            return data;
        }

        #endregion

        #region Flash 信息

        /// <summary>
        /// 读取 Flash 信息
        /// </summary>
        public async Task<SprdFlashInfo> ReadFlashInfoAsync()
        {
            if (!IsConnected)
                return null;

            return await _client.ReadFlashInfoAsync();
        }

        /// <summary>
        /// 重新分区
        /// </summary>
        public async Task<bool> RepartitionAsync(byte[] partitionTableData)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            Log("[展讯] 执行重新分区...", Color.Red);
            return await _client.RepartitionAsync(partitionTableData);
        }

        #endregion

        #region 波特率

        /// <summary>
        /// 设置波特率
        /// </summary>
        public async Task<bool> SetBaudRateAsync(int baudRate)
        {
            if (!IsConnected)
                return false;

            return await _client.SetBaudRateAsync(baudRate);
        }

        #endregion

        #region 漏洞利用

        /// <summary>
        /// 检测设备漏洞
        /// </summary>
        public SprdVulnerabilityCheckResult CheckVulnerability(string pkHash = null)
        {
            uint chipId = _client?.ChipId ?? ChipId;
            return _exploitService.CheckVulnerability(chipId, pkHash);
        }

        /// <summary>
        /// 尝试自动漏洞利用
        /// </summary>
        public async Task<SprdExploitResult> TryExploitAsync(
            SerialPort port = null,
            string pkHash = null,
            CancellationToken ct = default(CancellationToken))
        {
            uint chipId = _client?.ChipId ?? ChipId;
            SerialPort targetPort = port ?? _client?.GetPort();

            if (targetPort == null)
            {
                Log("[漏洞] 无可用串口", Color.Red);
                return new SprdExploitResult
                {
                    Success = false,
                    Message = "无可用串口连接"
                };
            }

            return await _exploitService.TryExploitAsync(targetPort, chipId, pkHash, ct);
        }

        /// <summary>
        /// 检查并尝试漏洞利用
        /// </summary>
        public async Task<SprdExploitResult> CheckAndExploitAsync(CancellationToken ct = default(CancellationToken))
        {
            // 1. 先检测漏洞
            var vulnResult = CheckVulnerability();

            if (!vulnResult.HasVulnerability)
            {
                return new SprdExploitResult
                {
                    Success = false,
                    Message = "未检测到可用漏洞"
                };
            }

            // 2. 尝试利用
            return await TryExploitAsync(ct: ct);
        }

        /// <summary>
        /// 获取漏洞利用服务
        /// </summary>
        public SprdExploitService GetExploitService()
        {
            return _exploitService;
        }

        #endregion

        #region 分区单独刷写

        /// <summary>
        /// 刷写单个 IMG 文件到指定分区 (不依赖 PAC)
        /// </summary>
        public async Task<bool> FlashImageFileAsync(string partitionName, string imageFilePath, CancellationToken ct = default(CancellationToken))
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪 (需要加载 FDL2)", Color.Orange);
                return false;
            }

            if (!File.Exists(imageFilePath))
            {
                Log(string.Format("[展讯] 文件不存在: {0}", imageFilePath), Color.Red);
                return false;
            }

            try
            {
                Log(string.Format("[展讯] 刷写分区: {0} <- {1}", partitionName, Path.GetFileName(imageFilePath)), Color.Cyan);

                string dataFile = imageFilePath;
                bool needCleanup = false;

                // 检测并处理 Sparse Image
                if (SparseHandler.IsSparseImage(imageFilePath))
                {
                    Log("[展讯] 检测到 Sparse Image, 解压中...", Color.Gray);
                    string rawFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(imageFilePath) + ".raw");
                    var sparseHandler = new SparseHandler(msg => Log(msg, Color.Gray));
                    sparseHandler.Decompress(imageFilePath, rawFile, (c, t) => OnProgress?.Invoke((int)c, (int)t));
                    dataFile = rawFile;
                    needCleanup = true;
                }

                // 读取文件数据
                byte[] data = File.ReadAllBytes(dataFile);
                Log(string.Format("[展讯] 数据大小: {0}", FormatSize((ulong)data.Length)), Color.Gray);

                // 写入分区
                bool success = await _client.WritePartitionAsync(partitionName, data, ct);

                // 清理临时文件
                if (needCleanup && File.Exists(dataFile))
                {
                    File.Delete(dataFile);
                }

                if (success)
                {
                    Log(string.Format("[展讯] 分区 {0} 刷写成功", partitionName), Color.Green);
                }
                else
                {
                    Log(string.Format("[展讯] 分区 {0} 刷写失败", partitionName), Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 刷写异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 批量刷写多个分区
        /// </summary>
        public async Task<bool> FlashMultipleImagesAsync(Dictionary<string, string> partitionFiles, CancellationToken ct = default(CancellationToken))
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            int total = partitionFiles.Count;
            int current = 0;
            int success = 0;

            foreach (var kvp in partitionFiles)
            {
                ct.ThrowIfCancellationRequested();
                
                current++;
                Log(string.Format("[展讯] 刷写进度 ({0}/{1}): {2}", current, total, kvp.Key), Color.White);

                if (await FlashImageFileAsync(kvp.Key, kvp.Value, ct))
                {
                    success++;
                }
            }

            Log(string.Format("[展讯] 批量刷写完成: {0}/{1} 成功", success, total), 
                success == total ? Color.Green : Color.Orange);

            return success == total;
        }

        #endregion

        #region 校准数据备份/恢复

        // 校准数据分区名称
        private static readonly string[] CalibrationPartitions = new[]
        {
            "nvitem", "nv", "nvram",           // NV 数据
            "wcnmodem", "wcn",                  // WiFi/BT 校准
            "l_modem", "modem",                 // 射频校准
            "l_fixnv1", "l_fixnv2",            // 固定 NV
            "l_runtimenv1", "l_runtimenv2",    // 运行时 NV
            "prodnv", "prodinfo",              // 生产信息
            "miscdata",                         // 杂项数据
            "factorydata"                       // 工厂数据
        };

        /// <summary>
        /// 备份校准数据
        /// </summary>
        public async Task<bool> BackupCalibrationDataAsync(string outputDir, CancellationToken ct = default(CancellationToken))
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            Log("[展讯] 开始备份校准数据...", Color.Cyan);

            // 获取设备分区表
            var partitions = await _client.ReadPartitionTableAsync();
            if (partitions == null || partitions.Count == 0)
            {
                Log("[展讯] 无法读取分区表", Color.Red);
                return false;
            }

            int backed = 0;

            foreach (var partition in partitions)
            {
                ct.ThrowIfCancellationRequested();

                // 检查是否为校准分区
                bool isCalibration = CalibrationPartitions.Any(c => 
                    partition.Name.ToLower().Contains(c.ToLower()));

                if (!isCalibration)
                    continue;

                Log(string.Format("[展讯] 备份: {0}", partition.Name), Color.White);

                string outputPath = Path.Combine(outputDir, partition.Name + ".bin");
                
                // 读取分区数据
                byte[] data = await _client.ReadPartitionAsync(partition.Name, partition.Size, ct);
                
                if (data != null && data.Length > 0)
                {
                    File.WriteAllBytes(outputPath, data);
                    backed++;
                    Log(string.Format("[展讯] {0} 备份成功 ({1})", partition.Name, FormatSize((ulong)data.Length)), Color.Gray);
                }
                else
                {
                    Log(string.Format("[展讯] {0} 备份失败", partition.Name), Color.Orange);
                }
            }

            Log(string.Format("[展讯] 校准数据备份完成: {0} 个分区", backed), Color.Green);
            return backed > 0;
        }

        /// <summary>
        /// 恢复校准数据
        /// </summary>
        public async Task<bool> RestoreCalibrationDataAsync(string inputDir, CancellationToken ct = default(CancellationToken))
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            if (!Directory.Exists(inputDir))
            {
                Log("[展讯] 备份目录不存在", Color.Red);
                return false;
            }

            Log("[展讯] 开始恢复校准数据...", Color.Cyan);

            var backupFiles = Directory.GetFiles(inputDir, "*.bin");
            if (backupFiles.Length == 0)
            {
                Log("[展讯] 未找到备份文件", Color.Orange);
                return false;
            }

            int restored = 0;

            foreach (var backupFile in backupFiles)
            {
                ct.ThrowIfCancellationRequested();

                string partitionName = Path.GetFileNameWithoutExtension(backupFile);

                // 验证是否为校准分区
                bool isCalibration = CalibrationPartitions.Any(c => 
                    partitionName.ToLower().Contains(c.ToLower()));

                if (!isCalibration)
                {
                    Log(string.Format("[展讯] 跳过非校准分区: {0}", partitionName), Color.Gray);
                    continue;
                }

                Log(string.Format("[展讯] 恢复: {0}", partitionName), Color.White);

                bool success = await FlashImageFileAsync(partitionName, backupFile, ct);

                if (success)
                {
                    restored++;
                }
            }

            Log(string.Format("[展讯] 校准数据恢复完成: {0} 个分区", restored), Color.Green);
            return restored > 0;
        }

        /// <summary>
        /// 获取校准分区列表
        /// </summary>
        public string[] GetCalibrationPartitionNames()
        {
            return CalibrationPartitions;
        }

        #endregion

        #region 强制下载模式

        /// <summary>
        /// 进入强制下载模式 (Force Download)
        /// </summary>
        public async Task<bool> EnterForceDownloadModeAsync()
        {
            Log("[展讯] 尝试进入强制下载模式...", Color.Yellow);

            // 强制下载模式通常需要：
            // 1. 发送特殊的复位命令
            // 2. 或者在设备关机状态下按住特定按键

            if (_client == null || !_client.IsConnected)
            {
                Log("[展讯] 请确保设备已连接", Color.Orange);
                return false;
            }

            try
            {
                // 发送强制下载命令
                bool result = await _client.EnterForceDownloadAsync();
                
                if (result)
                {
                    Log("[展讯] 已进入强制下载模式", Color.Green);
                }
                else
                {
                    Log("[展讯] 进入强制下载模式失败", Color.Red);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        #endregion

        #region 工厂重置

        /// <summary>
        /// 恢复出厂设置
        /// </summary>
        public async Task<bool> FactoryResetAsync(bool eraseUserData = true, bool eraseCache = true, CancellationToken ct = default(CancellationToken))
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未就绪", Color.Orange);
                return false;
            }

            Log("[展讯] 执行恢复出厂设置...", Color.Yellow);

            try
            {
                // 获取分区表
                var partitions = await _client.ReadPartitionTableAsync();
                if (partitions == null)
                {
                    Log("[展讯] 无法读取分区表", Color.Red);
                    return false;
                }

                // 需要擦除的分区
                var partitionsToErase = new List<string>();

                // 擦除 userdata
                if (eraseUserData)
                {
                    var userData = partitions.Find(p => 
                        p.Name.ToLower().Contains("userdata") || 
                        p.Name.ToLower() == "data");
                    if (userData != null)
                    {
                        partitionsToErase.Add(userData.Name);
                    }
                }

                // 擦除 cache
                if (eraseCache)
                {
                    var cache = partitions.Find(p => p.Name.ToLower().Contains("cache"));
                    if (cache != null)
                    {
                        partitionsToErase.Add(cache.Name);
                    }
                }

                // 擦除 metadata (Android 10+)
                var metadata = partitions.Find(p => p.Name.ToLower() == "metadata");
                if (metadata != null)
                {
                    partitionsToErase.Add(metadata.Name);
                }

                // 执行擦除
                int erased = 0;
                foreach (var partName in partitionsToErase)
                {
                    ct.ThrowIfCancellationRequested();

                    Log(string.Format("[展讯] 擦除: {0}", partName), Color.White);
                    bool success = await _client.ErasePartitionAsync(partName);
                    
                    if (success)
                    {
                        erased++;
                        Log(string.Format("[展讯] {0} 已擦除", partName), Color.Gray);
                    }
                    else
                    {
                        Log(string.Format("[展讯] {0} 擦除失败", partName), Color.Orange);
                    }
                }

                Log(string.Format("[展讯] 出厂重置完成: 已擦除 {0} 个分区", erased), Color.Green);
                return erased > 0;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 出厂重置异常: {0}", ex.Message), Color.Red);
                return false;
            }
        }

        #endregion

        #region 安全信息

        /// <summary>
        /// 获取设备安全信息
        /// </summary>
        public async Task<SprdSecurityInfo> GetSecurityInfoAsync()
        {
            if (!IsConnected)
            {
                Log("[展讯] 设备未连接", Color.Orange);
                return null;
            }

            Log("[展讯] 读取安全信息...", Color.Cyan);

            try
            {
                var info = new SprdSecurityInfo();

                // 读取 eFuse 数据
                var efuseData = await _client.ReadEfuseAsync(0);
                if (efuseData != null)
                {
                    info.RawEfuseData = efuseData;
                    ParseEfuseData(efuseData, info);
                }

                // 读取公钥
                var pubKey = await _client.ReadPublicKeyAsync();
                if (pubKey != null && pubKey.Length > 0)
                {
                    info.PublicKeyHash = ComputeHash(pubKey);
                    Log(string.Format("[展讯] 公钥哈希: {0}...", info.PublicKeyHash.Substring(0, 16)), Color.Gray);
                }

                // 判断安全状态
                if (string.IsNullOrEmpty(info.PublicKeyHash) || 
                    info.PublicKeyHash.All(c => c == '0' || c == 'F' || c == 'f'))
                {
                    info.IsSecureBootEnabled = false;
                    Log("[展讯] 安全启动: 未启用 (Unfused)", Color.Yellow);
                }
                else
                {
                    info.IsSecureBootEnabled = true;
                    Log("[展讯] 安全启动: 已启用", Color.White);
                }

                return info;
            }
            catch (Exception ex)
            {
                Log(string.Format("[展讯] 读取安全信息失败: {0}", ex.Message), Color.Red);
                return null;
            }
        }

        private void ParseEfuseData(byte[] efuseData, SprdSecurityInfo info)
        {
            if (efuseData.Length < 4)
                return;

            // 解析 eFuse 标志位
            uint flags = BitConverter.ToUInt32(efuseData, 0);

            info.IsEfuseLocked = (flags & 0x01) != 0;
            info.IsAntiRollbackEnabled = (flags & 0x02) != 0;

            if (efuseData.Length >= 8)
            {
                info.SecurityVersion = BitConverter.ToUInt32(efuseData, 4);
            }

            Log(string.Format("[展讯] eFuse 锁定: {0}", info.IsEfuseLocked ? "是" : "否"), Color.Gray);
            Log(string.Format("[展讯] 防回滚: {0}", info.IsAntiRollbackEnabled ? "是" : "否"), Color.Gray);
            Log(string.Format("[展讯] 安全版本: {0}", info.SecurityVersion), Color.Gray);
        }

        private string ComputeHash(byte[] data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        /// <summary>
        /// 获取 Flash 信息
        /// </summary>
        public async Task<SprdFlashInfo> GetFlashInfoAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                return null;
            }

            return await _client.ReadFlashInfoAsync();
        }

        #endregion

        #region Bootloader 解锁

        /// <summary>
        /// 获取 Bootloader 状态
        /// </summary>
        public async Task<SprdBootloaderStatus> GetBootloaderStatusAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                return null;
            }

            try
            {
                var status = new SprdBootloaderStatus();

                // 读取 eFuse 获取锁定状态
                var efuseData = await _client.ReadEfuseAsync();
                if (efuseData != null && efuseData.Length >= 4)
                {
                    // 检查 Secure Boot 和解锁标志
                    uint efuseFlags = BitConverter.ToUInt32(efuseData, 0);
                    status.IsSecureBootEnabled = (efuseFlags & 0x01) != 0;
                    status.IsUnlocked = (efuseFlags & 0x10) != 0;  // BL 解锁位
                    status.IsUnfused = (efuseFlags & 0x01) == 0;   // 未熔丝
                }

                // 读取公钥哈希判断是否为 Unfused
                var pubKey = await _client.ReadPublicKeyAsync();
                if (pubKey != null)
                {
                    string pkHash = ComputeHash(pubKey);
                    // 检查是否为已知的 Unfused 哈希
                    if (SprdExploitDatabase.IsUnfusedDevice(pkHash))
                    {
                        status.IsUnfused = true;
                    }
                }

                // 读取安全版本
                if (efuseData != null && efuseData.Length >= 8)
                {
                    status.SecurityVersion = BitConverter.ToUInt32(efuseData, 4);
                }

                // 获取设备型号
                var flashInfo = await _client.ReadFlashInfoAsync();
                if (flashInfo != null)
                {
                    status.DeviceModel = flashInfo.ChipModel ?? "Unknown";
                }

                Log($"[展讯] BL状态: {(status.IsUnlocked ? "已解锁" : "已锁定")}, Unfused: {(status.IsUnfused ? "是" : "否")}", Color.Cyan);

                return status;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 获取 BL 状态失败: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 解锁 Bootloader (利用漏洞或直接解锁)
        /// </summary>
        public async Task<bool> UnlockBootloaderAsync(bool useExploit = false)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2", Color.Red);
                return false;
            }

            try
            {
                if (useExploit)
                {
                    // 使用签名绕过漏洞解锁
                    Log("[展讯] 尝试签名绕过解锁...", Color.Yellow);
                    
                    // 先检查设备漏洞
                    var vulnCheck = _exploitService.CheckVulnerability(0, "");
                    if (vulnCheck.HasVulnerability)
                    {
                        var exploitResult = await _exploitService.TryExploitAsync(
                            _client.GetPort(),
                            0);  // chipId=0 表示自动检测
                        
                        if (exploitResult.Success)
                        {
                            // 发送解锁命令
                            return await SendUnlockCommandAsync();
                        }
                        else
                        {
                            Log($"[展讯] 漏洞利用失败: {exploitResult.Message}", Color.Red);
                            return false;
                        }
                    }
                    else
                    {
                        Log("[展讯] 未检测到可用漏洞", Color.Orange);
                        // 尝试直接发送解锁命令
                        return await SendUnlockCommandAsync();
                    }
                }
                else
                {
                    // 直接尝试解锁 (需要设备本身支持)
                    return await SendUnlockCommandAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"[展讯] 解锁失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 使用解锁码解锁 Bootloader
        /// </summary>
        public async Task<bool> UnlockBootloaderWithCodeAsync(string unlockCode)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2", Color.Red);
                return false;
            }

            try
            {
                Log($"[展讯] 使用解锁码解锁...", Color.Yellow);

                // 将16进制字符串转换为字节数组
                byte[] codeBytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    codeBytes[i] = Convert.ToByte(unlockCode.Substring(i * 2, 2), 16);
                }

                // 发送解锁命令
                return await _client.UnlockAsync(codeBytes);
            }
            catch (Exception ex)
            {
                Log($"[展讯] 解锁失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 重新锁定 Bootloader
        /// </summary>
        public async Task<bool> RelockBootloaderAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2", Color.Red);
                return false;
            }

            try
            {
                Log("[展讯] 重新锁定 Bootloader...", Color.Yellow);

                // 发送锁定命令
                // 使用全0作为锁定标识
                byte[] lockCode = new byte[8];
                return await _client.UnlockAsync(lockCode, true);  // true = relock
            }
            catch (Exception ex)
            {
                Log($"[展讯] 锁定失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 发送解锁命令
        /// </summary>
        private async Task<bool> SendUnlockCommandAsync()
        {
            try
            {
                // 写入解锁标志到特定分区或 eFuse
                // Spreadtrum 设备通常将解锁状态存储在 misc 分区或专用分区

                // 方案1: 写入 misc 分区的解锁标志
                byte[] unlockFlag = new byte[16];
                unlockFlag[0] = 0x55;  // 解锁魔数
                unlockFlag[1] = 0x4E;  // 'U'
                unlockFlag[2] = 0x4C;  // 'N'
                unlockFlag[3] = 0x4B;  // 'L'
                unlockFlag[4] = 0x4F;  // 'O'
                unlockFlag[5] = 0x43;  // 'C'
                unlockFlag[6] = 0x4B;  // 'K'
                unlockFlag[7] = 0x45;  // 'E'
                unlockFlag[8] = 0x44;  // 'D'

                // 写入 FRP (Factory Reset Protection) 分区
                bool frpResult = await _client.WritePartitionAsync("frp", unlockFlag);
                
                // 写入 misc 分区
                bool miscResult = await _client.WritePartitionAsync("misc", unlockFlag);

                if (frpResult || miscResult)
                {
                    Log("[展讯] 解锁标志写入成功", Color.Green);
                    return true;
                }
                else
                {
                    // 尝试直接发送 FDL 解锁命令
                    return await _client.UnlockAsync(unlockFlag);
                }
            }
            catch (Exception ex)
            {
                Log($"[展讯] 发送解锁命令失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        #endregion

        #region Boot 镜像解析与设备信息

        private BootParser _bootParser;
        private DeviceInfoExtractor _deviceInfoExtractor;
        private DiagClient _diagClient;

        /// <summary>
        /// 从 Boot.img 文件提取设备信息
        /// </summary>
        public SprdDeviceDetails ExtractDeviceInfoFromBoot(string bootImagePath)
        {
            try
            {
                Log("[展讯] 解析 Boot 镜像...", Color.White);
                
                if (_deviceInfoExtractor == null)
                    _deviceInfoExtractor = new DeviceInfoExtractor(msg => Log(msg, Color.Gray));

                var details = _deviceInfoExtractor.ExtractFromBootImage(bootImagePath);
                
                if (details != null)
                {
                    Log($"[展讯] 设备: {details.GetDisplayName()}", Color.Lime);
                    Log($"[展讯] 版本: {details.GetVersionInfo()}", Color.Lime);
                }
                
                return details;
            }
            catch (Exception ex)
            {
                Log($"[展讯] Boot 解析失败: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 从 Boot.img 数据提取设备信息
        /// </summary>
        public SprdDeviceDetails ExtractDeviceInfoFromBoot(byte[] bootImageData)
        {
            try
            {
                Log("[展讯] 解析 Boot 镜像数据...", Color.White);
                
                if (_deviceInfoExtractor == null)
                    _deviceInfoExtractor = new DeviceInfoExtractor(msg => Log(msg, Color.Gray));

                return _deviceInfoExtractor.ExtractFromBootImage(bootImageData);
            }
            catch (Exception ex)
            {
                Log($"[展讯] Boot 解析失败: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 从 PAC 固件包提取设备信息
        /// </summary>
        public async Task<SprdDeviceDetails> ExtractDeviceInfoFromPacAsync(string pacFilePath)
        {
            try
            {
                Log("[展讯] 从 PAC 提取设备信息...", Color.White);

                // 解析 PAC
                var pacInfo = _pacParser.Parse(pacFilePath);
                if (pacInfo == null)
                {
                    Log("[展讯] PAC 解析失败", Color.Red);
                    return null;
                }

                // 查找 boot.img
                var bootEntry = pacInfo.Files.FirstOrDefault(f => 
                    f.FileName.ToLower().Contains("boot") && 
                    (f.FileName.EndsWith(".img") || f.FileName.EndsWith(".bin")));

                if (bootEntry == null)
                {
                    Log("[展讯] PAC 中未找到 Boot 镜像", Color.Orange);
                    return null;
                }

                // 提取 boot.img
                var pacParser = new PacParser();
                byte[] bootData = await Task.Run(() => pacParser.ExtractFileData(pacInfo.FilePath, bootEntry));
                if (bootData == null || bootData.Length == 0)
                {
                    Log("[展讯] 无法提取 Boot 镜像", Color.Red);
                    return null;
                }

                // 检查是否加密
                if (SprdCryptograph.IsEncrypted(bootData))
                {
                    Log("[展讯] Boot 镜像已加密，尝试解密...", Color.Yellow);
                    bootData = SprdCryptograph.TryDecrypt(bootData);
                }

                return ExtractDeviceInfoFromBoot(bootData);
            }
            catch (Exception ex)
            {
                Log($"[展讯] 从 PAC 提取设备信息失败: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 解压 Ramdisk 并列出文件
        /// </summary>
        public List<CpioEntry> ExtractRamdiskFiles(string bootImagePath)
        {
            try
            {
                if (_bootParser == null)
                    _bootParser = new BootParser(msg => Log(msg, Color.Gray));

                var bootInfo = _bootParser.Parse(bootImagePath);
                return _bootParser.ExtractRamdisk(bootInfo);
            }
            catch (Exception ex)
            {
                Log($"[展讯] Ramdisk 提取失败: {ex.Message}", Color.Red);
                return new List<CpioEntry>();
            }
        }

        #endregion

        #region Diag 诊断通道

        /// <summary>
        /// 连接 Diag 端口
        /// </summary>
        public async Task<bool> ConnectDiagAsync(string portName)
        {
            try
            {
                if (_diagClient == null)
                {
                    _diagClient = new DiagClient();
                    _diagClient.OnLog += msg => Log(msg, Color.Gray);
                }

                return await _diagClient.ConnectAsync(portName);
            }
            catch (Exception ex)
            {
                Log($"[展讯] Diag 连接失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 断开 Diag 连接
        /// </summary>
        public void DisconnectDiag()
        {
            _diagClient?.Disconnect();
        }

        /// <summary>
        /// 通过 Diag 读取 IMEI
        /// </summary>
        public async Task<string> ReadImeiViaDiagAsync(int slot = 1)
        {
            if (_diagClient == null || !_diagClient.IsConnected)
            {
                Log("[展讯] Diag 未连接", Color.Red);
                return null;
            }

            try
            {
                string imei = await _diagClient.ReadImeiAsync(slot);
                if (!string.IsNullOrEmpty(imei))
                {
                    Log($"[展讯] IMEI{slot}: {imei}", Color.Lime);
                }
                return imei;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 读取 IMEI 失败: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 通过 Diag 写入 IMEI
        /// </summary>
        public async Task<bool> WriteImeiViaDiagAsync(string imei, int slot = 1)
        {
            if (_diagClient == null || !_diagClient.IsConnected)
            {
                Log("[展讯] Diag 未连接", Color.Red);
                return false;
            }

            try
            {
                bool result = await _diagClient.WriteImeiAsync(imei, slot);
                if (result)
                {
                    Log($"[展讯] IMEI{slot} 写入成功: {imei}", Color.Lime);
                }
                else
                {
                    Log($"[展讯] IMEI{slot} 写入失败", Color.Red);
                }
                return result;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 写入 IMEI 失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 通过 Diag 发送 AT 命令
        /// </summary>
        public async Task<string> SendAtCommandViaDiagAsync(string command)
        {
            if (_diagClient == null || !_diagClient.IsConnected)
            {
                Log("[展讯] Diag 未连接", Color.Red);
                return null;
            }

            try
            {
                return await _diagClient.SendAtCommandAsync(command);
            }
            catch (Exception ex)
            {
                Log($"[展讯] AT 命令失败: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 通过 Diag 切换到下载模式
        /// </summary>
        public async Task<bool> SwitchToDownloadModeViaDiagAsync()
        {
            if (_diagClient == null || !_diagClient.IsConnected)
            {
                Log("[展讯] Diag 未连接", Color.Red);
                return false;
            }

            try
            {
                Log("[展讯] 切换到下载模式...", Color.Yellow);
                return await _diagClient.SwitchToDownloadModeAsync();
            }
            catch (Exception ex)
            {
                Log($"[展讯] 切换下载模式失败: {ex.Message}", Color.Red);
                return false;
            }
        }

        #endregion

        #region 高级功能 (iReverseSPRDClient)

        /// <summary>
        /// 解锁 Bootloader
        /// </summary>
        public async Task<bool> UnlockBootloaderAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2 阶段", Color.Red);
                return false;
            }

            try
            {
                Log("[展讯] 尝试解锁 Bootloader...", Color.Yellow);
                
                // 使用 UnlockAsync 方法 (relock=false 表示解锁)
                bool success = await _client.UnlockAsync(null, false);
                
                if (success)
                {
                    Log("[展讯] Bootloader 解锁成功", Color.Lime);
                }
                else
                {
                    Log("[展讯] Bootloader 解锁失败，当前 FDL 可能不支持此功能", Color.Orange);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log($"[展讯] Bootloader 解锁异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 锁定 Bootloader
        /// </summary>
        public async Task<bool> LockBootloaderAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2 阶段", Color.Red);
                return false;
            }

            try
            {
                Log("[展讯] 尝试锁定 Bootloader...", Color.Yellow);
                
                // 使用 UnlockAsync 方法 (relock=true 表示锁定)
                bool success = await _client.UnlockAsync(null, true);
                
                if (success)
                {
                    Log("[展讯] Bootloader 锁定成功", Color.Lime);
                }
                else
                {
                    Log("[展讯] Bootloader 锁定失败，当前 FDL 可能不支持此功能", Color.Orange);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log($"[展讯] Bootloader 锁定异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 设置 A/B 分区活动槽位
        /// </summary>
        public async Task<bool> SetActiveSlotAsync(ActiveSlot slot)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2 阶段", Color.Red);
                return false;
            }

            try
            {
                // 检查 misc 分区是否存在
                if (!await _client.CheckPartitionExistAsync("misc"))
                {
                    Log("[展讯] misc 分区不存在，无法切换槽位", Color.Red);
                    return false;
                }

                string slotName = slot == ActiveSlot.SlotA ? "A" : "B";
                Log($"[展讯] 设置活动槽位为: {slotName}", Color.Yellow);

                byte[] payload = SlotPayloads.GetPayload(slot);
                
                // 写入 misc 分区（完整写入 payload 数据）
                // 注意：此方法会覆盖整个分区，payload 需要包含正确的偏移数据
                bool success = await _client.WritePartitionAsync("misc", payload);
                
                if (success)
                {
                    Log($"[展讯] 活动槽位已设置为: {slotName}", Color.Lime);
                }
                else
                {
                    Log("[展讯] 设置活动槽位失败", Color.Red);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 设置活动槽位异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 启用/禁用 DM-Verity (需要完整读取和修改 vbmeta 分区)
        /// </summary>
        public async Task<bool> SetDmVerityAsync(bool enable)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2 阶段", Color.Red);
                return false;
            }

            try
            {
                // 检查 vbmeta 分区是否存在
                if (!await _client.CheckPartitionExistAsync("vbmeta"))
                {
                    Log("[展讯] vbmeta 分区不存在，无法修改 DM-Verity", Color.Red);
                    return false;
                }

                string action = enable ? "启用" : "禁用";
                Log($"[展讯] {action} DM-Verity...", Color.Yellow);

                // 读取 vbmeta 分区
                var partitions = await _client.ReadPartitionTableAsync();
                var vbmetaInfo = partitions?.FirstOrDefault(p => 
                    p.Name.Equals("vbmeta", StringComparison.OrdinalIgnoreCase));
                
                if (vbmetaInfo == null)
                {
                    Log("[展讯] 无法获取 vbmeta 分区信息", Color.Red);
                    return false;
                }

                byte[] vbmetaData = await _client.ReadPartitionAsync("vbmeta", vbmetaInfo.Size);
                if (vbmetaData == null || vbmetaData.Length < DmVerityControl.VbmetaFlagOffset + 1)
                {
                    Log("[展讯] 读取 vbmeta 分区失败", Color.Red);
                    return false;
                }

                // 修改 flag 字节
                byte[] verityData = DmVerityControl.GetVerityData(enable);
                vbmetaData[DmVerityControl.VbmetaFlagOffset] = verityData[0];
                
                // 写回 vbmeta 分区
                bool success = await _client.WritePartitionAsync("vbmeta", vbmetaData);
                
                if (success)
                {
                    Log($"[展讯] DM-Verity 已{action}", Color.Lime);
                }
                else
                {
                    Log($"[展讯] {action} DM-Verity 失败", Color.Red);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 修改 DM-Verity 异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 重启到指定模式
        /// </summary>
        public async Task<bool> ResetToModeAsync(ResetToMode mode)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2 阶段", Color.Red);
                return false;
            }

            try
            {
                string modeName = mode switch
                {
                    ResetToMode.Normal => "正常模式",
                    ResetToMode.Recovery => "Recovery",
                    ResetToMode.Fastboot => "Fastboot",
                    ResetToMode.FactoryReset => "恢复出厂设置",
                    ResetToMode.EraseFrp => "擦除 FRP",
                    _ => "未知"
                };
                
                Log($"[展讯] 准备重启到: {modeName}", Color.Yellow);

                // 擦除 FRP 模式
                if (mode == ResetToMode.EraseFrp)
                {
                    if (await _client.CheckPartitionExistAsync("frp"))
                    {
                        Log("[展讯] 擦除 frp 分区...", Color.White);
                        await _client.ErasePartitionAsync("frp");
                    }
                    if (await _client.CheckPartitionExistAsync("persistent"))
                    {
                        Log("[展讯] 擦除 persistent 分区...", Color.White);
                        await _client.ErasePartitionAsync("persistent");
                    }
                    Log("[展讯] FRP 数据已擦除", Color.Lime);
                    return true;
                }

                // 需要写入 misc 分区的模式
                if (mode != ResetToMode.Normal)
                {
                    if (!await _client.CheckPartitionExistAsync("misc"))
                    {
                        Log("[展讯] misc 分区不存在", Color.Orange);
                    }
                    else
                    {
                        byte[] miscData = MiscCommands.CreateMiscData(mode);
                        if (miscData != null)
                        {
                            await _client.WritePartitionAsync("misc", miscData);
                        }
                    }

                    // 擦除 FRP 相关分区
                    if (await _client.CheckPartitionExistAsync("frp"))
                    {
                        await _client.ErasePartitionAsync("frp");
                    }
                    if (await _client.CheckPartitionExistAsync("persistent"))
                    {
                        await _client.ErasePartitionAsync("persistent");
                    }
                }

                // 发送重启命令
                await _client.ResetDeviceAsync();
                
                Log($"[展讯] 设备将重启到: {modeName}", Color.Lime);
                return true;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 重启异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 绕过签名写入分区 (使用临时重分区) - 需要先实现分区表序列化
        /// 注意：此功能需要 FdlClient 支持分区表修改，当前为简化版实现
        /// </summary>
        public async Task<bool> WritePartitionSkipVerifyAsync(
            string partitionName, 
            byte[] data)
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                Log("[展讯] 设备未连接或未进入 FDL2 阶段", Color.Red);
                return false;
            }

            if (!SkipVerifyHelper.CanUseSkipVerify(partitionName))
            {
                Log($"[展讯] 分区 {partitionName} 不支持绕过签名写入", Color.Red);
                return false;
            }

            try
            {
                Log($"[展讯] 尝试写入分区: {partitionName}", Color.Yellow);
                
                // 直接写入分区 (FDL2 可能已支持绕过验证)
                bool success = await _client.WritePartitionAsync(partitionName, data);
                
                if (success)
                {
                    Log($"[展讯] 分区 {partitionName} 写入成功", Color.Lime);
                }
                else
                {
                    Log($"[展讯] 分区 {partitionName} 写入失败", Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 写入异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 擦除 FRP 分区
        /// </summary>
        public async Task<bool> EraseFrpAsync()
        {
            return await ResetToModeAsync(ResetToMode.EraseFrp);
        }

        /// <summary>
        /// 检查是否为 A/B 分区系统
        /// </summary>
        public async Task<bool> IsAbSystemAsync()
        {
            if (!IsConnected || CurrentStage != FdlStage.FDL2)
            {
                return false;
            }

            try
            {
                // 检查 boot_a 分区是否存在
                return await _client.CheckPartitionExistAsync("boot_a");
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 固件加解密

        /// <summary>
        /// 解密固件文件
        /// </summary>
        public void DecryptFirmware(string inputPath, string outputPath, string password = null)
        {
            try
            {
                Log($"[展讯] 解密固件: {Path.GetFileName(inputPath)}", Color.White);
                SprdCryptograph.DecryptFile(inputPath, outputPath, password);
                Log("[展讯] 解密完成", Color.Lime);
            }
            catch (Exception ex)
            {
                Log($"[展讯] 解密失败: {ex.Message}", Color.Red);
                throw;
            }
        }

        /// <summary>
        /// 加密固件文件
        /// </summary>
        public void EncryptFirmware(string inputPath, string outputPath, string password = null)
        {
            try
            {
                Log($"[展讯] 加密固件: {Path.GetFileName(inputPath)}", Color.White);
                SprdCryptograph.EncryptFile(inputPath, outputPath, password);
                Log("[展讯] 加密完成", Color.Lime);
            }
            catch (Exception ex)
            {
                Log($"[展讯] 加密失败: {ex.Message}", Color.Red);
                throw;
            }
        }

        /// <summary>
        /// 检查文件是否已加密
        /// </summary>
        public bool IsFirmwareEncrypted(string filePath)
        {
            return SprdCryptograph.IsEncrypted(filePath);
        }

        #endregion

        #region ISP eMMC 直接访问

        private EmmcPartitionManager _ispManager;

        /// <summary>
        /// ISP 分区管理器
        /// </summary>
        public EmmcPartitionManager IspManager => _ispManager;

        /// <summary>
        /// ISP 模式是否就绪
        /// </summary>
        public bool IsIspReady => _ispManager?.IsReady ?? false;

        /// <summary>
        /// 检测 ISP USB 设备
        /// </summary>
        public List<DetectedUsbStorage> DetectIspDevices()
        {
            Log("[展讯] 检测 ISP USB 设备...", Color.Yellow);
            var devices = EmmcPartitionManager.DetectUsbStorageDevices();
            
            foreach (var device in devices)
            {
                Log($"  发现: {device.FriendlyName} ({device.Size / 1024 / 1024} MB)", Color.White);
            }
            
            if (devices.Count == 0)
            {
                Log("[展讯] 未检测到 USB 存储设备", Color.Gray);
            }
            
            return devices;
        }

        /// <summary>
        /// 检测 Spreadtrum ISP 设备
        /// </summary>
        public DetectedUsbStorage DetectSprdIspDevice()
        {
            return EmmcPartitionManager.DetectSprdIspDevice();
        }

        /// <summary>
        /// 等待 ISP 设备连接
        /// </summary>
        public async Task<DetectedUsbStorage> WaitForIspDeviceAsync(int timeoutSeconds = 60)
        {
            Log("[展讯] 等待 ISP 设备连接...", Color.Yellow);
            var device = await EmmcPartitionManager.WaitForDeviceAsync(timeoutSeconds, _cts?.Token ?? default);
            
            if (device != null)
            {
                Log($"[展讯] ISP 设备已连接: {device.FriendlyName}", Color.Lime);
            }
            else
            {
                Log("[展讯] 等待 ISP 设备超时", Color.Red);
            }
            
            return device;
        }

        /// <summary>
        /// 打开 ISP 设备
        /// </summary>
        public bool OpenIspDevice(string devicePath)
        {
            try
            {
                if (_ispManager == null)
                {
                    _ispManager = new EmmcPartitionManager();
                    _ispManager.OnLog += msg => Log($"[ISP] {msg}", Color.Cyan);
                    _ispManager.OnProgress += (cur, total) => OnProgress?.Invoke((int)(cur * 100 / total), 100);
                }

                Log($"[展讯] 打开 ISP 设备: {devicePath}", Color.Yellow);
                
                if (_ispManager.Open(devicePath))
                {
                    Log($"[展讯] ISP 设备已打开", Color.Lime);
                    Log($"  扇区大小: {_ispManager.Device.SectorSize} 字节", Color.White);
                    Log($"  总大小: {_ispManager.Device.TotalSize / 1024 / 1024} MB", Color.White);
                    Log($"  分区数: {_ispManager.Partitions.Count}", Color.White);
                    
                    // 打印分区表
                    foreach (var p in _ispManager.Partitions)
                    {
                        Log($"    {p.Name}: {p.GetSize(_ispManager.Device.SectorSize) / 1024 / 1024} MB", Color.Gray);
                    }
                    
                    return true;
                }
                
                Log("[展讯] 打开 ISP 设备失败", Color.Red);
                return false;
            }
            catch (Exception ex)
            {
                Log($"[展讯] 打开 ISP 设备异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 关闭 ISP 设备
        /// </summary>
        public void CloseIspDevice()
        {
            _ispManager?.Close();
            Log("[展讯] ISP 设备已关闭", Color.White);
        }

        /// <summary>
        /// ISP 读取分区到文件
        /// </summary>
        public async Task<bool> IspReadPartitionAsync(string partitionName, string outputPath)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            Log($"[展讯] ISP 读取分区: {partitionName} -> {outputPath}", Color.Yellow);
            var result = await _ispManager.ReadPartitionAsync(partitionName, outputPath, _cts?.Token ?? default);
            
            if (result.Success)
            {
                Log($"[展讯] 分区读取成功: {result.BytesTransferred / 1024 / 1024} MB, 耗时 {result.Duration.TotalSeconds:F1}s", Color.Lime);
            }
            else
            {
                Log($"[展讯] 分区读取失败: {result.ErrorMessage}", Color.Red);
            }
            
            return result.Success;
        }

        /// <summary>
        /// ISP 写入文件到分区
        /// </summary>
        public async Task<bool> IspWritePartitionAsync(string partitionName, string inputPath)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            Log($"[展讯] ISP 写入分区: {inputPath} -> {partitionName}", Color.Yellow);
            var result = await _ispManager.WritePartitionAsync(partitionName, inputPath, _cts?.Token ?? default);
            
            if (result.Success)
            {
                Log($"[展讯] 分区写入成功: {result.BytesTransferred / 1024 / 1024} MB, 耗时 {result.Duration.TotalSeconds:F1}s", Color.Lime);
            }
            else
            {
                Log($"[展讯] 分区写入失败: {result.ErrorMessage}", Color.Red);
            }
            
            return result.Success;
        }

        /// <summary>
        /// ISP 擦除分区
        /// </summary>
        public bool IspErasePartition(string partitionName)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            Log($"[展讯] ISP 擦除分区: {partitionName}", Color.Yellow);
            var result = _ispManager.ErasePartition(partitionName);
            
            if (result.Success)
            {
                Log($"[展讯] 分区擦除成功", Color.Lime);
            }
            else
            {
                Log($"[展讯] 分区擦除失败: {result.ErrorMessage}", Color.Red);
            }
            
            return result.Success;
        }

        /// <summary>
        /// ISP 备份所有分区
        /// </summary>
        public async Task<bool> IspBackupAllPartitionsAsync(string outputFolder)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            Log($"[展讯] ISP 备份所有分区到: {outputFolder}", Color.Yellow);
            var results = await _ispManager.BackupAllPartitionsAsync(outputFolder, _cts?.Token ?? default);
            
            int success = results.Count(r => r.Success);
            int failed = results.Count(r => !r.Success);
            
            Log($"[展讯] 备份完成: 成功 {success}, 失败 {failed}", success == results.Count ? Color.Lime : Color.Orange);
            
            return failed == 0;
        }

        /// <summary>
        /// ISP 备份 GPT
        /// </summary>
        public bool IspBackupGpt(string outputPath)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            return _ispManager.BackupGpt(outputPath);
        }

        /// <summary>
        /// ISP 恢复 GPT
        /// </summary>
        public bool IspRestoreGpt(string inputPath)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            return _ispManager.RestoreGpt(inputPath);
        }

        /// <summary>
        /// ISP 读取原始扇区
        /// </summary>
        public byte[] IspReadRawSectors(long startSector, int sectorCount)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return null;
            }

            return _ispManager.ReadRawSectors(startSector, sectorCount);
        }

        /// <summary>
        /// ISP 写入原始扇区
        /// </summary>
        public bool IspWriteRawSectors(long startSector, byte[] data)
        {
            if (!IsIspReady)
            {
                Log("[展讯] ISP 设备未就绪", Color.Red);
                return false;
            }

            return _ispManager.WriteRawSectors(startSector, data);
        }

        /// <summary>
        /// 获取 ISP 分区列表
        /// </summary>
        public List<EmmcPartitionInfo> GetIspPartitions()
        {
            return _ispManager?.Partitions ?? new List<EmmcPartitionInfo>();
        }

        /// <summary>
        /// 查找 ISP 分区
        /// </summary>
        public EmmcPartitionInfo FindIspPartition(string name)
        {
            return _ispManager?.Gpt?.FindPartition(name);
        }

        #endregion

        #region 辅助方法

        private string FormatSize(ulong size)
        {
            if (size >= 1024UL * 1024 * 1024)
                return string.Format("{0:F2} GB", size / (1024.0 * 1024 * 1024));
            if (size >= 1024 * 1024)
                return string.Format("{0:F2} MB", size / (1024.0 * 1024));
            if (size >= 1024)
                return string.Format("{0:F2} KB", size / 1024.0);
            return string.Format("{0} B", size);
        }

        private void Log(string message, Color color)
        {
            OnLog?.Invoke(message, color);
        }

        public void Dispose()
        {
            Disconnect();
            _portDetector?.Dispose();
            
            // 释放 ISP 管理器
            _ispManager?.Dispose();
            _ispManager = null;
            
            // 释放 CancellationTokenSource (忽略异常，确保完整清理)
            if (_cts != null)
            {
                try { _cts.Cancel(); } 
                catch (ObjectDisposedException) { /* 已释放，忽略 */ }
                try { _cts.Dispose(); } 
                catch (ObjectDisposedException) { /* 已释放，忽略 */ }
                _cts = null;
            }
            
            // 释放看门狗
            _watchdog?.Dispose();
        }

        /// <summary>
        /// 安全重置 CancellationTokenSource
        /// </summary>
        private void ResetCancellationToken()
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } 
                catch (ObjectDisposedException) { /* 已释放，忽略 */ }
                catch (Exception ex) { Log($"[展讯] 取消令牌异常: {ex.Message}", Color.Gray); }
                try { _cts.Dispose(); } 
                catch (Exception ex) { Log($"[展讯] 释放令牌异常: {ex.Message}", Color.Gray); }
            }
            _cts = new CancellationTokenSource();
        }

        #endregion
    }
}
