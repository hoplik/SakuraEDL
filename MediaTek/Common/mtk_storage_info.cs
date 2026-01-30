// ============================================================================
// SakuraEDL - Storage Info | 存储信息解析
// ============================================================================
// [ZH] 存储信息 - UFS/EMMC 存储设备识别和信息解析
// [EN] Storage Info - UFS/EMMC storage device identification and parsing
// [JA] ストレージ情報 - UFS/EMMCストレージデバイスの識別と解析
// [KO] 저장소 정보 - UFS/EMMC 저장 장치 식별 및 분석
// [RU] Информация о хранилище - Идентификация и разбор UFS/EMMC
// [ES] Info de almacenamiento - Identificación y análisis de UFS/EMMC
// ============================================================================
// Based on UnlockTool log format analysis
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================
// 功能:
// - UFS/EMMC 存储信息解析
// - 存储厂商识别 (Samsung, SKHynix, Micron 等)
// - UnlockTool 风格日志输出
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// 存储类型
    /// </summary>
    public enum StorageType
    {
        Unknown = 0,
        EMMC = 1,
        UFS = 2,
        NAND = 3
    }

    /// <summary>
    /// 存储厂商信息 (基于 UnlockTool 分析)
    /// </summary>
    public static class StorageVendors
    {
        // UFS/EMMC 厂商 ID 映射
        private static readonly Dictionary<int, string> VendorNames = new Dictionary<int, string>
        {
            // 常见厂商
            { 0x1CE, "Samsung" },
            { 0x1AD, "SKHynix" },
            { 0x12C, "Micron MP" },
            { 0x198, "Toshiba" },
            { 0x1C5, "SanDisk" },
            { 0x1E3, "KIOXIA" },
            { 0x02C, "Micron" },
            { 0x045, "SanDisk/WD" },
            { 0x090, "Hynix" },
            { 0x013, "Toshiba" },
            { 0x015, "Samsung" },
            { 0x037, "KingSpec" },
            { 0x070, "Kingston" },
            { 0x0A9, "YMTC" },
            { 0x0A98, "Unknown" },  // 从截图中看到的
            
            // EMMC 厂商
            { 0x11, "Toshiba" },
            { 0x13, "Micron" },
            { 0x15, "Samsung" },
            { 0x45, "SanDisk" },
            { 0x90, "Hynix" },
            { 0xFE, "Micron" },
        };

        /// <summary>
        /// 获取厂商名称
        /// </summary>
        public static string GetVendorName(int vendorId)
        {
            if (VendorNames.TryGetValue(vendorId, out string name))
                return name;
            return "Unknown";
        }

        /// <summary>
        /// 格式化厂商信息 (UnlockTool 风格)
        /// </summary>
        public static string FormatVendorInfo(int vendorId)
        {
            string name = GetVendorName(vendorId);
            return $"{name} [0x{vendorId:X}]";
        }
    }

    /// <summary>
    /// 存储信息
    /// </summary>
    public class MtkStorageInfo
    {
        /// <summary>存储类型</summary>
        public StorageType Type { get; set; }
        
        /// <summary>固件版本</summary>
        public string FirmwareVersion { get; set; }
        
        /// <summary>CID (芯片 ID)</summary>
        public string Cid { get; set; }
        
        /// <summary>厂商 ID</summary>
        public int VendorId { get; set; }
        
        /// <summary>厂商名称</summary>
        public string VendorName => StorageVendors.GetVendorName(VendorId);
        
        /// <summary>UFS LU0 大小 (字节)</summary>
        public long Lu0Size { get; set; }
        
        /// <summary>UFS LU1 大小 (字节)</summary>
        public long Lu1Size { get; set; }
        
        /// <summary>UFS LU2 大小 (字节)</summary>
        public long Lu2Size { get; set; }
        
        /// <summary>EMMC 总大小 (字节)</summary>
        public long TotalSize { get; set; }
        
        /// <summary>块大小</summary>
        public int BlockSize { get; set; }
        
        /// <summary>
        /// 格式化输出 (UnlockTool 风格)
        /// </summary>
        public string ToUnlockToolFormat()
        {
            var sb = new StringBuilder();
            
            // Storage: UFS - FWVer: 0600 - CID: KLUEG8UHDC-B0E1
            sb.Append($"Storage: {Type}");
            if (!string.IsNullOrEmpty(FirmwareVersion))
                sb.Append($" - FWVer: {FirmwareVersion}");
            if (!string.IsNullOrEmpty(Cid))
                sb.Append($" - CID: {Cid}");
            
            return sb.ToString();
        }

        /// <summary>
        /// 格式化厂商信息
        /// </summary>
        public string FormatVendorInfo()
        {
            return $"Vendor ID: {StorageVendors.FormatVendorInfo(VendorId)}";
        }

        /// <summary>
        /// 格式化 UFS 大小信息
        /// </summary>
        public string FormatUfsSize()
        {
            if (Type != StorageType.UFS)
                return $"Size: {FormatSize(TotalSize)}";
            
            return $"UFS: LU0 Size: {FormatSize(Lu0Size)} - LU1 Size: {FormatSize(Lu1Size)} - LU2 Size: {FormatSize(Lu2Size)}";
        }

        /// <summary>
        /// 格式化大小
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GiB";
            if (bytes >= 1024L * 1024)
                return $"{bytes / (1024.0 * 1024):F0} MiB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F0} KiB";
            return $"{bytes} B";
        }
    }

    /// <summary>
    /// UnlockTool 风格日志格式化器
    /// </summary>
    public static class UnlockToolLogFormat
    {
        /// <summary>
        /// 格式化硬件信息 (UnlockTool 风格)
        /// Hardware: MT6833 [Dimensity 700|810|6020|6080] 0989 8A00 CA00 0000
        /// </summary>
        public static string FormatHardwareInfo(ushort hwCode, ushort hwVer, string chipName, string[] aliases = null)
        {
            var sb = new StringBuilder();
            sb.Append($"Hardware: {chipName}");
            
            if (aliases != null && aliases.Length > 0)
            {
                sb.Append($" [{string.Join("|", aliases)}]");
            }
            
            // HW Code 和版本信息
            sb.Append($" {hwCode:X4} {hwVer:X4} CA00 0000");
            
            return sb.ToString();
        }

        /// <summary>
        /// 格式化安全配置 (UnlockTool 风格)
        /// Security Config: SCB DAA
        /// </summary>
        public static string FormatSecurityConfig(bool sbc, bool sla, bool daa)
        {
            var flags = new List<string>();
            if (sbc) flags.Add("SCB");
            if (sla) flags.Add("SLA");
            if (daa) flags.Add("DAA");
            
            return $"Security Config: {(flags.Count > 0 ? string.Join(" ", flags) : "None")}";
        }

        /// <summary>
        /// 格式化 MEID
        /// MEID: 5D1F8784038C25CA93859DF8C39239FD
        /// </summary>
        public static string FormatMeid(byte[] meid)
        {
            if (meid == null || meid.Length == 0)
                return "MEID: (not available)";
            return $"MEID: {BitConverter.ToString(meid).Replace("-", "")}";
        }

        /// <summary>
        /// 格式化操作状态 (UnlockTool 风格)
        /// </summary>
        public static string FormatStatus(string operation, bool success, string extra = null)
        {
            string status = success ? "OK" : "FAIL";
            if (!string.IsNullOrEmpty(extra))
                return $"{operation}... {status} [{extra}]";
            return $"{operation}... {status}";
        }

        /// <summary>
        /// 格式化分区信息
        /// Reading partition info... OK [63]
        /// </summary>
        public static string FormatPartitionInfo(int partitionCount)
        {
            return $"Reading partition info... OK [{partitionCount}]";
        }

        /// <summary>
        /// 格式化设备信息
        /// Reading device info... SKIP [EROFS]
        /// Model: PFDM00
        /// </summary>
        public static string FormatDeviceInfo(string model, string fsType = null)
        {
            if (string.IsNullOrEmpty(fsType))
                return $"Model: {model}";
            return $"Reading device info... SKIP [{fsType}]\nModel: {model}";
        }

        /// <summary>
        /// 格式化锁定状态
        /// Checking for lock data... OK [RMX3618] [locked]
        /// </summary>
        public static string FormatLockStatus(string model, bool locked)
        {
            string status = locked ? "locked" : "unlocked";
            return $"Checking for lock data... OK [{model}] [{status}]";
        }

        /// <summary>
        /// 获取完整的设备信息报告 (UnlockTool 风格)
        /// </summary>
        public static string GetFullDeviceReport(
            ushort hwCode, ushort hwVer, string chipName, string[] aliases,
            bool sbc, bool sla, bool daa,
            byte[] meid,
            MtkStorageInfo storage,
            int partitionCount,
            string model,
            bool locked)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine(FormatHardwareInfo(hwCode, hwVer, chipName, aliases));
            sb.AppendLine(FormatSecurityConfig(sbc, sla, daa));
            sb.AppendLine(FormatMeid(meid));
            
            if (storage != null)
            {
                sb.AppendLine(storage.ToUnlockToolFormat());
                sb.AppendLine(storage.FormatVendorInfo());
                sb.AppendLine(storage.FormatUfsSize());
            }
            
            sb.AppendLine(FormatPartitionInfo(partitionCount));
            sb.AppendLine($"Model: {model}");
            sb.AppendLine(FormatLockStatus(model, locked));
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// MTK 芯片别名数据库 (基于 UnlockTool 截图)
    /// </summary>
    public static class MtkChipAliases
    {
        private static readonly Dictionary<ushort, string[]> Aliases = new Dictionary<ushort, string[]>
        {
            // MT6833 - Dimensity 700 系列
            { 0x0813, new[] { "Dimensity 700", "810", "6020", "6080" } },
            { 0x0989, new[] { "Dimensity 700", "810", "6020", "6080" } },
            
            // MT6853 - Dimensity 720
            { 0x0600, new[] { "Dimensity 720", "800U" } },
            
            // MT6877 - Dimensity 900
            { 0x0959, new[] { "Dimensity 900", "920", "7050" } },
            
            // MT6893 - Dimensity 1100/1200
            { 0x0816, new[] { "Dimensity 1100", "1200", "1300", "8020", "8050" } },
            
            // MT6885 - Dimensity 1000
            { 0x0886, new[] { "Dimensity 1000", "1000+", "1000L" } },
            
            // MT6873 - Dimensity 800
            { 0x0788, new[] { "Dimensity 800", "820" } },
            
            // MT6765 - Helio P35/G35
            { 0x0766, new[] { "Helio P35", "G35" } },
            
            // MT6768 - Helio G85
            { 0x0551, new[] { "Helio G85" } },
            
            // MT6785 - Helio G90/G95
            { 0x0588, new[] { "Helio G90T", "G95" } },
            
            // MT6781 - Helio G96
            { 0x1066, new[] { "Helio G96" } },
        };

        /// <summary>
        /// 获取芯片别名
        /// </summary>
        public static string[] GetAliases(ushort hwCode)
        {
            if (Aliases.TryGetValue(hwCode, out string[] aliases))
                return aliases;
            return null;
        }

        /// <summary>
        /// 格式化芯片名称带别名
        /// </summary>
        public static string FormatChipNameWithAliases(ushort hwCode, string chipName)
        {
            var aliases = GetAliases(hwCode);
            if (aliases != null && aliases.Length > 0)
                return $"{chipName} [{string.Join("|", aliases)}]";
            return chipName;
        }
    }
}
