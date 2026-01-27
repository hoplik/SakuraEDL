// ============================================================================
// LoveAlways - MediaTek UI 控制器
// MediaTek UI Controller
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LoveAlways.MediaTek.Common;
using LoveAlways.MediaTek.Database;
using LoveAlways.MediaTek.Exploit;
using LoveAlways.MediaTek.Models;
using LoveAlways.MediaTek.Protocol;
using LoveAlways.MediaTek.Services;

namespace LoveAlways.MediaTek.UI
{
    /// <summary>
    /// MediaTek UI 控制器
    /// </summary>
    public class MediatekUIController : IDisposable
    {
        private readonly MediatekService _service;
        private readonly MtkPortDetector _portDetector;
        private readonly Action<string, Color> _logCallback;
        private readonly Action<string> _detailLogCallback;
        private CancellationTokenSource _operationCts;

        // 事件
        public event Action<int, int> OnProgress;
        public event Action<MtkDeviceState> OnStateChanged;
        public event Action<MtkDeviceInfo> OnDeviceConnected;
        public event Action<MtkDeviceInfo> OnDeviceDisconnected;
        public event Action<List<MtkPartitionInfo>> OnPartitionTableLoaded;

        // 属性
        public bool IsConnected => _service.IsConnected;
        public bool IsBromMode => _service.IsBromMode;
        public MtkDeviceState State => _service.State;
        public MtkChipInfo ChipInfo => _service.ChipInfo;
        public MtkDeviceInfo CurrentDevice => _service.CurrentDevice;

        // 缓存的分区表
        public List<MtkPartitionInfo> CachedPartitions { get; private set; }

        // 端口检测事件
        public event Action<MtkPortInfo> OnPortDetected;
        public event Action<string> OnPortRemoved;

        public MediatekUIController(Action<string, Color> logCallback, Action<string> detailLogCallback = null)
        {
            _logCallback = logCallback;
            _detailLogCallback = detailLogCallback;

            _service = new MediatekService();
            _service.OnLog += Log;
            _service.OnProgress += (c, t) => OnProgress?.Invoke(c, t);
            _service.OnStateChanged += state => OnStateChanged?.Invoke(state);
            _service.OnDeviceConnected += dev => OnDeviceConnected?.Invoke(dev);
            _service.OnDeviceDisconnected += dev => OnDeviceDisconnected?.Invoke(dev);

            // 初始化端口检测器
            _portDetector = new MtkPortDetector(msg => Log(msg, Color.Gray));
            _portDetector.OnDeviceArrived += port =>
            {
                Log($"[MTK] 检测到设备: {port.ComPort} ({port.Description})", Color.Cyan);
                OnPortDetected?.Invoke(port);
            };
            _portDetector.OnDeviceRemoved += portName =>
            {
                Log($"[MTK] 设备移除: {portName}", Color.Orange);
                OnPortRemoved?.Invoke(portName);
            };
        }

        #region 端口检测

        /// <summary>
        /// 获取所有 MTK 设备端口
        /// </summary>
        public List<MtkPortInfo> GetMtkPorts()
        {
            return _portDetector.GetMtkPorts();
        }

        /// <summary>
        /// 启动设备监控
        /// </summary>
        public void StartPortMonitoring()
        {
            _portDetector.StartMonitoring();
            Log("[MTK] 设备监控已启动", Color.Gray);
        }

        /// <summary>
        /// 停止设备监控
        /// </summary>
        public void StopPortMonitoring()
        {
            _portDetector.StopMonitoring();
        }

        /// <summary>
        /// 等待 MTK 设备连接
        /// </summary>
        public async Task<MtkPortInfo> WaitForDeviceAsync(int timeoutMs = 60000)
        {
            ResetOperationCts();
            return await _portDetector.WaitForDeviceAsync(timeoutMs, _operationCts.Token);
        }

        /// <summary>
        /// 等待 BROM 设备连接
        /// </summary>
        public async Task<MtkPortInfo> WaitForBromDeviceAsync(int timeoutMs = 60000)
        {
            ResetOperationCts();
            return await _portDetector.WaitForBromDeviceAsync(timeoutMs, _operationCts.Token);
        }

