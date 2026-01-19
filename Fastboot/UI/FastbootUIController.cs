using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoveAlways.Fastboot.Common;
using LoveAlways.Fastboot.Models;
using LoveAlways.Fastboot.Services;

namespace LoveAlways.Fastboot.UI
{
    /// <summary>
    /// Fastboot UI 控制器
    /// 负责连接 UI 控件与 Fastboot 服务
    /// </summary>
    public class FastbootUIController : IDisposable
    {
        private readonly Action<string, Color?> _log;
        private readonly Action<string> _logDetail;

        private FastbootService _service;
        private CancellationTokenSource _cts;
        private System.Windows.Forms.Timer _deviceRefreshTimer;
        private bool _disposed;

        // UI 控件绑定
        private dynamic _portComboBox;        // 设备选择下拉框
        private dynamic _partitionListView;   // 分区列表
        private dynamic _progressBar;         // 进度条
        private dynamic _commandComboBox;     // 快捷命令下拉框
        private dynamic _payloadTextBox;      // Payload 路径
        private dynamic _outputPathTextBox;   // 输出路径

        // Checkbox 控件
        private dynamic _autoRebootCheckbox;      // 自动重启
        private dynamic _switchSlotCheckbox;      // 切换A槽
        private dynamic _eraseGoogleLockCheckbox; // 擦除谷歌锁
        private dynamic _keepDataCheckbox;        // 保留数据
        private dynamic _fbdFlashCheckbox;        // FBD刷写
        private dynamic _unlockBlCheckbox;        // 解锁BL
        private dynamic _lockBlCheckbox;          // 锁定BL

        // 状态
        public bool IsBusy { get; private set; }
        public bool IsConnected => _service?.IsConnected ?? false;
        public FastbootDeviceInfo DeviceInfo => _service?.DeviceInfo;
        public List<FastbootPartitionInfo> Partitions => _service?.DeviceInfo?.GetPartitions();

        // 事件
        public event EventHandler<bool> ConnectionStateChanged;
        public event EventHandler<List<FastbootPartitionInfo>> PartitionsLoaded;
        public event EventHandler<List<FastbootDeviceListItem>> DevicesRefreshed;

        public FastbootUIController(Action<string, Color?> log, Action<string> logDetail = null)
        {
            _log = log ?? ((msg, color) => { });
            _logDetail = logDetail ?? (msg => { });

            // 初始化设备刷新定时器
            _deviceRefreshTimer = new System.Windows.Forms.Timer();
            _deviceRefreshTimer.Interval = 2000; // 每 2 秒刷新一次
            _deviceRefreshTimer.Tick += async (s, e) => await RefreshDeviceListAsync();
        }

        #region 日志方法

        private void Log(string message, Color? color = null)
        {
            _log(message, color);
        }

        #endregion

        #region 控件绑定

        /// <summary>
        /// 绑定 UI 控件
        /// </summary>
        public void BindControls(
            object portComboBox = null,
            object partitionListView = null,
            object progressBar = null,
            object commandComboBox = null,
            object payloadTextBox = null,
            object outputPathTextBox = null,
            object autoRebootCheckbox = null,
            object switchSlotCheckbox = null,
            object eraseGoogleLockCheckbox = null,
            object keepDataCheckbox = null,
            object fbdFlashCheckbox = null,
            object unlockBlCheckbox = null,
            object lockBlCheckbox = null)
        {
            _portComboBox = portComboBox;
            _partitionListView = partitionListView;
            _progressBar = progressBar;
            _commandComboBox = commandComboBox;
            _payloadTextBox = payloadTextBox;
            _outputPathTextBox = outputPathTextBox;
            _autoRebootCheckbox = autoRebootCheckbox;
            _switchSlotCheckbox = switchSlotCheckbox;
            _eraseGoogleLockCheckbox = eraseGoogleLockCheckbox;
            _keepDataCheckbox = keepDataCheckbox;
            _fbdFlashCheckbox = fbdFlashCheckbox;
            _unlockBlCheckbox = unlockBlCheckbox;
            _lockBlCheckbox = lockBlCheckbox;

            // 初始化分区列表
            if (_partitionListView != null)
            {
                try
                {
                    _partitionListView.CheckBoxes = true;
                    _partitionListView.FullRowSelect = true;
                    _partitionListView.MultiSelect = true;
                }
                catch { }
            }
        }

        /// <summary>
        /// 启动设备监控
        /// </summary>
        public void StartDeviceMonitoring()
        {
            _deviceRefreshTimer.Start();
            Task.Run(() => RefreshDeviceListAsync());
        }

