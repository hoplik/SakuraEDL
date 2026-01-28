// ============================================================================
// SakuraEDL - MediaTek 设备信息模型
// MediaTek Device Information Models
// ============================================================================

using System;

namespace SakuraEDL.MediaTek.Models
{
    /// <summary>
    /// MTK 芯片信息
    /// </summary>
    public class MtkChipInfo
    {
        /// <summary>硬件代码 (HW Code)</summary>
        public ushort HwCode { get; set; }
        
        /// <summary>硬件版本</summary>
        public ushort HwVer { get; set; }
        
        /// <summary>硬件子代码</summary>
        public ushort HwSubCode { get; set; }
        
        /// <summary>软件版本</summary>
        public ushort SwVer { get; set; }
        
        /// <summary>芯片名称</summary>
        public string ChipName { get; set; }
        
        /// <summary>芯片描述</summary>
        public string Description { get; set; }
        
        /// <summary>看门狗地址</summary>
        public uint WatchdogAddr { get; set; }
        
        /// <summary>UART 地址</summary>
        public uint UartAddr { get; set; }
        
        /// <summary>BROM Payload 地址</summary>
        public uint BromPayloadAddr { get; set; }
        
        /// <summary>DA Payload 地址</summary>
        public uint DaPayloadAddr { get; set; }
        
        /// <summary>CQ_DMA 基地址</summary>
        public uint? CqDmaBase { get; set; }
        
        /// <summary>DA 模式</summary>
        public int DaMode { get; set; } = 6;  // 默认 XML 模式
        
        /// <summary>是否支持 XFlash</summary>
        public bool SupportsXFlash { get; set; }
        
        /// <summary>是否需要签名</summary>
        public bool RequiresSignature { get; set; }
        
        /// <summary>是否支持 64 位</summary>
        public bool Is64Bit { get; set; }
        
        /// <summary>BROM 是否已被修补</summary>
        public bool BromPatched { get; set; }
        
        /// <summary>是否需要 V6 Loader</summary>
        public bool RequiresLoader { get; set; }
        
        /// <summary>Loader 文件名</summary>
        public string LoaderName { get; set; }
        
        /// <summary>芯片代号</summary>
        public string Codename { get; set; }
        
        /// <summary>漏洞类型</summary>
        public string ExploitType { get; set; }
        
        /// <summary>是否有可用漏洞</summary>
        public bool HasExploit { get; set; }
        
        /// <summary>
        /// 获取芯片名称 (根据 HW Code)
        /// </summary>
        public string GetChipName()
        {
            if (!string.IsNullOrEmpty(ChipName))
                return ChipName;
                
            return HwCode switch
            {
                0x0279 => "MT6797",
                0x0321 => "MT6735",
                0x0326 => "MT6755",
                0x0335 => "MT6737",
                0x0507 => "MT6779",
                0x0551 => "MT6768",
                0x0562 => "MT6761",
                0x0570 => "MT6580",
                0x0571 => "MT6572",
                0x0572 => "MT6572",
                0x0588 => "MT6785",
                0x0600 => "MT6853",
                0x0601 => "MT6757",
                0x0688 => "MT6771",
                0x0690 => "MT6763",
                0x0699 => "MT6739",
                0x0707 => "MT6762",
                0x0717 => "MT6765",
                0x0725 => "MT6765",
                0x0766 => "MT6877",
                0x0959 => "MT6877",  // Preloader 模式 HW Code
                0x0788 => "MT6873",
                0x0813 => "MT6833",
                0x0816 => "MT6893",
                0x0886 => "MT6885",
                0x0950 => "MT6833",
                0x0989 => "MT6891",
                0x0996 => "MT6895",
                0x1172 => "MT6895",  // Dimensity 8200 (之前误标为 MT6983)
                0x1186 => "MT6983",  // Dimensity 9000 (待确认)
                0x1208 => "MT6895",
                0x1209 => "MT6985",
                0x2502 => "MT2502",
                0x2503 => "MT2503",
                0x2601 => "MT2601",
                0x6261 => "MT6261",
                0x6570 => "MT6570",
                0x6575 => "MT6575",
                0x6577 => "MT6577",
                0x6580 => "MT6580",
                0x6582 => "MT6582",
                0x6589 => "MT6589",
                0x6592 => "MT6592",
                0x6595 => "MT6595",
                0x6752 => "MT6752",
                0x6753 => "MT6753",
                0x6755 => "MT6755",
                0x6757 => "MT6757",
                0x6761 => "MT6761",
                0x6763 => "MT6763",
                0x6765 => "MT6765",
                0x6768 => "MT6768",
                0x6771 => "MT6771",
                0x6779 => "MT6779",
                0x6785 => "MT6785",
                0x6795 => "MT6795",
                0x6797 => "MT6797",
                0x8127 => "MT8127",
                0x8135 => "MT8135",
                0x8163 => "MT8163",
                0x8167 => "MT8167",
                0x8168 => "MT8168",
                0x8173 => "MT8173",
                0x8176 => "MT8176",
                0x8695 => "MT8695",
                _ => $"MT{HwCode:X4}"
            };
        }