        /// <summary>
        /// 自动连接第一个检测到的设备
        /// </summary>
        public async Task<bool> AutoConnectAsync(int waitTimeoutMs = 60000)
        {
            Log("[MTK] 等待设备连接...", Color.Cyan);

            var port = await WaitForDeviceAsync(waitTimeoutMs);
            if (port == null)
            {
                Log("[MTK] 未检测到设备", Color.Red);
                return false;
            }

            return await ConnectDeviceAsync(port.ComPort);
        }

        #endregion

        #region 设备连接

        /// <summary>
        /// 连接设备
        /// </summary>
        public async Task<bool> ConnectDeviceAsync(string comPort, int baudRate = 115200)
        {
            ResetOperationCts();
            return await _service.ConnectAsync(comPort, baudRate, _operationCts.Token);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _operationCts?.Cancel();
            _service.Disconnect();
            CachedPartitions = null;
        }

        #endregion

        #region DA 加载

        /// <summary>
        /// 设置 DA 文件路径
        /// </summary>
        public void SetDaFilePath(string filePath)
        {
            _service.SetDaFilePath(filePath);
        }

        /// <summary>
        /// 设置自定义 DA1
        /// </summary>
        public void SetCustomDa1(string filePath)
        {
            _service.SetCustomDa1(filePath);
        }

        /// <summary>
        /// 设置自定义 DA2
        /// </summary>
        public void SetCustomDa2(string filePath)
        {
            _service.SetCustomDa2(filePath);
        }

        /// <summary>
        /// 加载 DA
        /// </summary>
        public async Task<bool> LoadDaAsync()
        {
            ResetOperationCts();
            return await _service.LoadDaAsync(_operationCts.Token);
        }

        /// <summary>
        /// 连接并加载 DA (一键操作)
        /// </summary>
        public async Task<bool> ConnectAndLoadDaAsync(string comPort)
        {
            ResetOperationCts();

            Log("[MTK] 连接设备并加载 DA...", Color.Cyan);

            if (!await _service.ConnectAsync(comPort, 115200, _operationCts.Token))
            {
                return false;
            }

            return await _service.LoadDaAsync(_operationCts.Token);
        }

        #endregion

        #region 分区操作

        /// <summary>
        /// 读取分区表
        /// </summary>
        public async Task ReadPartitionTableAsync()
        {
            ResetOperationCts();

            var partitions = await _service.ReadPartitionTableAsync(_operationCts.Token);
            if (partitions != null)
            {
                CachedPartitions = partitions;
                OnPartitionTableLoaded?.Invoke(partitions);
            }
        }

        /// <summary>
        /// 读取分区到文件
        /// </summary>
        public async Task<bool> ReadPartitionAsync(string partitionName, string outputPath, ulong size)
        {
            ResetOperationCts();
            return await _service.ReadPartitionAsync(partitionName, outputPath, size, _operationCts.Token);
        }

        /// <summary>
        /// 写入分区
        /// </summary>
        public async Task<bool> WritePartitionAsync(string partitionName, string filePath)
        {
            ResetOperationCts();
            return await _service.WritePartitionAsync(partitionName, filePath, _operationCts.Token);
        }

        /// <summary>
        /// 擦除分区
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName)
        {
            ResetOperationCts();
            return await _service.ErasePartitionAsync(partitionName, _operationCts.Token);
        }

        /// <summary>
        /// 批量刷写
        /// </summary>
        public async Task<bool> FlashMultipleAsync(Dictionary<string, string> partitionFiles)
        {
            ResetOperationCts();

            Log("========================================", Color.White);
            Log("[MTK] 开始刷机流程", Color.Cyan);
            Log("========================================", Color.White);

            bool result = await _service.FlashMultipleAsync(partitionFiles, _operationCts.Token);

            if (result)
            {
                Log("========================================", Color.Green);
                Log("[MTK] 刷机完成！", Color.Green);
                Log("========================================", Color.Green);
            }
            else
            {
                Log("========================================", Color.Red);
                Log("[MTK] 刷机失败", Color.Red);
                Log("========================================", Color.Red);
            }

            return result;
        }

