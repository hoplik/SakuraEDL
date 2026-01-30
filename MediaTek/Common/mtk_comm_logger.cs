// ============================================================================
// SakuraEDL - MediaTek 通讯日志管理器
// 基于 UnlockTool 通讯逻辑风格 (中文版)
// ============================================================================
// 功能:
// - 统一的通讯日志格式
// - 清晰的操作状态显示
// - 设备信息格式化输出
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// 通讯操作类型
    /// </summary>
    public enum CommOperationType
    {
        /// <summary>等待设备连接</summary>
        WaitingDevice,
        /// <summary>握手</summary>
        Handshake,
        /// <summary>读取硬件信息</summary>
        ReadHardwareInfo,
        /// <summary>发送 Download Agent</summary>
        SendingDA,
        /// <summary>同步目标</summary>
        SyncTarget,
        /// <summary>启动设备 (Preloader auth)</summary>
        BootDevice,
        /// <summary>绕过认证</summary>
        BypassAuth,
        /// <summary>同步 DA</summary>
        SyncDA,
        /// <summary>读取分区信息</summary>
        ReadPartitions,
        /// <summary>读取设备信息</summary>
        ReadDeviceInfo,
        /// <summary>连接设备</summary>
        ConnectDevice,
        /// <summary>读取解锁数据</summary>
        ReadUnlockData,
        /// <summary>解锁 Bootloader</summary>
        UnlockBootloader,
        /// <summary>检查锁定状态</summary>
        CheckLockStatus,
        /// <summary>禁用锁定</summary>
        DisableLock,
        /// <summary>Preloader Dump</summary>
        PreloaderDump,
        /// <summary>发送 Exploit</summary>
        SendExploit,
        /// <summary>禁用看门狗</summary>
        DisableWatchdog,
        /// <summary>刷写分区</summary>
        FlashPartition,
        /// <summary>读取分区</summary>
        ReadPartition,
        /// <summary>格式化分区</summary>
        FormatPartition,
        /// <summary>擦除 FRP</summary>
        EraseFRP,
        /// <summary>备份 NV</summary>
        BackupNV,
        /// <summary>恢复 NV</summary>
        RestoreNV,
        /// <summary>重启设备</summary>
        RebootDevice,
        /// <summary>自定义操作</summary>
        Custom
    }

    /// <summary>
    /// 操作状态
    /// </summary>
    public enum CommOperationStatus
    {
        /// <summary>进行中</summary>
        InProgress,
        /// <summary>成功</summary>
        OK,
        /// <summary>失败</summary>
        Failed,
        /// <summary>跳过</summary>
        Skip,
        /// <summary>超时</summary>
        Timeout,
        /// <summary>警告</summary>
        Warning
    }

    /// <summary>
    /// 通讯日志条目
    /// </summary>
    public class CommLogEntry
    {
        public DateTime Timestamp { get; set; }
        public CommOperationType OperationType { get; set; }
        public string CustomOperation { get; set; }
        public CommOperationStatus Status { get; set; }
        public string ExtraInfo { get; set; }
        public string DetailMessage { get; set; }

        public CommLogEntry()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// MTK 设备模式 (用于日志显示)
    /// </summary>
    public enum MtkDeviceMode
    {
        Unknown,
        Brom,       // Boot ROM 模式
        Preloader,  // Preloader 模式
        Da,         // DA 模式
        Meta,       // META 模式
        Factory     // FACTORY 模式
    }

    /// <summary>
    /// MTK 通讯日志管理器 (UnlockTool 风格 - 中文版)
    /// 特性：正常时简洁输出，出错时自动显示详细日志
    /// </summary>
    public class MtkCommLogger
    {
        private readonly Action<string> _output;
        private readonly Action<string> _detailOutput;
        private readonly List<CommLogEntry> _history;
        private readonly List<string> _detailBuffer;  // 详细日志缓存
        private bool _showTimestamp;
        private string _currentPort;
        private MtkDeviceMode _deviceMode;
        private bool _verboseMode;  // 详细模式 (始终显示详细日志)
        private bool _hasError;     // 是否发生过错误
        private CommOperationType _currentOperation;  // 当前操作

        // 操作名称映射 (中文)
        private static readonly Dictionary<CommOperationType, string> OperationNames = new Dictionary<CommOperationType, string>
        {
            { CommOperationType.WaitingDevice, "等待设备" },
            { CommOperationType.Handshake, "握手" },
            { CommOperationType.ReadHardwareInfo, "读取硬件信息" },
            { CommOperationType.SendingDA, "发送 Download-Agent" },
            { CommOperationType.SyncTarget, "同步目标" },
            { CommOperationType.BootDevice, "启动设备 (Preloader auth)" },
            { CommOperationType.BypassAuth, "绕过认证" },
            { CommOperationType.SyncDA, "同步 DA" },
            { CommOperationType.ReadPartitions, "读取分区信息" },
            { CommOperationType.ReadDeviceInfo, "读取设备信息" },
            { CommOperationType.ConnectDevice, "连接设备" },
            { CommOperationType.ReadUnlockData, "读取解锁数据" },
            { CommOperationType.UnlockBootloader, "解锁 Bootloader" },
            { CommOperationType.CheckLockStatus, "检查锁定状态" },
            { CommOperationType.DisableLock, "禁用锁定" },
            { CommOperationType.PreloaderDump, "Preloader 转储" },
            { CommOperationType.SendExploit, "发送 Exploit" },
            { CommOperationType.DisableWatchdog, "禁用看门狗" },
            { CommOperationType.FlashPartition, "刷写分区" },
            { CommOperationType.ReadPartition, "读取分区" },
            { CommOperationType.FormatPartition, "格式化分区" },
            { CommOperationType.EraseFRP, "擦除 FRP" },
            { CommOperationType.BackupNV, "备份 NV" },
            { CommOperationType.RestoreNV, "恢复 NV" },
            { CommOperationType.RebootDevice, "重启设备" },
            { CommOperationType.Custom, "" }
        };

        // 状态映射
        private static readonly Dictionary<CommOperationStatus, string> StatusNames = new Dictionary<CommOperationStatus, string>
        {
            { CommOperationStatus.InProgress, "..." },
            { CommOperationStatus.OK, "OK" },
            { CommOperationStatus.Failed, "失败" },
            { CommOperationStatus.Skip, "跳过" },
            { CommOperationStatus.Timeout, "超时" },
            { CommOperationStatus.Warning, "警告" }
        };

        public MtkCommLogger(Action<string> output, Action<string> detailOutput = null)
        {
            _output = output ?? delegate { };
            _detailOutput = detailOutput ?? _output;
            _history = new List<CommLogEntry>();
            _detailBuffer = new List<string>();
            _showTimestamp = false;
            _verboseMode = false;
            _hasError = false;
        }

        /// <summary>
        /// 设置详细模式 (始终显示所有日志)
        /// </summary>
        public void SetVerboseMode(bool verbose)
        {
            _verboseMode = verbose;
        }

        /// <summary>
        /// 是否为详细模式
        /// </summary>
        public bool IsVerbose => _verboseMode;

        /// <summary>
        /// 是否发生过错误
        /// </summary>
        public bool HasError => _hasError;

        /// <summary>
        /// 清除错误状态和缓存
        /// </summary>
        public void ClearError()
        {
            _hasError = false;
            _detailBuffer.Clear();
        }

        /// <summary>
        /// 开始新操作 (清除之前的详细日志缓存)
        /// </summary>
        public void BeginOperation(CommOperationType operation)
        {
            _currentOperation = operation;
            _detailBuffer.Clear();
        }

        #region 基础日志方法

        /// <summary>
        /// 记录操作状态
        /// </summary>
        public void Log(CommOperationType operation, CommOperationStatus status, string extra = null)
        {
            var entry = new CommLogEntry
            {
                OperationType = operation,
                Status = status,
                ExtraInfo = extra
            };
            _history.Add(entry);

            string opName = OperationNames[operation];
            string statusText = StatusNames[status];
            string message = FormatLogMessage(opName, statusText, extra);

            // 检查是否失败/超时
            bool isError = status == CommOperationStatus.Failed || status == CommOperationStatus.Timeout;
            
            if (isError)
            {
                _hasError = true;
                // 出错时先输出缓存的详细日志
                FlushDetailBuffer();
            }

            _output(message);
            
            // 成功时清除详细日志缓存
            if (status == CommOperationStatus.OK)
            {
                _detailBuffer.Clear();
            }
        }

        /// <summary>
        /// 记录自定义操作
        /// </summary>
        public void Log(string operation, CommOperationStatus status, string extra = null)
        {
            var entry = new CommLogEntry
            {
                OperationType = CommOperationType.Custom,
                CustomOperation = operation,
                Status = status,
                ExtraInfo = extra
            };
            _history.Add(entry);

            string statusText = StatusNames[status];
            string message = FormatLogMessage(operation, statusText, extra);

            // 检查是否失败/超时
            bool isError = status == CommOperationStatus.Failed || status == CommOperationStatus.Timeout;
            
            if (isError)
            {
                _hasError = true;
                // 出错时先输出缓存的详细日志
                FlushDetailBuffer();
            }

            _output(message);
            
            // 成功时清除详细日志缓存
            if (status == CommOperationStatus.OK)
            {
                _detailBuffer.Clear();
            }
        }

        /// <summary>
        /// 记录详细信息 (正常时缓存，出错时显示)
        /// </summary>
        public void LogDetail(string message)
        {
            string formattedMsg = $"  {message}";
            
            if (_verboseMode || _hasError)
            {
                // 详细模式或已出错：直接输出
                _detailOutput(formattedMsg);
            }
            else
            {
                // 正常模式：缓存详细日志
                _detailBuffer.Add(formattedMsg);
            }
        }

        /// <summary>
        /// 输出缓存的详细日志
        /// </summary>
        private void FlushDetailBuffer()
        {
            if (_detailBuffer.Count > 0)
            {
                _output("  ─── 详细日志 ───");
                foreach (var msg in _detailBuffer)
                {
                    _detailOutput(msg);
                }
                _output("  ─────────────────");
                _detailBuffer.Clear();
            }
        }

        /// <summary>
        /// 强制输出详细日志缓存
        /// </summary>
        public void ForceFlushDetails()
        {
            FlushDetailBuffer();
        }

        /// <summary>
        /// 记录原始信息 (不格式化)
        /// </summary>
        public void LogRaw(string message)
        {
            _output(message);
        }

        /// <summary>
        /// 格式化日志消息
        /// </summary>
        private string FormatLogMessage(string operation, string status, string extra)
        {
            var sb = new StringBuilder();
            
            if (_showTimestamp)
            {
                sb.Append($"[{DateTime.Now:HH:mm:ss}] ");
            }

            sb.Append($"{operation}... {status}");

            if (!string.IsNullOrEmpty(extra))
            {
                sb.Append($" [{extra}]");
            }

            return sb.ToString();
        }

        #endregion

        #region 特定场景日志

        /// <summary>
        /// 等待设备连接 (旧接口，兼容)
        /// </summary>
        public void WaitingForDevice(string portName, string mode)
        {
            _currentPort = portName;
            _output($"等待设备... {portName} [{mode}]");
        }

        /// <summary>
        /// 等待设备连接 (新接口，支持模式枚举)
        /// </summary>
        public void WaitingForDevice(string portName, MtkDeviceMode mode, int vid = 0x0E8D, int pid = 0x2000)
        {
            _currentPort = portName;
            _deviceMode = mode;
            
            // UnlockTool 风格: 等待设备... COM3 [PRELOADER:0E8D:2000]
            string modeStr = GetModeString(mode);
            _output($"等待设备... {portName} [{modeStr}:{vid:X4}:{pid:X4}]");
        }

        /// <summary>
        /// 获取模式字符串
        /// </summary>
        private static string GetModeString(MtkDeviceMode mode)
        {
            switch (mode)
            {
                case MtkDeviceMode.Brom: return "BROM";
                case MtkDeviceMode.Preloader: return "PRELOADER";
                case MtkDeviceMode.Da: return "DA";
                case MtkDeviceMode.Meta: return "META";
                case MtkDeviceMode.Factory: return "FACTORY";
                default: return "UNKNOWN";
            }
        }

        /// <summary>
        /// 获取模式中文名称
        /// </summary>
        private static string GetModeNameCN(MtkDeviceMode mode)
        {
            switch (mode)
            {
                case MtkDeviceMode.Brom: return "BROM (Boot ROM)";
                case MtkDeviceMode.Preloader: return "Preloader";
                case MtkDeviceMode.Da: return "DA (Download Agent)";
                case MtkDeviceMode.Meta: return "META (工程测试)";
                case MtkDeviceMode.Factory: return "FACTORY (工厂测试)";
                default: return "未知";
            }
        }

        /// <summary>
        /// 设置当前设备模式
        /// </summary>
        public void SetDeviceMode(MtkDeviceMode mode)
        {
            _deviceMode = mode;
        }

        /// <summary>
        /// 记录模式信息
        /// </summary>
        public void LogMode(MtkDeviceMode mode)
        {
            _deviceMode = mode;
            LogDetail($"模式: {GetModeNameCN(mode)}");
        }

        /// <summary>
        /// 硬件信息
        /// </summary>
        public void LogHardwareInfo(ushort hwCode, ushort hwVer, string chipName, string[] aliases = null)
        {
            var sb = new StringBuilder();
            sb.Append($"  Hardware: {chipName}");
            
            if (aliases != null && aliases.Length > 0)
            {
                sb.Append($" [{string.Join("|", aliases)}]");
            }
            
            sb.Append($" {hwCode:X4} {hwVer:X4} CA00 0000");
            
            _output(sb.ToString());
        }

        /// <summary>
        /// 安全配置
        /// </summary>
        public void LogSecurityConfig(bool sbc, bool sla, bool daa)
        {
            var flags = new List<string>();
            if (sbc) flags.Add("SCB");
            if (sla) flags.Add("SLA");
            if (daa) flags.Add("DAA");
            
            string config = flags.Count > 0 ? string.Join(" ", flags) : "无";
            _output($"  Security Config: {config}");
        }

        /// <summary>
        /// MEID
        /// </summary>
        public void LogMeid(byte[] meid)
        {
            if (meid == null || meid.Length == 0)
            {
                _output("  MEID: (不可用)");
            }
            else
            {
                _output($"  MEID: {BitConverter.ToString(meid).Replace("-", "")}");
            }
        }

        /// <summary>
        /// 存储信息
        /// </summary>
        public void LogStorageInfo(MtkStorageInfo storage)
        {
            if (storage == null) return;
            
            _output($"  {storage.ToUnlockToolFormat()}");
            _output($"  {storage.FormatVendorInfo()}");
            _output($"  {storage.FormatUfsSize()}");
        }

        /// <summary>
        /// 分区信息
        /// </summary>
        public void LogPartitionCount(int count)
        {
            Log(CommOperationType.ReadPartitions, CommOperationStatus.OK, count.ToString());
        }

        /// <summary>
        /// 设备型号
        /// </summary>
        public void LogModel(string model)
        {
            _output($"  Model: {model}");
        }

        /// <summary>
        /// 锁定状态
        /// </summary>
        public void LogLockStatus(string model, bool locked)
        {
            string status = locked ? "已锁定" : "已解锁";
            Log(CommOperationType.CheckLockStatus, CommOperationStatus.OK, $"{model}] [{status}");
        }

        #endregion

        #region 完整通讯流程日志

        /// <summary>
        /// 开始通讯流程
        /// </summary>
        public void BeginCommunication(string portName, string mode)
        {
            _output("═══════════════════════════════════════════════════════════════════");
            _output($"  SakuraEDL MTK 通讯 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output("═══════════════════════════════════════════════════════════════════");
            WaitingForDevice(portName, mode);
        }

        /// <summary>
        /// 记录完整的设备信息
        /// </summary>
        public void LogFullDeviceInfo(
            ushort hwCode, ushort hwVer, string chipName, string[] aliases,
            bool sbc, bool sla, bool daa,
            byte[] meid,
            MtkStorageInfo storage,
            int partitionCount,
            string model,
            bool locked)
        {
            Log(CommOperationType.ReadHardwareInfo, CommOperationStatus.OK);
            LogHardwareInfo(hwCode, hwVer, chipName, aliases);
            LogSecurityConfig(sbc, sla, daa);
            LogMeid(meid);
            
            if (storage != null)
            {
                LogStorageInfo(storage);
            }
            
            LogPartitionCount(partitionCount);
            LogModel(model);
            LogLockStatus(model, locked);
        }

        /// <summary>
        /// 通讯完成
        /// </summary>
        public void EndCommunication(bool success, TimeSpan elapsed)
        {
            _output("───────────────────────────────────────────────────────────────────");
            if (success)
            {
                _output($"  ✓ 操作完成 - 耗时: {elapsed.TotalSeconds:F1} 秒");
            }
            else
            {
                _output($"  ✗ 操作失败 - 耗时: {elapsed.TotalSeconds:F1} 秒");
            }
            _output("═══════════════════════════════════════════════════════════════════");
        }

        #endregion

        #region 协议层日志

        /// <summary>
        /// BROM 握手日志 (详细)
        /// </summary>
        public void LogBromHandshake(byte sent, byte received, byte expected, int index)
        {
            _detailOutput($"  BRom_StartCmd: send[0x{sent:X2}] read[0x{received:X2}] expected[0x{expected:X2}] index[{index}]");
        }

        /// <summary>
        /// BROM 连接成功
        /// </summary>
        public void LogBromConnected()
        {
            Log(CommOperationType.Handshake, CommOperationStatus.OK);
            _detailOutput("  ✓ boot_rom::connect_brom success!");
        }

        /// <summary>
        /// BROM 协议错误
        /// </summary>
        public void LogBromProtocolError(byte expected, byte actual)
        {
            _output($"  ✗ BRom protocol error: ACK 0x{actual:X2} != 0x{expected:X2}");
        }

        /// <summary>
        /// Exploit 执行日志
        /// </summary>
        public void LogExploitExecution(string exploitName, bool success)
        {
            if (success)
            {
                _output($"  ✓ Exploit [{exploitName}] 执行成功");
            }
            else
            {
                _output($"  ✗ Exploit [{exploitName}] 执行失败");
            }
        }

        /// <summary>
        /// 接收数据日志
        /// </summary>
        public void LogDataReceived(string description, int bytes)
        {
            _detailOutput($"  ← 收到: {description} ({bytes} bytes)");
        }

        /// <summary>
        /// 发送数据日志
        /// </summary>
        public void LogDataSent(string description, int bytes)
        {
            _detailOutput($"  → 发送: {description} ({bytes} bytes)");
        }

        #endregion

        #region 进度日志

        /// <summary>
        /// 进度更新
        /// </summary>
        public void LogProgress(string operation, double progress, string detail = null)
        {
            int percent = (int)(progress * 100);
            string bar = new string('█', percent / 5) + new string('░', 20 - percent / 5);
            
            if (!string.IsNullOrEmpty(detail))
            {
                _output($"  [{bar}] {percent}% - {operation}: {detail}");
            }
            else
            {
                _output($"  [{bar}] {percent}% - {operation}");
            }
        }

        /// <summary>
        /// 传输进度
        /// </summary>
        public void LogTransferProgress(string operation, long current, long total)
        {
            double progress = (double)current / total;
            string currentStr = FormatBytes(current);
            string totalStr = FormatBytes(total);
            LogProgress(operation, progress, $"{currentStr} / {totalStr}");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GiB";
            if (bytes >= 1024L * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MiB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KiB";
            return $"{bytes} B";
        }

        #endregion

        #region 配置

        /// <summary>
        /// 设置是否显示时间戳
        /// </summary>
        public void SetShowTimestamp(bool show)
        {
            _showTimestamp = show;
        }

        /// <summary>
        /// 获取历史记录
        /// </summary>
        public IReadOnlyList<CommLogEntry> GetHistory()
        {
            return _history.AsReadOnly();
        }

        /// <summary>
        /// 清除历史
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        #endregion
    }

    /// <summary>
    /// 通讯流程构建器 - 提供流畅的 API 记录通讯流程
    /// </summary>
    public class CommFlowBuilder
    {
        private readonly MtkCommLogger _logger;
        private readonly DateTime _startTime;

        public CommFlowBuilder(MtkCommLogger logger)
        {
            _logger = logger;
            _startTime = DateTime.Now;
        }

        /// <summary>
        /// 开始通讯
        /// </summary>
        public CommFlowBuilder Begin(string port, string mode)
        {
            _logger.BeginCommunication(port, mode);
            return this;
        }

        /// <summary>
        /// 记录操作
        /// </summary>
        public CommFlowBuilder Operation(CommOperationType op, CommOperationStatus status, string extra = null)
        {
            _logger.Log(op, status, extra);
            return this;
        }

        /// <summary>
        /// 记录自定义操作
        /// </summary>
        public CommFlowBuilder Operation(string op, CommOperationStatus status, string extra = null)
        {
            _logger.Log(op, status, extra);
            return this;
        }

        /// <summary>
        /// 记录详细信息
        /// </summary>
        public CommFlowBuilder Detail(string message)
        {
            _logger.LogDetail(message);
            return this;
        }

        /// <summary>
        /// 记录硬件信息
        /// </summary>
        public CommFlowBuilder HardwareInfo(ushort hwCode, ushort hwVer, string chipName, string[] aliases = null)
        {
            _logger.LogHardwareInfo(hwCode, hwVer, chipName, aliases);
            return this;
        }

        /// <summary>
        /// 记录安全配置
        /// </summary>
        public CommFlowBuilder SecurityConfig(bool sbc, bool sla, bool daa)
        {
            _logger.LogSecurityConfig(sbc, sla, daa);
            return this;
        }

        /// <summary>
        /// 记录 MEID
        /// </summary>
        public CommFlowBuilder Meid(byte[] meid)
        {
            _logger.LogMeid(meid);
            return this;
        }

        /// <summary>
        /// 记录存储信息
        /// </summary>
        public CommFlowBuilder Storage(MtkStorageInfo storage)
        {
            _logger.LogStorageInfo(storage);
            return this;
        }

        /// <summary>
        /// 记录型号
        /// </summary>
        public CommFlowBuilder Model(string model)
        {
            _logger.LogModel(model);
            return this;
        }

        /// <summary>
        /// 结束通讯
        /// </summary>
        public void End(bool success)
        {
            var elapsed = DateTime.Now - _startTime;
            _logger.EndCommunication(success, elapsed);
        }
    }
}