        /// <summary>
        /// 克隆
        /// </summary>
        public MtkChipInfo Clone()
        {
            return (MtkChipInfo)MemberwiseClone();
        }
    }

    /// <summary>
    /// MTK 设备信息
    /// </summary>
    public class MtkDeviceInfo
    {
        /// <summary>设备路径/端口名</summary>
        public string DevicePath { get; set; }
        
        /// <summary>COM 端口</summary>
        public string ComPort { get; set; }
        
        /// <summary>USB VID</summary>
        public int Vid { get; set; }
        
        /// <summary>USB PID</summary>
        public int Pid { get; set; }
        
        /// <summary>设备描述</summary>
        public string Description { get; set; }
        
        /// <summary>是否为下载模式</summary>
        public bool IsDownloadMode { get; set; }
        
        /// <summary>芯片信息</summary>
        public MtkChipInfo ChipInfo { get; set; }
        
        /// <summary>ME ID</summary>
        public byte[] MeId { get; set; }
        
        /// <summary>SOC ID</summary>
        public byte[] SocId { get; set; }
        
        /// <summary>
        /// ME ID 十六进制字符串
        /// </summary>
        public string MeIdHex => MeId != null ? BitConverter.ToString(MeId).Replace("-", "") : "";
        
        /// <summary>
        /// SOC ID 十六进制字符串
        /// </summary>
        public string SocIdHex => SocId != null ? BitConverter.ToString(SocId).Replace("-", "") : "";
        
        /// <summary>
        /// DA 模式 (5 = XFlash, 6 = XML)
        /// </summary>
        public int DaMode { get; set; }
    }

    /// <summary>
    /// DA 条目信息
    /// </summary>
    public class DaEntry
    {
        /// <summary>DA 名称</summary>
        public string Name { get; set; }
        
        /// <summary>加载地址</summary>
        public uint LoadAddr { get; set; }
        
        /// <summary>签名长度</summary>
        public int SignatureLen { get; set; }
        
        /// <summary>DA 数据</summary>
        public byte[] Data { get; set; }
        
        /// <summary>是否为 64 位</summary>
        public bool Is64Bit { get; set; }
        
        /// <summary>DA 版本</summary>
        public int Version { get; set; }
        
        /// <summary>DA 类型 (Legacy/XFlash/XML)</summary>
        public int DaType { get; set; }
    }

    /// <summary>
    /// MTK 分区信息
    /// </summary>
    public class MtkPartitionInfo
    {
        /// <summary>分区名称</summary>
        public string Name { get; set; }
        
        /// <summary>起始扇区</summary>
        public ulong StartSector { get; set; }
        
        /// <summary>扇区数量</summary>
        public ulong SectorCount { get; set; }
        
        /// <summary>分区大小 (字节)</summary>
        public ulong Size { get; set; }
        
        /// <summary>分区类型</summary>
        public string Type { get; set; }
        
        /// <summary>分区属性</summary>
        public ulong Attributes { get; set; }
        
        /// <summary>是否只读</summary>
        public bool IsReadOnly => (Attributes & 0x1) != 0;
        
        /// <summary>是否为系统分区</summary>
        public bool IsSystem => (Attributes & 0x2) != 0;
        
        /// <summary>
        /// 格式化大小显示
        /// </summary>
        public string SizeDisplay
        {
            get
            {
                if (Size >= 1024UL * 1024 * 1024)
                    return $"{Size / (1024.0 * 1024 * 1024):F2} GB";
                if (Size >= 1024 * 1024)
                    return $"{Size / (1024.0 * 1024):F2} MB";
                if (Size >= 1024)
                    return $"{Size / 1024.0:F2} KB";
                return $"{Size} B";
            }
        }
    }

    /// <summary>
    /// MTK 目标配置
    /// </summary>
    public class MtkTargetConfig
    {
        /// <summary>原始配置值</summary>
        public uint RawValue { get; set; }
        
        /// <summary>是否启用 Secure Boot</summary>
        public bool SbcEnabled { get; set; }
        
        /// <summary>是否启用 SLA</summary>
        public bool SlaEnabled { get; set; }
        
        /// <summary>是否启用 DAA</summary>
        public bool DaaEnabled { get; set; }
        
        /// <summary>是否启用 SW JTAG</summary>
        public bool SwJtagEnabled { get; set; }
        
        /// <summary>EPP 是否启用</summary>
        public bool EppEnabled { get; set; }
        
        /// <summary>是否需要 Root 证书</summary>
        public bool CertRequired { get; set; }
        