        /// <summary>
        /// 停止设备监控
        /// </summary>
        public void StopDeviceMonitoring()
        {
            _deviceRefreshTimer.Stop();
        }

        #endregion

        #region 设备操作

        /// <summary>
        /// 刷新设备列表
        /// </summary>
        public async Task RefreshDeviceListAsync()
        {
            try
            {
                using (var tempService = new FastbootService(msg => { }))
                {
                    var devices = await tempService.GetDevicesAsync();
                    
                    // 在 UI 线程更新
                    if (_portComboBox != null)
                    {
                        try
                        {
                            if (_portComboBox.InvokeRequired)
                            {
                                _portComboBox.BeginInvoke(new Action(() => UpdateDeviceComboBox(devices)));
                            }
                            else
                            {
                                UpdateDeviceComboBox(devices);
                            }
                        }
                        catch { }
                    }

                    DevicesRefreshed?.Invoke(this, devices);
                }
            }
            catch (Exception ex)
            {
                _logDetail($"[Fastboot] 刷新设备列表异常: {ex.Message}");
            }
        }

        private void UpdateDeviceComboBox(List<FastbootDeviceListItem> devices)
        {
            if (_portComboBox == null) return;

            try
            {
                string currentSelection = null;
                try { currentSelection = _portComboBox.SelectedItem?.ToString(); } catch { }

                _portComboBox.Items.Clear();
                foreach (var device in devices)
                {
                    _portComboBox.Items.Add(device.ToString());
                }

                // 尝试恢复之前的选择
                if (!string.IsNullOrEmpty(currentSelection) && _portComboBox.Items.Contains(currentSelection))
                {
                    _portComboBox.SelectedItem = currentSelection;
                }
                else if (_portComboBox.Items.Count > 0)
                {
                    _portComboBox.SelectedIndex = 0;
                }
            }
            catch { }
        }

        /// <summary>
        /// 连接选中的设备
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            if (IsBusy)
            {
                Log("操作进行中", Color.Orange);
                return false;
            }

            string selectedDevice = GetSelectedDevice();
            if (string.IsNullOrEmpty(selectedDevice))
            {
                Log("请选择 Fastboot 设备", Color.Red);
                return false;
            }

            // 从 "serial (status)" 格式提取序列号
            string serial = selectedDevice.Split(' ')[0];

