// ============================================================================
// SakuraEDL - MediaTek Port Detector | 联发科端口检测器
// ============================================================================
// [ZH] MTK 端口检测 - 自动检测 BROM/Preloader 模式设备
// [EN] MTK Port Detector - Auto-detect BROM/Preloader mode devices
// [JA] MTKポート検出 - BROM/Preloaderモードデバイスの自動検出
// [KO] MTK 포트 탐지 - BROM/Preloader 모드 기기 자동 감지
// [RU] Детектор портов MTK - Автообнаружение устройств BROM/Preloader
// [ES] Detector de puertos MTK - Detección automática de dispositivos
// ============================================================================
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// MTK 端口检测器
    /// </summary>
    public class MtkPortDetector : IDisposable
    {
        private readonly Action<string> _log;
        private ManagementEventWatcher _insertWatcher;
        private ManagementEventWatcher _removeWatcher;
        private CancellationTokenSource _cts;
        private bool _isMonitoring;
        private bool _disposed;

        // MTK 设备 VID/PID
        private static readonly (int Vid, int Pid, string Description)[] MtkDeviceIds = new[]
        {
            (0x0E8D, 0x0003, "MTK BROM"),
            (0x0E8D, 0x2000, "MTK Preloader"),
            (0x0E8D, 0x2001, "MTK Preloader"),
            (0x0E8D, 0x0023, "MTK Composite"),
            (0x0E8D, 0x3000, "MTK SP Flash"),
            (0x0E8D, 0x0002, "MTK BROM Legacy"),
            (0x0E8D, 0x00A5, "MTK DA"),
            (0x0E8D, 0x00A2, "MTK DA"),
            (0x0E8D, 0x2006, "MTK CDC"),
            (0x1004, 0x6000, "LGE MTK"),    // LG MTK devices
            (0x22D9, 0x2766, "OPPO MTK"),   // OPPO MTK devices
            (0x2717, 0xFF40, "Xiaomi MTK"), // Xiaomi MTK devices
            (0x2A45, 0x0C02, "Meizu MTK"),  // Meizu MTK devices
        };

        // 事件
        public event Action<MtkPortInfo> OnDeviceArrived;
        public event Action<string> OnDeviceRemoved;

        public MtkPortDetector(Action<string> log = null)
        {
            _log = log ?? delegate { };
        }

        #region 端口检测

        /// <summary>
        /// 获取所有 MTK 设备端口
        /// </summary>
        public List<MtkPortInfo> GetMtkPorts()
        {
            var ports = new List<MtkPortInfo>();

            try
            {
                // 方法 1: 使用 WMI 查询 USB 设备
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string deviceId = obj["DeviceID"]?.ToString() ?? "";
                            string name = obj["Name"]?.ToString() ?? "";
                            string caption = obj["Caption"]?.ToString() ?? "";

                            // 检查是否为 MTK 设备
                            var portInfo = ParseMtkDevice(deviceId, name, caption);
                            if (portInfo != null)
                            {
                                ports.Add(portInfo);
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MTK Port] 检测端口异常: {ex.Message}"); }
                    }
                }

                // 方法 2: 扫描串口
                foreach (string portName in SerialPort.GetPortNames())
                {
                    if (!ports.Any(p => p.ComPort == portName))
                    {
                        var portInfo = ProbePort(portName);
                        if (portInfo != null)
                        {
                            ports.Add(portInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"[MTK Port] 端口检测错误: {ex.Message}");
            }

            return ports;
        }

        /// <summary>
        /// 解析 MTK 设备信息 - 双重验证 (VID + 设备名称)
        /// </summary>
        private MtkPortInfo ParseMtkDevice(string deviceId, string name, string caption)
        {
            string nameUpper = name.ToUpper();
            string captionUpper = caption.ToUpper();
            
            // ========== 第一步: 硬排除其他平台 ==========
            
            // 排除展讯设备 (VID 0x1782)
            if (deviceId.ToUpper().Contains("VID_1782"))
                return null;
            
            // 排除展讯设备关键字
            string[] sprdKeywords = { "SPRD", "SPREADTRUM", "UNISOC", "U2S DIAG", "SCI USB2SERIAL" };
            foreach (var kw in sprdKeywords)
            {
                if (nameUpper.Contains(kw) || captionUpper.Contains(kw))
                    return null;
            }
            
            // 排除高通设备 (VID 0x05C6)
            if (deviceId.ToUpper().Contains("VID_05C6"))
                return null;
            
            // 排除高通设备关键字
            string[] qcKeywords = { "QUALCOMM", "QDL", "QHSUSB", "QDLOADER" };
            foreach (var kw in qcKeywords)
            {
                if (nameUpper.Contains(kw) || captionUpper.Contains(kw))
                    return null;
            }
            
            // 排除 ADB/Fastboot
            if (nameUpper.Contains("ADB INTERFACE") || nameUpper.Contains("FASTBOOT"))
                return null;
            
            // ========== 第二步: 解析 VID/PID ==========
            
            var vidMatch = Regex.Match(deviceId, @"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            var pidMatch = Regex.Match(deviceId, @"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);

            if (!vidMatch.Success || !pidMatch.Success)
                return null;

            int vid = Convert.ToInt32(vidMatch.Groups[1].Value, 16);
            int pid = Convert.ToInt32(pidMatch.Groups[1].Value, 16);
            
            // ========== 第三步: 双重验证 MTK 设备 ==========
            
            // MTK 专属 VID (0x0E8D)
            bool hasMtkVid = (vid == 0x0E8D);
            
            // MTK 专属设备名称关键字
            string[] mtkKeywords = { "MEDIATEK", "MTK", "PRELOADER", "DA USB", "BROM" };
            bool hasMtkKeyword = false;
            foreach (var kw in mtkKeywords)
            {
                if (nameUpper.Contains(kw) || captionUpper.Contains(kw))
                {
                    hasMtkKeyword = true;
                    break;
                }
            }
            
            // 情况1: VID 0x0E8D = 确认是 MTK (无需关键字)
            if (hasMtkVid)
            {
                // 继续处理
            }
            // 情况2: 已知厂商 MTK 设备 (VID + PID 组合)
            else
            {
                var mtkDevice = MtkDeviceIds.FirstOrDefault(d => d.Vid == vid && d.Pid == pid);
                if (mtkDevice.Vid == 0)
                {
                    // 不是已知的 MTK 设备组合
                    // 检查是否有 MTK 关键字
                    if (!hasMtkKeyword)
                        return null;
                }
            }

            // ========== 第四步: 提取 COM 端口号 ==========
            
            var comMatch = Regex.Match(name, @"\(COM(\d+)\)", RegexOptions.IgnoreCase);
            if (!comMatch.Success)
            {
                comMatch = Regex.Match(caption, @"\(COM(\d+)\)", RegexOptions.IgnoreCase);
            }

            string comPort = comMatch.Success ? $"COM{comMatch.Groups[1].Value}" : null;
            if (string.IsNullOrEmpty(comPort))
                return null;
            
            // ========== 第五步: 确定设备模式 ==========
            
            var knownDevice = MtkDeviceIds.FirstOrDefault(d => d.Vid == vid && d.Pid == pid);
            string description = knownDevice.Description ?? name;
            
            bool isBrom = (pid == 0x0003 || pid == 0x0002) || nameUpper.Contains("BROM");
            bool isPreloader = (pid == 0x2000 || pid == 0x2001) || nameUpper.Contains("PRELOADER");

            return new MtkPortInfo
            {
                ComPort = comPort,
                Vid = vid,
                Pid = pid,
                DeviceId = deviceId,
                Description = description,
                IsBromMode = isBrom,
                IsPreloaderMode = isPreloader
            };
        }

        /// <summary>
        /// 探测端口是否为 MTK 设备
        /// </summary>
        private MtkPortInfo ProbePort(string portName)
        {
            try
            {
                using (var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One))
                {
                    port.ReadTimeout = 500;
                    port.WriteTimeout = 500;
                    port.Open();

                    // 发送 MTK 握手字节
                    port.Write(new byte[] { 0xA0 }, 0, 1);
                    Thread.Sleep(50);

                    if (port.BytesToRead > 0)
                    {
                        byte[] response = new byte[port.BytesToRead];
                        port.Read(response, 0, response.Length);

                        // 检查是否为 MTK 响应
                        if (response.Any(b => b == 0x5F || b == 0xA0))
                        {
                            return new MtkPortInfo
                            {
                                ComPort = portName,
                                Description = "MTK Device (Probed)",
                                IsBromMode = true
                            };
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region 热插拔监控

        /// <summary>
        /// 启动设备监控
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            try
            {
                _cts = new CancellationTokenSource();

                // 设备插入监控
                var insertQuery = new WqlEventQuery(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 2 " +
                    "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
                    "AND TargetInstance.ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'");

                _insertWatcher = new ManagementEventWatcher(insertQuery);
                _insertWatcher.EventArrived += OnDeviceInserted;
                _insertWatcher.Start();

                // 设备移除监控
                var removeQuery = new WqlEventQuery(
                    "SELECT * FROM __InstanceDeletionEvent WITHIN 2 " +
                    "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
                    "AND TargetInstance.ClassGuid='{4d36e978-e325-11ce-bfc1-08002be10318}'");

                _removeWatcher = new ManagementEventWatcher(removeQuery);
                _removeWatcher.EventArrived += OnDeviceRemovedEvent;
                _removeWatcher.Start();

                _isMonitoring = true;
                _log("[MTK Port] 设备监控已启动");
            }
            catch (Exception ex)
            {
                _log($"[MTK Port] 启动监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止设备监控
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            try
            {
                _cts?.Cancel();
                
                _insertWatcher?.Stop();
                _insertWatcher?.Dispose();
                _insertWatcher = null;

                _removeWatcher?.Stop();
                _removeWatcher?.Dispose();
                _removeWatcher = null;

                _isMonitoring = false;
                _log("[MTK Port] 设备监控已停止");
            }
            catch { }
        }

        /// <summary>
        /// 设备插入事件处理
        /// </summary>
        private void OnDeviceInserted(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string deviceId = targetInstance["DeviceID"]?.ToString() ?? "";
                string name = targetInstance["Name"]?.ToString() ?? "";
                string caption = targetInstance["Caption"]?.ToString() ?? "";

                var portInfo = ParseMtkDevice(deviceId, name, caption);
                if (portInfo != null)
                {
                    _log($"检测到设备: {portInfo.ComPort} [{portInfo.Description}]");
                    OnDeviceArrived?.Invoke(portInfo);
                }
            }
            catch { }
        }

        /// <summary>
        /// 设备移除事件处理
        /// </summary>
        private void OnDeviceRemovedEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                string name = targetInstance["Name"]?.ToString() ?? "";

                var comMatch = Regex.Match(name, @"\(COM(\d+)\)", RegexOptions.IgnoreCase);
                if (comMatch.Success)
                {
                    string comPort = $"COM{comMatch.Groups[1].Value}";
                    _log($"设备已移除: {comPort}");
                    OnDeviceRemoved?.Invoke(comPort);
                }
            }
            catch { }
        }

        #endregion

        #region 异步等待设备

        /// <summary>
        /// 等待 MTK 设备连接
        /// </summary>
        public async Task<MtkPortInfo> WaitForDeviceAsync(int timeoutMs = 60000, CancellationToken ct = default)
        {
            _log("等待设备... (BROM/Preloader)");
            
            var tcs = new TaskCompletionSource<MtkPortInfo>();
            
            void OnArrived(MtkPortInfo info)
            {
                tcs.TrySetResult(info);
            }

            try
            {
                OnDeviceArrived += OnArrived;
                StartMonitoring();

                // 先检查是否已有设备
                var existingPorts = GetMtkPorts();
                if (existingPorts.Count > 0)
                {
                    _log($"发现已连接设备: {existingPorts[0].ComPort}");
                    return existingPorts[0];
                }

                // 等待新设备
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(timeoutMs);
                    
                    try
                    {
                        var completedTask = await Task.WhenAny(
                            tcs.Task,
                            Task.Delay(timeoutMs, cts.Token)
                        );

                        if (completedTask == tcs.Task)
                        {
                            return await tcs.Task;
                        }
                    }
                    catch (OperationCanceledException) { }
                }

                _log("等待设备... 超时");
                return null;
            }
            finally
            {
                OnDeviceArrived -= OnArrived;
            }
        }

        /// <summary>
        /// 等待特定类型的 MTK 设备
        /// </summary>
        public async Task<MtkPortInfo> WaitForBromDeviceAsync(int timeoutMs = 60000, CancellationToken ct = default)
        {
            var startTime = DateTime.Now;
            
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (ct.IsCancellationRequested)
                    return null;

                var ports = GetMtkPorts();
                var bromPort = ports.FirstOrDefault(p => p.IsBromMode);
                
                if (bromPort != null)
                {
                    _log($"发现 BROM 设备: {bromPort.ComPort}");
                    return bromPort;
                }

                await Task.Delay(500, ct);
            }

            return null;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查端口是否可用
        /// </summary>
        public static bool IsPortAvailable(string portName)
        {
            try
            {
                using (var port = new SerialPort(portName))
                {
                    port.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取端口友好名称
        /// </summary>
        public static string GetPortFriendlyName(string portName)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%{portName}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["Caption"]?.ToString() ?? portName;
                    }
                }
            }
            catch { }

            return portName;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                _cts?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// MTK 端口信息
    /// </summary>
    public class MtkPortInfo
    {
        /// <summary>COM 端口名</summary>
        public string ComPort { get; set; }
        
        /// <summary>USB VID</summary>
        public int Vid { get; set; }
        
        /// <summary>USB PID</summary>
        public int Pid { get; set; }
        
        /// <summary>设备 ID</summary>
        public string DeviceId { get; set; }
        
        /// <summary>设备描述</summary>
        public string Description { get; set; }
        
        /// <summary>是否为 BROM 模式</summary>
        public bool IsBromMode { get; set; }
        
        /// <summary>是否为 Preloader 模式</summary>
        public bool IsPreloaderMode { get; set; }
        
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName => $"{ComPort} - {Description}";
        
        /// <summary>
        /// 模式描述
        /// </summary>
        public string ModeDescription
        {
            get
            {
                if (IsBromMode) return "BROM 模式";
                if (IsPreloaderMode) return "Preloader 模式";
                return "未知模式";
            }
        }

        public override string ToString()
        {
            return $"{ComPort} ({Description}) [{ModeDescription}]";
        }
    }
}
