using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LoveAlways.Fastboot.Common;
using LoveAlways.Fastboot.Models;

namespace LoveAlways.Fastboot.Services
{
    /// <summary>
    /// Fastboot 服务层
    /// 提供设备检测、分区操作、刷写等功能
    /// </summary>
    public class FastbootService : IDisposable
    {
        private readonly Action<string> _log;
        private readonly Action<string> _logDetail;
        private readonly Action<int, int> _progress;

        private string _currentSerial;
        private FastbootDeviceInfo _deviceInfo;
        private bool _disposed;

        /// <summary>
        /// 当前连接的设备序列号
        /// </summary>
        public string CurrentSerial => _currentSerial;

        /// <summary>
        /// 当前设备信息
        /// </summary>
        public FastbootDeviceInfo DeviceInfo => _deviceInfo;

        /// <summary>
        /// 是否已连接设备
        /// </summary>
        public bool IsConnected => !string.IsNullOrEmpty(_currentSerial);

        public FastbootService(Action<string> log, Action<int, int> progress = null, Action<string> logDetail = null)
        {
            _log = log ?? (msg => { });
            _progress = progress;
            _logDetail = logDetail ?? (msg => { });
        }

        #region 设备检测

        /// <summary>
        /// 获取 Fastboot 设备列表
        /// </summary>
        public async Task<List<FastbootDeviceListItem>> GetDevicesAsync(CancellationToken ct = default)
        {
            var devices = new List<FastbootDeviceListItem>();

            try
            {
                var result = await FastbootCommand.ExecuteAsync(null, "devices", ct);
                
                if (!string.IsNullOrEmpty(result.StdOut))
                {
                    foreach (string line in result.StdOut.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            devices.Add(new FastbootDeviceListItem
                            {
                                Serial = parts[0],
                                Status = parts[1]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logDetail($"[Fastboot] 获取设备列表失败: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// 选择设备并获取设备信息
        /// </summary>
        public async Task<bool> SelectDeviceAsync(string serial, CancellationToken ct = default)
        {
            _currentSerial = serial;
            _log($"[Fastboot] 选择设备: {serial}");

            return await RefreshDeviceInfoAsync(ct);
        }

        /// <summary>
        /// 刷新设备信息
        /// </summary>
        public async Task<bool> RefreshDeviceInfoAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_currentSerial))
            {
                _log("[Fastboot] 未选择设备");
                return false;
            }

            try
            {
                _log("[Fastboot] 正在读取设备信息...");

                var result = await FastbootCommand.ExecuteAsync(_currentSerial, "getvar all", ct);
                
                // Fastboot 的 getvar 输出通常在 stderr
                string output = string.IsNullOrEmpty(result.StdErr) ? result.StdOut : result.StdErr;
                
                _deviceInfo = FastbootDeviceInfo.ParseFromGetvarAll(output);
                _deviceInfo.Serial = _currentSerial;

                _log($"[Fastboot] 设备: {_deviceInfo.Product ?? "未知"}");
                _log($"[Fastboot] 安全启动: {(_deviceInfo.SecureBoot ? "启用" : "禁用")}");
                
                if (_deviceInfo.HasABPartition)
                {
                    _log($"[Fastboot] 当前槽位: {_deviceInfo.CurrentSlot}");
                }

                _log($"[Fastboot] Fastbootd 模式: {(_deviceInfo.IsFastbootd ? "是" : "否")}");
                _log($"[Fastboot] 分区数量: {_deviceInfo.PartitionSizes.Count}");

                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 读取设备信息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 断开设备
        /// </summary>
        public void Disconnect()
        {
            _currentSerial = null;
            _deviceInfo = null;
        }

        #endregion

        #region 分区操作

        /// <summary>
        /// 刷写分区
        /// </summary>
        public async Task<bool> FlashPartitionAsync(string partitionName, string imagePath, 
            bool disableVerity = false, CancellationToken ct = default)
        {
            if (!IsConnected)
            {
                _log("[Fastboot] 未连接设备");
                return false;
            }

            if (!File.Exists(imagePath))
            {
                _log($"[Fastboot] 镜像文件不存在: {imagePath}");
                return false;
            }

            try
            {
                _log($"[Fastboot] 正在刷写 {partitionName}...");

                string extraArgs = "";
                
                // 对于 vbmeta 分区，可以禁用 verity
                if (disableVerity && (partitionName.StartsWith("vbmeta")))
                {
                    extraArgs = "--disable-verity --disable-verification ";
                }

                string cmd = $"flash {extraArgs}\"{partitionName}\" \"{imagePath}\"";
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, cmd, ct, 
                    line => _logDetail($"[Fastboot] {line}"));

                if (result.Success || result.StdErr.Contains("OKAY") || result.StdErr.Contains("Finished"))
                {
                    _log($"[Fastboot] {partitionName} 刷写成功");
                    return true;
                }
                else
                {
                    _log($"[Fastboot] {partitionName} 刷写失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 刷写异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 擦除分区
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName, CancellationToken ct = default)
        {
            if (!IsConnected)
            {
                _log("[Fastboot] 未连接设备");
                return false;
            }

            try
            {
                _log($"[Fastboot] 正在擦除 {partitionName}...");

                var result = await FastbootCommand.ExecuteAsync(_currentSerial, $"erase \"{partitionName}\"", ct);

                if (result.Success || result.StdErr.Contains("OKAY"))
                {
                    _log($"[Fastboot] {partitionName} 擦除成功");
                    return true;
                }
                else
                {
                    _log($"[Fastboot] {partitionName} 擦除失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 擦除异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量刷写分区
        /// </summary>
        public async Task<int> FlashPartitionsBatchAsync(List<Tuple<string, string>> partitions, 
            CancellationToken ct = default)
        {
            int success = 0;
            int total = partitions.Count;

            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (partName, imagePath) = partitions[i];
                
                _progress?.Invoke(i, total);
                
                if (await FlashPartitionAsync(partName, imagePath, false, ct))
                {
                    success++;
                }
            }

            _progress?.Invoke(total, total);
            return success;
        }

        #endregion

        #region 重启操作

        /// <summary>
        /// 重启到系统
        /// </summary>
        public async Task<bool> RebootAsync(CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log("[Fastboot] 正在重启到系统...");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, "reboot", ct);
                Disconnect();
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 重启失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重启到 Bootloader
        /// </summary>
        public async Task<bool> RebootBootloaderAsync(CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log("[Fastboot] 正在重启到 Bootloader...");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, "reboot bootloader", ct);
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 重启失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重启到 Fastbootd (用户空间 fastboot)
        /// </summary>
        public async Task<bool> RebootFastbootdAsync(CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log("[Fastboot] 正在重启到 Fastbootd...");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, "reboot fastboot", ct);
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 重启失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重启到 Recovery
        /// </summary>
        public async Task<bool> RebootRecoveryAsync(CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log("[Fastboot] 正在重启到 Recovery...");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, "reboot recovery", ct);
                Disconnect();
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 重启失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 解锁/锁定操作

        /// <summary>
        /// 解锁 Bootloader
        /// </summary>
        public async Task<bool> UnlockBootloaderAsync(string method = "flashing unlock", CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log($"[Fastboot] 正在执行: {method}");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, method, ct);
                
                _log($"[Fastboot] 输出: {result.StdErr}");
                
                // 解锁命令可能需要用户在设备上确认
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 解锁失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 锁定 Bootloader
        /// </summary>
        public async Task<bool> LockBootloaderAsync(string method = "flashing lock", CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log($"[Fastboot] 正在执行: {method}");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, method, ct);
                
                _log($"[Fastboot] 输出: {result.StdErr}");
                return true;
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 锁定失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region A/B 槽位操作

        /// <summary>
        /// 切换 A/B 槽位
        /// </summary>
        public async Task<bool> SetActiveSlotAsync(string slot, CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log($"[Fastboot] 正在切换到槽位: {slot}");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, $"set_active {slot}", ct);

                if (result.Success || result.StdErr.Contains("OKAY"))
                {
                    _log($"[Fastboot] 槽位切换成功");
                    await RefreshDeviceInfoAsync(ct);
                    return true;
                }
                else
                {
                    _log($"[Fastboot] 槽位切换失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 槽位切换异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 切换到另一个槽位
        /// </summary>
        public async Task<bool> SwitchSlotAsync(CancellationToken ct = default)
        {
            if (_deviceInfo == null || !_deviceInfo.HasABPartition)
            {
                _log("[Fastboot] 设备不支持 A/B 分区");
                return false;
            }

            string newSlot = _deviceInfo.CurrentSlot == "a" ? "b" : "a";
            return await SetActiveSlotAsync(newSlot, ct);
        }

        #endregion

        #region 逻辑分区操作 (Fastbootd)

        /// <summary>
        /// 创建逻辑分区
        /// </summary>
        public async Task<bool> CreateLogicalPartitionAsync(string name, long size, CancellationToken ct = default)
        {
            if (!IsConnected || _deviceInfo == null || !_deviceInfo.IsFastbootd)
            {
                _log("[Fastboot] 需要在 Fastbootd 模式下操作逻辑分区");
                return false;
            }

            try
            {
                _log($"[Fastboot] 正在创建逻辑分区: {name} ({size} bytes)");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, 
                    $"create-logical-partition \"{name}\" {size}", ct);

                if (result.Success || result.StdErr.Contains("OKAY"))
                {
                    _log($"[Fastboot] 逻辑分区 {name} 创建成功");
                    return true;
                }
                else
                {
                    _log($"[Fastboot] 创建失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 创建逻辑分区异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除逻辑分区
        /// </summary>
        public async Task<bool> DeleteLogicalPartitionAsync(string name, CancellationToken ct = default)
        {
            if (!IsConnected || _deviceInfo == null || !_deviceInfo.IsFastbootd)
            {
                _log("[Fastboot] 需要在 Fastbootd 模式下操作逻辑分区");
                return false;
            }

            try
            {
                _log($"[Fastboot] 正在删除逻辑分区: {name}");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, 
                    $"delete-logical-partition \"{name}\"", ct);

                if (result.Success || result.StdErr.Contains("OKAY"))
                {
                    _log($"[Fastboot] 逻辑分区 {name} 删除成功");
                    return true;
                }
                else
                {
                    _log($"[Fastboot] 删除失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 删除逻辑分区异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 调整逻辑分区大小
        /// </summary>
        public async Task<bool> ResizeLogicalPartitionAsync(string name, long newSize, CancellationToken ct = default)
        {
            if (!IsConnected || _deviceInfo == null || !_deviceInfo.IsFastbootd)
            {
                _log("[Fastboot] 需要在 Fastbootd 模式下操作逻辑分区");
                return false;
            }

            try
            {
                _log($"[Fastboot] 正在调整逻辑分区大小: {name} -> {newSize} bytes");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, 
                    $"resize-logical-partition \"{name}\" {newSize}", ct);

                if (result.Success || result.StdErr.Contains("OKAY"))
                {
                    _log($"[Fastboot] 逻辑分区 {name} 大小调整成功");
                    return true;
                }
                else
                {
                    _log($"[Fastboot] 调整失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 调整逻辑分区大小异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 其他操作

        /// <summary>
        /// 执行自定义 Fastboot 命令
        /// </summary>
        public async Task<FastbootResult> ExecuteCommandAsync(string command, CancellationToken ct = default)
        {
            _log($"[Fastboot] 执行: {command}");
            var result = await FastbootCommand.ExecuteAsync(_currentSerial, command, ct);
            
            if (!string.IsNullOrEmpty(result.StdErr))
            {
                _log($"[Fastboot] {result.StdErr}");
            }

            return result;
        }

        /// <summary>
        /// 取消快照更新状态
        /// </summary>
        public async Task<bool> CancelSnapshotUpdateAsync(CancellationToken ct = default)
        {
            if (!IsConnected) return false;

            try
            {
                _log("[Fastboot] 正在取消快照更新状态...");
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, "snapshot-update cancel", ct);

                if (result.Success || result.StdErr.Contains("OKAY"))
                {
                    _log("[Fastboot] 快照更新状态已取消");
                    return true;
                }
                else
                {
                    _log($"[Fastboot] 取消失败: {result.StdErr}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log($"[Fastboot] 取消快照更新异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取单个变量值
        /// </summary>
        public async Task<string> GetVariableAsync(string name, CancellationToken ct = default)
        {
            if (!IsConnected) return null;

            try
            {
                var result = await FastbootCommand.ExecuteAsync(_currentSerial, $"getvar {name}", ct);
                
                // 解析输出，格式: name: value
                string output = result.StdErr ?? result.StdOut;
                if (string.IsNullOrEmpty(output)) return null;

                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains(name + ":"))
                    {
                        int idx = line.IndexOf(':');
                        if (idx > 0)
                        {
                            return line.Substring(idx + 1).Trim();
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
    }
}