            try
            {
                IsBusy = true;
                _cts = new CancellationTokenSource();

                _service = new FastbootService(
                    msg => Log(msg, null),
                    (current, total) => UpdateProgress(current, total),
                    _logDetail
                );

                bool success = await _service.SelectDeviceAsync(serial, _cts.Token);

                if (success)
                {
                    Log("Fastboot 设备连接成功", Color.Green);
                    
                    // 更新分区列表
                    UpdatePartitionListView();
                    
                    ConnectionStateChanged?.Invoke(this, true);
                    PartitionsLoaded?.Invoke(this, Partitions);
                }
                else
                {
                    Log("Fastboot 设备连接失败", Color.Red);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log($"连接异常: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _service?.Disconnect();
            ConnectionStateChanged?.Invoke(this, false);
        }

        private string GetSelectedDevice()
        {
            try
            {
                if (_portComboBox == null) return null;
                return _portComboBox.SelectedItem?.ToString();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 分区操作

        /// <summary>
        /// 读取分区表（刷新设备信息）
        /// </summary>
        public async Task<bool> ReadPartitionTableAsync()
        {
            if (!EnsureConnected()) return false;
            if (IsBusy) { Log("操作进行中", Color.Orange); return false; }

            try
            {
                IsBusy = true;
                _cts = new CancellationTokenSource();

                Log("正在读取 Fastboot 分区表...", Color.Blue);

                bool success = await _service.RefreshDeviceInfoAsync(_cts.Token);

                if (success)
                {
                    UpdatePartitionListView();
                    Log($"成功读取 {Partitions?.Count ?? 0} 个分区", Color.Green);
                    PartitionsLoaded?.Invoke(this, Partitions);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log($"读取分区表失败: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 更新分区列表视图
        /// </summary>
        private void UpdatePartitionListView()
        {
            if (_partitionListView == null || Partitions == null) return;

            try
            {
                if (_partitionListView.InvokeRequired)
                {
                    _partitionListView.BeginInvoke(new Action(UpdatePartitionListViewInternal));
                }
                else
                {
                    UpdatePartitionListViewInternal();
                }
            }
            catch { }
        }

        private void UpdatePartitionListViewInternal()
        {
            try
            {
                _partitionListView.Items.Clear();

                foreach (var part in Partitions)
                {
                    var item = new ListViewItem(new string[]
                    {
                        part.Name,
                        "-",  // 操作列
                        part.SizeFormatted,
                        part.IsLogicalText
                    });
                    item.Tag = part;
                    _partitionListView.Items.Add(item);
                }
            }
            catch { }
        }

        /// <summary>
        /// 刷写选中的分区
        /// </summary>
        public async Task<bool> FlashSelectedPartitionsAsync()
        {
            if (!EnsureConnected()) return false;
            if (IsBusy) { Log("操作进行中", Color.Orange); return false; }

            var selectedItems = GetSelectedPartitionItems();
            if (selectedItems.Count == 0)
            {
                Log("请选择要刷写的分区", Color.Orange);
                return false;
            }

            // 检查是否有镜像文件
            var partitionsWithFiles = new List<Tuple<string, string>>();
            foreach (ListViewItem item in selectedItems)
            {
                string partName = item.SubItems[0].Text;
                string filePath = item.SubItems.Count > 3 ? item.SubItems[3].Text : "";
                
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    Log($"分区 {partName} 没有选择镜像文件", Color.Orange);
                    continue;
                }

                partitionsWithFiles.Add(Tuple.Create(partName, filePath));
            }

            if (partitionsWithFiles.Count == 0)
            {
                Log("没有可刷写的分区（请双击分区选择镜像文件）", Color.Orange);
                return false;
            }

            try
            {
                IsBusy = true;
                _cts = new CancellationTokenSource();

                Log($"开始刷写 {partitionsWithFiles.Count} 个分区...", Color.Blue);

                int success = await _service.FlashPartitionsBatchAsync(partitionsWithFiles, _cts.Token);

                Log($"刷写完成: {success}/{partitionsWithFiles.Count} 成功", 
                    success == partitionsWithFiles.Count ? Color.Green : Color.Orange);

                // 自动重启
                if (IsAutoRebootEnabled() && success > 0)
                {
                    await _service.RebootAsync(_cts.Token);
                }

                return success == partitionsWithFiles.Count;
            }
            catch (Exception ex)
            {
                Log($"刷写失败: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 擦除选中的分区
        /// </summary>
        public async Task<bool> EraseSelectedPartitionsAsync()
        {
            if (!EnsureConnected()) return false;
            if (IsBusy) { Log("操作进行中", Color.Orange); return false; }

            var selectedItems = GetSelectedPartitionItems();
            if (selectedItems.Count == 0)
            {
                Log("请选择要擦除的分区", Color.Orange);
                return false;
            }

            try
            {
                IsBusy = true;
                _cts = new CancellationTokenSource();

                int success = 0;
                int total = selectedItems.Count;

                Log($"开始擦除 {total} 个分区...", Color.Blue);

                foreach (ListViewItem item in selectedItems)
                {
                    string partName = item.SubItems[0].Text;
                    
                    UpdateProgress(success, total);
                    
                    if (await _service.ErasePartitionAsync(partName, _cts.Token))
                    {
                        success++;
                    }
                }

                UpdateProgress(total, total);
                Log($"擦除完成: {success}/{total} 成功", 
                    success == total ? Color.Green : Color.Orange);

                return success == total;
            }
            catch (Exception ex)
            {
                Log($"擦除失败: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private List<ListViewItem> GetSelectedPartitionItems()
        {
            var items = new List<ListViewItem>();
            if (_partitionListView == null) return items;

            try
            {
                foreach (ListViewItem item in _partitionListView.CheckedItems)
                {
                    items.Add(item);
                }
            }
            catch { }

            return items;
        }

        /// <summary>
        /// 为分区选择镜像文件
        /// </summary>
        public void SelectImageForPartition(ListViewItem item)
        {
            if (item == null) return;

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = $"选择 {item.SubItems[0].Text} 分区镜像";
                ofd.Filter = "镜像文件|*.img;*.bin;*.mbn|所有文件|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // 更新操作列和文件路径列
                    if (item.SubItems.Count > 1)
                        item.SubItems[1].Text = "写入";
                    if (item.SubItems.Count > 3)
                        item.SubItems[3].Text = ofd.FileName;
                    else
                    {
                        while (item.SubItems.Count < 4)
                            item.SubItems.Add("");
                        item.SubItems[3].Text = ofd.FileName;
                    }

                    item.Checked = true;
                    Log($"已选择镜像: {Path.GetFileName(ofd.FileName)} -> {item.SubItems[0].Text}", Color.Blue);
                }
            }
        }

        #endregion

        #region 重启操作

        /// <summary>
        /// 重启到系统
        /// </summary>
        public async Task<bool> RebootToSystemAsync()
        {
            if (!EnsureConnected()) return false;
            return await _service.RebootAsync();
        }

        /// <summary>
        /// 重启到 Bootloader
        /// </summary>
        public async Task<bool> RebootToBootloaderAsync()
        {
            if (!EnsureConnected()) return false;
            return await _service.RebootBootloaderAsync();
        }

        /// <summary>
        /// 重启到 Fastbootd
        /// </summary>
        public async Task<bool> RebootToFastbootdAsync()
        {
            if (!EnsureConnected()) return false;
            return await _service.RebootFastbootdAsync();
        }

        /// <summary>
        /// 重启到 Recovery
        /// </summary>
        public async Task<bool> RebootToRecoveryAsync()
        {
            if (!EnsureConnected()) return false;
            return await _service.RebootRecoveryAsync();
        }

        #endregion

        #region 解锁/锁定

        /// <summary>
        /// 执行解锁操作
        /// </summary>
        public async Task<bool> UnlockBootloaderAsync()
        {
            if (!EnsureConnected()) return false;

            string method = "flashing unlock";
            
            // 根据 checkbox 状态选择解锁方法
            // 可以从 _commandComboBox 获取选择的命令
            string selectedCmd = GetSelectedCommand();
            if (!string.IsNullOrEmpty(selectedCmd) && selectedCmd.Contains("unlock"))
            {
                method = selectedCmd;
            }

            return await _service.UnlockBootloaderAsync(method);
        }

        /// <summary>
        /// 执行锁定操作
        /// </summary>
        public async Task<bool> LockBootloaderAsync()
        {
            if (!EnsureConnected()) return false;

            string method = "flashing lock";
            
            string selectedCmd = GetSelectedCommand();
            if (!string.IsNullOrEmpty(selectedCmd) && selectedCmd.Contains("lock"))
            {
                method = selectedCmd;
            }

            return await _service.LockBootloaderAsync(method);
        }

        #endregion

        #region A/B 槽位

        /// <summary>
        /// 切换 A/B 槽位
        /// </summary>
        public async Task<bool> SwitchSlotAsync()
        {
            if (!EnsureConnected()) return false;
            
            bool success = await _service.SwitchSlotAsync();
            
            if (success)
            {
                await ReadPartitionTableAsync();
            }

            return success;
        }

        #endregion

        #region 快捷命令

        /// <summary>
        /// 执行选中的快捷命令
        /// </summary>
        public async Task<bool> ExecuteSelectedCommandAsync()
        {
            if (!EnsureConnected()) return false;

            string command = GetSelectedCommand();
            if (string.IsNullOrEmpty(command))
            {
                Log("请选择要执行的命令", Color.Orange);
                return false;
            }

            try
            {
                IsBusy = true;
                _cts = new CancellationTokenSource();

                var result = await _service.ExecuteCommandAsync(command, _cts.Token);
                
                return result.Success;
            }
            catch (Exception ex)
            {
                Log($"命令执行失败: {ex.Message}", Color.Red);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string GetSelectedCommand()
        {
            try
            {
                if (_commandComboBox == null) return null;
                return _commandComboBox.SelectedItem?.ToString() ?? _commandComboBox.Text;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 辅助方法

        private bool EnsureConnected()
        {
            if (_service == null || !_service.IsConnected)
            {
                Log("请先连接 Fastboot 设备", Color.Red);
                return false;
            }
            return true;
        }

        private void UpdateProgress(int current, int total)
        {
            if (_progressBar == null) return;

            try
            {
                int percent = total > 0 ? (current * 100 / total) : 0;
                
                if (_progressBar.InvokeRequired)
                {
                    _progressBar.BeginInvoke(new Action(() =>
                    {
                        _progressBar.Value = percent;
                    }));
                }
                else
                {
                    _progressBar.Value = percent;
                }
            }
            catch { }
        }

        private bool IsAutoRebootEnabled()
        {
            try
            {
                return _autoRebootCheckbox?.Checked ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 取消当前操作
        /// </summary>
        public void CancelOperation()
        {
            _cts?.Cancel();
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                StopDeviceMonitoring();
                _deviceRefreshTimer?.Dispose();
                _service?.Dispose();
                _cts?.Dispose();
                _disposed = true;
            }
        }
    }
}