        /// <summary>
        /// 获取分区大小
        /// </summary>
        public ulong GetPartitionSize(string partitionName)
        {
            if (CachedPartitions == null)
                return 0;

            var partition = CachedPartitions.FirstOrDefault(p =>
                p.Name.Equals(partitionName, StringComparison.OrdinalIgnoreCase));

            return partition?.Size ?? 0;
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task RebootDeviceAsync()
        {
            await _service.RebootAsync();
        }

        /// <summary>
        /// 关闭设备
        /// </summary>
        public async Task ShutdownDeviceAsync()
        {
            await _service.ShutdownAsync();
        }

        /// <summary>
        /// 获取 Flash 信息
        /// </summary>
        public async Task<MtkFlashInfo> GetFlashInfoAsync()
        {
            var info = await _service.GetFlashInfoAsync();
            if (info != null)
            {
                Log($"[MTK] Flash: {info.FlashType} {info.CapacityDisplay}", Color.Cyan);
            }
            return info;
        }

        #endregion

        #region 安全功能

        /// <summary>
        /// 检测漏洞
        /// </summary>
        public bool CheckVulnerability()
        {
            bool isVulnerable = _service.CheckVulnerability();
            if (isVulnerable)
            {
                Log("[MTK] ✓ 设备存在 Carbonara 漏洞，可以绕过签名验证", Color.Yellow);
            }
            else
            {
                Log("[MTK] 设备不存在已知漏洞", Color.Gray);
            }
            return isVulnerable;
        }

        /// <summary>
        /// 获取安全信息
        /// </summary>
        public MtkSecurityInfo GetSecurityInfo()
        {
            var info = _service.GetSecurityInfo();
            if (info != null)
            {
                Log($"[MTK] Secure Boot: {(info.SecureBootEnabled ? "启用" : "禁用")}", Color.Cyan);
                Log($"[MTK] SLA: {(info.SlaEnabled ? "启用" : "禁用")}", Color.Cyan);
                Log($"[MTK] DAA: {(info.DaaEnabled ? "启用" : "禁用")}", Color.Cyan);
                if (!string.IsNullOrEmpty(info.MeId))
                    Log($"[MTK] ME ID: {info.MeId}", Color.Gray);
            }
            return info;
        }

        #endregion

        #region 芯片数据库

        /// <summary>
        /// 获取所有支持的芯片
        /// </summary>
        public string[] GetSupportedChips()
        {
            return MtkChipDatabase.GetAllChips()
                .Select(c => $"{c.ChipName} (0x{c.HwCode:X4})")
                .ToArray();
        }

        /// <summary>
        /// 获取支持漏洞利用的芯片
        /// </summary>
        public string[] GetExploitableChips()
        {
            return MtkChipDatabase.GetExploitableChips()
                .Select(c => $"{c.ChipName} (0x{c.HwCode:X4})")
                .ToArray();
        }

        /// <summary>
        /// 获取芯片信息
        /// </summary>
        public MtkChipRecord GetChipRecord(ushort hwCode)
        {
            return MtkChipDatabase.GetChip(hwCode);
        }

        /// <summary>
        /// 获取数据库统计
        /// </summary>
        public (int chipCount, int exploitCount) GetDatabaseStats()
        {
            var chips = MtkChipDatabase.GetAllChips();
            var exploitable = MtkChipDatabase.GetExploitableChips();
            return (chips.Count, exploitable.Count);
        }

        #endregion

        #region ALLINONE-SIGNATURE 漏洞利用

        /// <summary>
        /// 获取支持 ALLINONE-SIGNATURE 漏洞的芯片列表
        /// </summary>
        public string[] GetAllinoneSignatureChips()
        {
            return MtkChipDatabase.GetAllinoneSignatureChips()
                .Select(c => $"{c.ChipName} - {c.Description} (0x{c.HwCode:X4})")
                .ToArray();
        }

        /// <summary>
        /// 检查当前连接的设备是否支持 ALLINONE-SIGNATURE 漏洞
        /// </summary>
        public bool IsCurrentDeviceAllinoneSignatureSupported()
        {
            if (!IsConnected || ChipInfo == null)
                return false;

            return MtkChipDatabase.IsAllinoneSignatureSupported(ChipInfo.HwCode);
        }

        /// <summary>
        /// 获取当前设备的漏洞类型
        /// </summary>
        public string GetCurrentDeviceExploitType()
        {
            if (!IsConnected || ChipInfo == null)
                return "None";

            return MtkChipDatabase.GetExploitType(ChipInfo.HwCode);
        }

        /// <summary>
        /// 执行 ALLINONE-SIGNATURE 漏洞利用
        /// 仅适用于 MT6989/MT6983/MT6985 等支持此漏洞的芯片
        /// </summary>
        /// <param name="shellcodePath">Shellcode 文件路径 (可选, 默认使用内置)</param>
        /// <param name="pointerTablePath">指针表文件路径 (可选, 默认自动生成)</param>
        /// <returns>是否成功</returns>
        public async Task<bool> RunAllinoneSignatureExploitAsync(
            string shellcodePath = null,
            string pointerTablePath = null)
        {
            // 检查设备是否支持此漏洞
            if (!IsCurrentDeviceAllinoneSignatureSupported())
            {
                string chipName = ChipInfo?.ChipName ?? "Unknown";
                ushort hwCode = ChipInfo?.HwCode ?? 0;
                Log($"[MTK] 当前设备 {chipName} (0x{hwCode:X4}) 不支持 ALLINONE-SIGNATURE 漏洞", Color.Red);
                Log("[MTK] 此漏洞仅适用于以下芯片:", Color.Yellow);
                foreach (var chip in GetAllinoneSignatureChips())
                {
                    Log($"[MTK]   • {chip}", Color.Yellow);
                }
                return false;
            }

            ResetOperationCts();

            Log("[MTK] ═══════════════════════════════════════", Color.Yellow);
            Log($"[MTK] 开始执行 ALLINONE-SIGNATURE 漏洞利用", Color.Yellow);
            Log($"[MTK] 目标芯片: {ChipInfo?.ChipName} (0x{ChipInfo?.HwCode:X4})", Color.Yellow);
            Log("[MTK] ═══════════════════════════════════════", Color.Yellow);

            try
            {
                bool success = await _service.RunAllinoneSignatureExploitAsync(
                    shellcodePath,
                    pointerTablePath,
                    _operationCts.Token);

                if (success)
                {
                    Log("[MTK] ✓ ALLINONE-SIGNATURE 漏洞利用成功!", Color.Green);
                    Log("[MTK] 设备安全检查已禁用, 现在可以执行任意操作", Color.Green);
                }
                else
                {
                    Log("[MTK] ✗ ALLINONE-SIGNATURE 漏洞利用失败", Color.Red);
                }

                return success;
            }
            catch (OperationCanceledException)
            {
                Log("[MTK] 漏洞利用操作已取消", Color.Orange);
                return false;
            }
            catch (Exception ex)
            {
                Log($"[MTK] 漏洞利用异常: {ex.Message}", Color.Red);
                return false;
            }
        }

        /// <summary>
        /// 获取漏洞利用信息
        /// </summary>
        public MtkExploitInfo GetExploitInfo()
        {
            var info = new MtkExploitInfo
            {
                IsConnected = IsConnected,
                ChipName = ChipInfo?.ChipName ?? "Unknown",
                HwCode = ChipInfo?.HwCode ?? 0,
                ExploitType = GetCurrentDeviceExploitType(),
                IsAllinoneSignatureSupported = IsCurrentDeviceAllinoneSignatureSupported(),
                IsCarbonaraSupported = _service.CheckVulnerability()
            };

            // 获取支持的漏洞芯片列表
            info.AllinoneSignatureChips = MtkChipDatabase.GetAllinoneSignatureChips()
                .Select(c => new MtkChipExploitInfo
                {
                    ChipName = c.ChipName,
                    HwCode = c.HwCode,
                    Description = c.Description
                }).ToArray();

            return info;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 取消当前操作
        /// </summary>
        public void CancelOperation()
        {
            _operationCts?.Cancel();
            Log("[MTK] 操作已取消", Color.Orange);
        }

        private void Log(string message, Color color)
        {
            _logCallback?.Invoke(message, color);
            _detailLogCallback?.Invoke(message);
        }

        private void ResetOperationCts()
        {
            if (_operationCts != null)
            {
                try { _operationCts.Cancel(); } catch { /* 取消可能已完成，忽略 */ }
                try { _operationCts.Dispose(); } catch { /* 释放失败可忽略 */ }
            }
            _operationCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _operationCts?.Cancel();
            _portDetector?.Dispose();
            _service?.Dispose();
        }

        #endregion
    }
}
