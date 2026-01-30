// ============================================================================
// SakuraEDL - MediaTek USB 设备检测器
// 基于 MTK META UTILITY 逆向分析增强
// ============================================================================
// 支持检测模式:
// - BROM 模式 (Boot ROM)
// - Preloader 模式
// - DA 模式 (Download Agent)
// - META 模式 (工程测试模式)
// - FACTORY 模式 (工厂测试模式)
// - ADB/Fastboot 模式
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// MTK 设备模式
    /// </summary>
    public enum MtkUsbMode
    {
        /// <summary>未知模式</summary>
        Unknown = 0,
        
        /// <summary>BROM 模式 (Boot ROM, 最底层)</summary>
        Brom = 1,
        
        /// <summary>Preloader 模式</summary>
        Preloader = 2,
        
        /// <summary>DA 模式 (Download Agent 已加载)</summary>
        Da = 3,
        
        /// <summary>META 模式 (工程测试模式)</summary>
        Meta = 4,
        
        /// <summary>FACTORY 模式 (工厂测试模式)</summary>
        Factory = 5,
        
        /// <summary>ADB 模式</summary>
        Adb = 6,
        
        /// <summary>Fastboot 模式</summary>
        Fastboot = 7,
        
        /// <summary>特殊/扩展模式</summary>
        Special = 8
    }

    /// <summary>
    /// MTK USB 设备信息
    /// </summary>
    public class MtkUsbDeviceInfo
    {
        /// <summary>Vendor ID</summary>
        public int Vid { get; set; }
        
        /// <summary>Product ID</summary>
        public int Pid { get; set; }
        
        /// <summary>COM 端口名称</summary>
        public string ComPort { get; set; }
        
        /// <summary>设备描述</summary>
        public string Description { get; set; }
        
        /// <summary>设备模式</summary>
        public MtkUsbMode Mode { get; set; }
        
        /// <summary>是否为 MTK 设备</summary>
        public bool IsMtkDevice { get; set; }
        
        /// <summary>设备实例路径</summary>
        public string InstancePath { get; set; }
        
        /// <summary>设备序列号</summary>
        public string SerialNumber { get; set; }
        
        public override string ToString()
        {
            return $"[{Mode}] VID:0x{Vid:X4} PID:0x{Pid:X4} - {ComPort ?? Description}";
        }
    }

    /// <summary>
    /// MTK USB 设备检测器
    /// </summary>
    public static class MtkUsbDetector
    {
        // MediaTek Vendor ID
        public const int VID_MEDIATEK = 0x0E8D;
        public const int VID_MEDIATEK_ALT = 0x0B05;  // ASUS OEM 变体

        /// <summary>
        /// MTK USB PID 定义 (基于 MTK META UTILITY V48 逆向分析)
        /// </summary>
        public static class MtkPids
        {
            // ═══════════════════════════════════════════════════════════════
            // BROM 模式 (Boot ROM)
            // ═══════════════════════════════════════════════════════════════
            public const int PID_BROM = 0x0003;           // 标准 BROM 模式
            public const int PID_BROM_ALT = 0x2001;       // BROM 模式变体
            
            // ═══════════════════════════════════════════════════════════════
            // Preloader 模式
            // ═══════════════════════════════════════════════════════════════
            public const int PID_PRELOADER = 0x2000;      // 标准 Preloader
            public const int PID_PRELOADER_ALT = 0x6000;  // Preloader 变体
            public const int PID_PRELOADER_V2 = 0x0616;   // V2 Preloader
            
            // ═══════════════════════════════════════════════════════════════
            // DA 模式 (Download Agent)
            // ═══════════════════════════════════════════════════════════════
            public const int PID_DA = 0x2001;             // DA 模式
            public const int PID_DA_CDC = 0x2003;         // DA CDC 模式
            public const int PID_DA_V2 = 0x2004;          // DA V2 模式
            public const int PID_DA_V3 = 0x2005;          // DA V3 模式 (V48 新增)
            
            // ═══════════════════════════════════════════════════════════════
            // META 模式 (工程测试) - V48 增强
            // ═══════════════════════════════════════════════════════════════
            public const int PID_META = 0x0001;           // META 模式
            public const int PID_META_UART = 0x2007;      // META UART 模式
            public const int PID_META_COM = 0x20FF;       // META COM 端口
            public const int PID_META_USB = 0x1010;       // META USB 模式 (V48 新增)
            public const int PID_META_SP = 0x1011;        // META SP 模式 (V48 新增)
            
            // ═══════════════════════════════════════════════════════════════
            // FACTORY 模式 (工厂测试) - V48 新增
            // ═══════════════════════════════════════════════════════════════
            public const int PID_FACTORY = 0x0002;        // FACTORY 模式
            public const int PID_FACTORY_UART = 0x2010;   // FACTORY UART 模式
            public const int PID_FACTORY_CDC = 0x2011;    // FACTORY CDC 模式
            
            // ═══════════════════════════════════════════════════════════════
            // 特殊模式
            // ═══════════════════════════════════════════════════════════════
            public const int PID_COMPOSITE = 0x2006;      // 复合设备
            public const int PID_ADB = 0x200A;            // ADB 模式
            public const int PID_ADB_RNDIS = 0x200C;      // ADB + RNDIS
            public const int PID_FASTBOOT = 0x200D;       // Fastboot 模式
            public const int PID_MTP = 0x200E;            // MTP 模式
            public const int PID_PTP = 0x200F;            // PTP 模式 (V48 新增)
            
            // ═══════════════════════════════════════════════════════════════
            // 扩展模式 (来自 MTK META UTILITY)
            // ═══════════════════════════════════════════════════════════════
            public const int PID_EXTENDED_1 = 0x2008;     // 扩展模式 1
            public const int PID_EXTENDED_2 = 0x2009;     // 扩展模式 2
            public const int PID_EXTENDED_3 = 0x200B;     // 扩展模式 3
            
            // ═══════════════════════════════════════════════════════════════
            // 厂商定制 PID (V48 分析)
            // ═══════════════════════════════════════════════════════════════
            public const int PID_OPPO_MTK = 0x2012;       // OPPO MTK 设备
            public const int PID_XIAOMI_MTK = 0x2013;     // 小米 MTK 设备
            public const int PID_VIVO_MTK = 0x2014;       // VIVO MTK 设备
            public const int PID_REALME_MTK = 0x2015;     // REALME MTK 设备
        }

        /// <summary>
        /// PID 到模式的映射 (V48 增强)
        /// </summary>
        private static readonly Dictionary<int, MtkUsbMode> PidToMode = new Dictionary<int, MtkUsbMode>
        {
            // BROM
            { MtkPids.PID_BROM, MtkUsbMode.Brom },
            { MtkPids.PID_BROM_ALT, MtkUsbMode.Brom },
            
            // Preloader
            { MtkPids.PID_PRELOADER, MtkUsbMode.Preloader },
            { MtkPids.PID_PRELOADER_ALT, MtkUsbMode.Preloader },
            { MtkPids.PID_PRELOADER_V2, MtkUsbMode.Preloader },
            
            // DA
            { MtkPids.PID_DA, MtkUsbMode.Da },
            { MtkPids.PID_DA_CDC, MtkUsbMode.Da },
            { MtkPids.PID_DA_V2, MtkUsbMode.Da },
            { MtkPids.PID_DA_V3, MtkUsbMode.Da },
            
            // META
            { MtkPids.PID_META, MtkUsbMode.Meta },
            { MtkPids.PID_META_UART, MtkUsbMode.Meta },
            { MtkPids.PID_META_COM, MtkUsbMode.Meta },
            { MtkPids.PID_META_USB, MtkUsbMode.Meta },
            { MtkPids.PID_META_SP, MtkUsbMode.Meta },
            
            // FACTORY (V48 新增)
            { MtkPids.PID_FACTORY, MtkUsbMode.Factory },
            { MtkPids.PID_FACTORY_UART, MtkUsbMode.Factory },
            { MtkPids.PID_FACTORY_CDC, MtkUsbMode.Factory },
            
            // ADB/Fastboot
            { MtkPids.PID_ADB, MtkUsbMode.Adb },
            { MtkPids.PID_ADB_RNDIS, MtkUsbMode.Adb },
            { MtkPids.PID_FASTBOOT, MtkUsbMode.Fastboot },
            
            // 特殊
            { MtkPids.PID_COMPOSITE, MtkUsbMode.Special },
            { MtkPids.PID_EXTENDED_1, MtkUsbMode.Special },
            { MtkPids.PID_EXTENDED_2, MtkUsbMode.Special },
            { MtkPids.PID_EXTENDED_3, MtkUsbMode.Special },
            { MtkPids.PID_PTP, MtkUsbMode.Special },
            
            // 厂商定制 (V48 新增)
            { MtkPids.PID_OPPO_MTK, MtkUsbMode.Special },
            { MtkPids.PID_XIAOMI_MTK, MtkUsbMode.Special },
            { MtkPids.PID_VIVO_MTK, MtkUsbMode.Special },
            { MtkPids.PID_REALME_MTK, MtkUsbMode.Special },
        };

        /// <summary>
        /// 检测所有 MTK USB 设备
        /// </summary>
        public static List<MtkUsbDeviceInfo> DetectDevices()
        {
            var devices = new List<MtkUsbDeviceInfo>();

            try
            {
                // 查询 USB 设备
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%USB%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string deviceId = obj["DeviceID"]?.ToString() ?? "";
                            string description = obj["Description"]?.ToString() ?? "";
                            string name = obj["Name"]?.ToString() ?? "";

                            // 解析 VID/PID
                            var vidPid = ParseVidPid(deviceId);
                            if (vidPid.HasValue)
                            {
                                int vid = vidPid.Value.vid;
                                int pid = vidPid.Value.pid;

                                // 检查是否为 MTK 设备
                                if (vid == VID_MEDIATEK || vid == VID_MEDIATEK_ALT)
                                {
                                    var device = new MtkUsbDeviceInfo
                                    {
                                        Vid = vid,
                                        Pid = pid,
                                        Description = description,
                                        InstancePath = deviceId,
                                        IsMtkDevice = true
                                    };

                                    // 确定模式
                                    if (PidToMode.TryGetValue(pid, out var mode))
                                    {
                                        device.Mode = mode;
                                    }
                                    else
                                    {
                                        device.Mode = MtkUsbMode.Unknown;
                                    }

                                    // 提取 COM 端口
                                    device.ComPort = ExtractComPort(name, deviceId);

                                    devices.Add(device);
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 额外查询串口设备
                var comDevices = DetectComPorts();
                foreach (var comDevice in comDevices)
                {
                    if (!devices.Any(d => d.ComPort == comDevice.ComPort))
                    {
                        devices.Add(comDevice);
                    }
                }
            }
            catch { }

            return devices;
        }

        /// <summary>
        /// 检测 MTK 串口设备
        /// </summary>
        public static List<MtkUsbDeviceInfo> DetectComPorts()
        {
            var devices = new List<MtkUsbDeviceInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            string deviceId = obj["DeviceID"]?.ToString() ?? "";
                            string name = obj["Name"]?.ToString() ?? "";
                            string description = obj["Description"]?.ToString() ?? "";

                            // 检查是否为 MTK 相关
                            bool isMtk = deviceId.Contains("0E8D") || 
                                        name.Contains("MediaTek") ||
                                        name.Contains("MTK") ||
                                        description.Contains("MediaTek");

                            if (isMtk)
                            {
                                var vidPid = ParseVidPid(deviceId);
                                var device = new MtkUsbDeviceInfo
                                {
                                    Vid = vidPid?.vid ?? VID_MEDIATEK,
                                    Pid = vidPid?.pid ?? 0,
                                    Description = description,
                                    ComPort = ExtractComPort(name, deviceId),
                                    InstancePath = deviceId,
                                    IsMtkDevice = true
                                };

                                if (vidPid.HasValue && PidToMode.TryGetValue(vidPid.Value.pid, out var mode))
                                {
                                    device.Mode = mode;
                                }
                                else
                                {
                                    // 根据描述推测模式
                                    device.Mode = InferModeFromDescription(description + " " + name);
                                }

                                devices.Add(device);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return devices;
        }

        /// <summary>
        /// 等待 MTK 设备连接
        /// </summary>
        public static MtkUsbDeviceInfo WaitForDevice(int timeoutMs = 30000, MtkUsbMode? expectedMode = null)
        {
            DateTime startTime = DateTime.Now;
            
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                var devices = DetectDevices();
                
                foreach (var device in devices)
                {
                    if (!expectedMode.HasValue || device.Mode == expectedMode.Value)
                    {
                        return device;
                    }
                }

                System.Threading.Thread.Sleep(200);
            }

            return null;
        }

        /// <summary>
        /// 检查是否为 BROM 模式
        /// </summary>
        public static bool IsBromMode(int vid, int pid)
        {
            return vid == VID_MEDIATEK && 
                   (pid == MtkPids.PID_BROM || pid == MtkPids.PID_BROM_ALT);
        }

        /// <summary>
        /// 检查是否为 Preloader 模式
        /// </summary>
        public static bool IsPreloaderMode(int vid, int pid)
        {
            return vid == VID_MEDIATEK && 
                   (pid == MtkPids.PID_PRELOADER || 
                    pid == MtkPids.PID_PRELOADER_ALT ||
                    pid == MtkPids.PID_PRELOADER_V2);
        }

        /// <summary>
        /// 检查是否为下载模式 (BROM 或 Preloader)
        /// </summary>
        public static bool IsDownloadMode(int vid, int pid)
        {
            return IsBromMode(vid, pid) || IsPreloaderMode(vid, pid);
        }

        /// <summary>
        /// 检查是否为 META 模式
        /// </summary>
        public static bool IsMetaMode(int vid, int pid)
        {
            return vid == VID_MEDIATEK && 
                   (pid == MtkPids.PID_META || 
                    pid == MtkPids.PID_META_UART ||
                    pid == MtkPids.PID_META_COM ||
                    pid == MtkPids.PID_META_USB ||
                    pid == MtkPids.PID_META_SP);
        }
        
        /// <summary>
        /// 检查是否为 FACTORY 模式 (V48 新增)
        /// </summary>
        public static bool IsFactoryMode(int vid, int pid)
        {
            return vid == VID_MEDIATEK && 
                   (pid == MtkPids.PID_FACTORY || 
                    pid == MtkPids.PID_FACTORY_UART ||
                    pid == MtkPids.PID_FACTORY_CDC);
        }
        
        /// <summary>
        /// 检查是否为 DA 模式 (V48 增强)
        /// </summary>
        public static bool IsDaMode(int vid, int pid)
        {
            return vid == VID_MEDIATEK && 
                   (pid == MtkPids.PID_DA || 
                    pid == MtkPids.PID_DA_CDC ||
                    pid == MtkPids.PID_DA_V2 ||
                    pid == MtkPids.PID_DA_V3);
        }
        
        /// <summary>
        /// 检查是否为可用于 Exploit 的模式 (BROM 或 Preloader)
        /// </summary>
        public static bool IsExploitableMode(int vid, int pid)
        {
            return IsBromMode(vid, pid) || IsPreloaderMode(vid, pid);
        }
        
        /// <summary>
        /// 获取推荐的操作
        /// </summary>
        public static string GetRecommendedAction(MtkUsbMode mode)
        {
            return mode switch
            {
                MtkUsbMode.Brom => "可以使用 Kamakiri2 BROM 漏洞或加载 Preloader",
                MtkUsbMode.Preloader => "可以使用 Carbonara DA 漏洞或上传 DA",
                MtkUsbMode.Da => "DA 已加载，可以执行读写操作",
                MtkUsbMode.Meta => "META 模式，可以执行工程命令",
                MtkUsbMode.Factory => "FACTORY 模式，可以执行工厂命令",
                MtkUsbMode.Adb => "ADB 模式，请重启到 Preloader/BROM",
                MtkUsbMode.Fastboot => "Fastboot 模式，请重启到 Preloader/BROM",
                _ => "请按住音量下键并连接设备进入 BROM 模式"
            };
        }

        /// <summary>
        /// 获取模式描述
        /// </summary>
        public static string GetModeDescription(MtkUsbMode mode)
        {
            return mode switch
            {
                MtkUsbMode.Brom => "BROM (Boot ROM)",
                MtkUsbMode.Preloader => "Preloader",
                MtkUsbMode.Da => "DA (Download Agent)",
                MtkUsbMode.Meta => "META (工程测试)",
                MtkUsbMode.Factory => "FACTORY (工厂测试)",
                MtkUsbMode.Adb => "ADB",
                MtkUsbMode.Fastboot => "Fastboot",
                MtkUsbMode.Special => "特殊模式",
                _ => "未知模式"
            };
        }

        /// <summary>
        /// 解析 VID/PID
        /// </summary>
        private static (int vid, int pid)? ParseVidPid(string deviceId)
        {
            // 格式: USB\VID_0E8D&PID_0003\...
            var match = Regex.Match(deviceId, @"VID_([0-9A-Fa-f]{4}).*PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int vid = Convert.ToInt32(match.Groups[1].Value, 16);
                int pid = Convert.ToInt32(match.Groups[2].Value, 16);
                return (vid, pid);
            }
            return null;
        }

        /// <summary>
        /// 提取 COM 端口
        /// </summary>
        private static string ExtractComPort(string name, string deviceId)
        {
            // 从名称中提取 (COM3) 这样的格式
            var match = Regex.Match(name, @"\(COM(\d+)\)");
            if (match.Success)
            {
                return $"COM{match.Groups[1].Value}";
            }
            return null;
        }

        /// <summary>
        /// 根据描述推测模式
        /// </summary>
        private static MtkUsbMode InferModeFromDescription(string text)
        {
            text = text.ToUpperInvariant();
            
            if (text.Contains("BROM") || text.Contains("BOOTROM"))
                return MtkUsbMode.Brom;
            if (text.Contains("PRELOADER"))
                return MtkUsbMode.Preloader;
            if (text.Contains("META"))
                return MtkUsbMode.Meta;
            if (text.Contains("FACTORY"))
                return MtkUsbMode.Factory;
            if (text.Contains("ADB"))
                return MtkUsbMode.Adb;
            if (text.Contains("FASTBOOT"))
                return MtkUsbMode.Fastboot;
            if (text.Contains("DA ") || text.Contains("DOWNLOAD AGENT"))
                return MtkUsbMode.Da;
            
            return MtkUsbMode.Unknown;
        }
    }
}