        /// <summary>内存读取是否需要认证</summary>
        public bool MemReadAuth { get; set; }
        
        /// <summary>内存写入是否需要认证</summary>
        public bool MemWriteAuth { get; set; }
        
        /// <summary>CMD C8 是否被阻止</summary>
        public bool CmdC8Blocked { get; set; }
    }

    /// <summary>
    /// MTK Flash 信息
    /// </summary>
    public class MtkFlashInfo
    {
        /// <summary>Flash 类型 (eMMC/UFS/NAND)</summary>
        public string FlashType { get; set; }
        
        /// <summary>Flash 制造商 ID</summary>
        public ushort ManufacturerId { get; set; }
        
        /// <summary>Flash 容量 (字节)</summary>
        public ulong Capacity { get; set; }
        
        /// <summary>块大小</summary>
        public uint BlockSize { get; set; }
        
        /// <summary>页大小</summary>
        public uint PageSize { get; set; }
        
        /// <summary>Flash 型号</summary>
        public string Model { get; set; }
        
        /// <summary>
        /// 格式化容量显示
        /// </summary>
        public string CapacityDisplay
        {
            get
            {
                if (Capacity >= 1024UL * 1024 * 1024 * 1024)
                    return $"{Capacity / (1024.0 * 1024 * 1024 * 1024):F2} TB";
                if (Capacity >= 1024UL * 1024 * 1024)
                    return $"{Capacity / (1024.0 * 1024 * 1024):F2} GB";
                if (Capacity >= 1024 * 1024)
                    return $"{Capacity / (1024.0 * 1024):F2} MB";
                return $"{Capacity} B";
            }
        }
    }

    /// <summary>
    /// MTK 安全信息
    /// </summary>
    public class MtkSecurityInfo
    {
        /// <summary>是否启用 Secure Boot</summary>
        public bool SecureBootEnabled { get; set; }
        
        /// <summary>是否为 Unfused 设备</summary>
        public bool IsUnfused { get; set; }
        
        /// <summary>SLA 是否启用</summary>
        public bool SlaEnabled { get; set; }
        
        /// <summary>DAA 是否启用</summary>
        public bool DaaEnabled { get; set; }
        
        /// <summary>ME ID</summary>
        public string MeId { get; set; }
        
        /// <summary>SOC ID</summary>
        public string SocId { get; set; }
        
        /// <summary>防回滚版本</summary>
        public uint AntiRollbackVersion { get; set; }
        
        /// <summary>是否锁定</summary>
        public bool IsLocked { get; set; }
        
        /// <summary>SBC 是否启用</summary>
        public bool SbcEnabled { get; set; }
    }

    /// <summary>
    /// MTK Bootloader 状态
    /// </summary>
    public class MtkBootloaderStatus
    {
        /// <summary>是否已解锁</summary>
        public bool IsUnlocked { get; set; }
        
        /// <summary>是否为 Unfused 设备</summary>
        public bool IsUnfused { get; set; }
        
        /// <summary>Secure Boot 是否启用</summary>
        public bool SecureBootEnabled { get; set; }
        
        /// <summary>安全版本</summary>
        public uint SecurityVersion { get; set; }
        
        /// <summary>设备型号</summary>
        public string DeviceModel { get; set; }
        
        /// <summary>状态消息</summary>
        public string StatusMessage
        {
            get
            {
                if (IsUnfused)
                    return "Unfused (开发设备)";
                if (IsUnlocked)
                    return "已解锁";
                return "已锁定";
            }
        }
    }

    /// <summary>
    /// 漏洞利用信息
    /// </summary>
    public class MtkExploitInfo
    {
        /// <summary>是否已连接</summary>
        public bool IsConnected { get; set; }
        
        /// <summary>芯片名称</summary>
        public string ChipName { get; set; }
        
        /// <summary>硬件代码</summary>
        public ushort HwCode { get; set; }
        
        /// <summary>漏洞类型 (Carbonara, AllinoneSignature, None)</summary>
        public string ExploitType { get; set; }
        
        /// <summary>是否支持 ALLINONE-SIGNATURE 漏洞</summary>
        public bool IsAllinoneSignatureSupported { get; set; }
        
        /// <summary>是否支持 Carbonara 漏洞</summary>
        public bool IsCarbonaraSupported { get; set; }
        
        /// <summary>支持 ALLINONE-SIGNATURE 的芯片列表</summary>
        public MtkChipExploitInfo[] AllinoneSignatureChips { get; set; }
    }

    /// <summary>
    /// 芯片漏洞信息
    /// </summary>
    public class MtkChipExploitInfo
    {
        /// <summary>芯片名称</summary>
        public string ChipName { get; set; }
        
        /// <summary>硬件代码</summary>
        public ushort HwCode { get; set; }
        
        /// <summary>描述</summary>
        public string Description { get; set; }
    }
}
