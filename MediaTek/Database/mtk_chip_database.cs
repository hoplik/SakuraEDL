// ============================================================================
// SakuraEDL - MediaTek 芯片数据库
// MediaTek Chip Information Database
// ============================================================================
// 参考: mtkclient 项目 brom_config.py
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SakuraEDL.MediaTek.Models;
using SakuraEDL.MediaTek.Protocol;

namespace SakuraEDL.MediaTek.Database
{
    /// <summary>
    /// MTK 芯片信息记录
    /// </summary>
    public class MtkChipRecord
    {
        public ushort HwCode { get; set; }
        public string ChipName { get; set; }
        public string Description { get; set; }
        public uint WatchdogAddr { get; set; }
        public uint UartAddr { get; set; }
        public uint BromPayloadAddr { get; set; }
        public uint DaPayloadAddr { get; set; }
        public uint? CqDmaBase { get; set; }
        public int DaMode { get; set; } = (int)Protocol.DaMode.Xml;
        public uint Var1 { get; set; }
        public bool SupportsXFlash { get; set; }
        public bool Is64Bit { get; set; }
        public bool HasExploit { get; set; }
        public string ExploitType { get; set; }
        
        // V6 协议相关
        /// <summary>
        /// BROM 是否已被修补 (kamakiri/linecode 等 BROM 漏洞无效)
        /// </summary>
        public bool BromPatched { get; set; }
        
        /// <summary>
        /// 是否需要使用 V6 Loader (Preloader 模式)
        /// </summary>
        public bool RequiresLoader { get; set; }
        
        /// <summary>
        /// Loader 文件名 (如果需要)
        /// </summary>
        public string LoaderName { get; set; }
        
        /// <summary>
        /// SoC 版本 (用于区分同一芯片的不同版本)
        /// </summary>
        public ushort SocVer { get; set; }
        
        /// <summary>
        /// 芯片代号 (用于 Loader 匹配)
        /// </summary>
        public string Codename { get; set; }
        
        /// <summary>
        /// MISC_LOCK 寄存器地址 (用于 reset_to_brom)
        /// </summary>
        public uint MiscLockAddr { get; set; }
        
        /// <summary>
        /// 芯片名称 (别名)
        /// </summary>
        public string Name => ChipName;
        
        // ═══════════════════════════════════════════════════════════════════════
        // 漏洞支持标记 (基于 mtkclient 分析)
        // ═══════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// 是否支持 Kamakiri2 漏洞 (BROM 层, 需要 libusb)
        /// 适用于 LEGACY/XFLASH 协议的老芯片, 需要 BROM 模式 (PID=0x0003)
        /// </summary>
        public bool SupportsKamakiri2 { get; set; }
        
        /// <summary>
        /// 是否支持 Carbonara 漏洞 (DA1 层, 串口执行)
        /// 适用于 XFLASH/XML 协议芯片, Dimensity 9200 及以前
        /// </summary>
        public bool SupportsCarbonara { get; set; }
        
        /// <summary>
        /// 是否支持 AllinoneSignature 漏洞 (DA2 层, 串口执行)
        /// 适用于 XML 协议新芯片
        /// </summary>
        public bool SupportsAllinoneSignature { get; set; }
        
        /// <summary>
        /// Carbonara 是否已被修补 (DA1 包含修补特征码)
        /// </summary>
        public bool CarbonaraPatched { get; set; }
        
        /// <summary>
        /// 获取推荐的漏洞类型
        /// </summary>
        public string GetRecommendedExploit(bool isBromMode)
        {
            // BROM 模式优先使用 Kamakiri2
            if (isBromMode && SupportsKamakiri2)
                return "Kamakiri2";
            
            // Preloader/DA 模式
            if (SupportsCarbonara && !CarbonaraPatched)
                return "Carbonara";
            
            if (SupportsAllinoneSignature)
                return "AllinoneSignature";
            
            // 如果支持 Kamakiri2 但需要 Crash to BROM
            if (SupportsKamakiri2)
                return "CrashToBrom+Kamakiri2";
            
            return null;
        }
    }

    /// <summary>
    /// MTK 芯片数据库
    /// </summary>
    public static class MtkChipDatabase
    {
        private static readonly Dictionary<ushort, MtkChipRecord> _chips = new Dictionary<ushort, MtkChipRecord>();
        
        // Preloader 模式下 HW Code 别名映射
        // 有些芯片在 Preloader 模式下报告不同的 HW Code
        private static readonly Dictionary<ushort, ushort> _preloaderAliases = new Dictionary<ushort, ushort>
        {
            // Preloader HW Code => BROM HW Code
            { 0x1236, 0x0950 },   // MT6989 Preloader => MT6989 BROM
            { 0x0951, 0x0950 },   // MT6989 Alt => MT6989 BROM
            { 0x1172, 0x0996 },   // MT6895 Dimensity 8200 => MT6895 (需确认)
            { 0x0959, 0x0766 },   // MT6877 Preloader => MT6877 BROM
        };

        static MtkChipDatabase()
        {
            InitializeDatabase();
        }

        /// <summary>
        /// 初始化芯片数据库
        /// </summary>
        private static void InitializeDatabase()
        {
            // ═══════════════════════════════════════════════════════════════════════
            // LEGACY 协议芯片 - 支持 Kamakiri2 (BROM 层漏洞)
            // 这些芯片需要进入 BROM 模式 (PID=0x0003) 才能使用 Kamakiri2
            // ═══════════════════════════════════════════════════════════════════════
            
            // MT6261 系列 (功能机)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6261,
                ChipName = "MT6261",
                Description = "功能机芯片",
                WatchdogAddr = 0xA0030000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                SupportsKamakiri2 = true,
                MiscLockAddr = 0x10001838
            });

            // MT6572/MT6582 系列
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6572,
                ChipName = "MT6572",
                Description = "双核智能机芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                SupportsKamakiri2 = true,
                MiscLockAddr = 0x1000141C
            });

            AddChip(new MtkChipRecord
            {
                HwCode = 0x6582,
                ChipName = "MT6582",
                Description = "四核智能机芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                SupportsKamakiri2 = true,
                MiscLockAddr = 0x10002050
            });

            // ═══════════════════════════════════════════════════════════════════════
            // XFLASH 协议芯片 - 支持 Kamakiri2 (BROM) + Carbonara (DA1)
            // Carbonara 可在 Preloader 模式直接执行, 不需要 BROM 模式
            // ═══════════════════════════════════════════════════════════════════════
            
            // MT6735/MT6737 系列
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0321,
                ChipName = "MT6735",
                Description = "64位四核芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x10001838
            });

            AddChip(new MtkChipRecord
            {
                HwCode = 0x0335,
                ChipName = "MT6737",
                Description = "64位四核芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x10001838
            });

            // MT6755/MT6757 系列 (Helio P10/P20)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0326,
                ChipName = "MT6755",
                Description = "Helio P10",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x10001838
            });

            AddChip(new MtkChipRecord
            {
                HwCode = 0x0601,
                ChipName = "MT6757",
                Description = "Helio P20",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x10001838
            });

            // MT6761/MT6762/MT6763 系列 (Helio A/P22/P23)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0562,
                ChipName = "MT6761",
                Description = "Helio A22",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            AddChip(new MtkChipRecord
            {
                HwCode = 0x0707,
                ChipName = "MT6762",
                Description = "Helio P22",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            AddChip(new MtkChipRecord
            {
                HwCode = 0x0690,
                ChipName = "MT6763",
                Description = "Helio P23",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6765 (Helio P35/G35)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0717,
                ChipName = "MT6765",
                Description = "Helio P35/G35",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            AddChip(new MtkChipRecord
            {
                HwCode = 0x0725,
                ChipName = "MT6765",
                Description = "Helio P35 (变体)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6768 (Helio G85)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0551,
                ChipName = "MT6768",
                Description = "Helio G85",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6771 (Helio P60/P70/G80)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0688,
                ChipName = "MT6771",
                Description = "Helio P60/P70/G80",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6779 (Helio P90)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0507,
                ChipName = "MT6779",
                Description = "Helio P90",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6781 (Helio G96)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0781,
                ChipName = "MT6781",
                Description = "Helio G96",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6785 (Helio G90/G95)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0588,
                ChipName = "MT6785",
                Description = "Helio G90/G95",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // MT6797 (Helio X20/X25/X27)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0279,
                ChipName = "MT6797",
                Description = "Helio X20/X25/X27",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x10002050
            });

            // MT6739
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0699,
                ChipName = "MT6739",
                Description = "入门级 4G 芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212C00,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });

            // ═══════════════════════════════════════════════════════════════════════
            // Dimensity 系列 (5G 芯片) - 支持 Carbonara
            // XFLASH/XML 协议, Dimensity 9200 及以前支持 Carbonara
            // ═══════════════════════════════════════════════════════════════════════
            
            // MT6833 (Dimensity 700)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0813,
                ChipName = "MT6833",
                Description = "Dimensity 700 5G",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });

            // ═══════════════════════════════════════════════════════════════════════
            // ★★★ 以下根据 mtkclient brom_config.py 修正 ★★★
            // HW Code 到芯片名称的映射关系
            // ═══════════════════════════════════════════════════════════════════════
            
            // 0x0766 = MT6765 (不是 MT6877!)
            // mtkclient: dacode=0x6765, name="MT6765/MT8768t"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0766,
                ChipName = "MT6765",
                Description = "Helio P35/G35 (HW 0x0766)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });
            
            // 0x0788 = MT6771 (Helio P60/P70/G80)
            // mtkclient: dacode=0x6771, name="MT6771/MT8385/MT8183/MT8666"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0788,
                ChipName = "MT6771",
                Description = "Helio P60/P70/G80",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001a100
            });
            
            // 0x0996 = MT6853 (Dimensity 720) - 不是 MT6895!
            // mtkclient: dacode=0x6853, name="MT6853"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0996,
                ChipName = "MT6853",
                Description = "Dimensity 720 5G (HW 0x0996)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });
            
            // 0x0886 = MT6873 (Dimensity 800/820) - 不是 MT6885!
            // mtkclient: dacode=0x6873, name="MT6873"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0886,
                ChipName = "MT6873",
                Description = "Dimensity 800/820 5G (HW 0x0886)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });

            // 0x0959 = MT6877 (Dimensity 900/1080/7050)
            // mtkclient: dacode=0x6877, name="MT6877/MT6877V/MT8791N"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0959,
                ChipName = "MT6877",
                Description = "Dimensity 900/1080/7050",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });

            // 0x0816 = MT6885 (Dimensity 1000/1000+) - 不是 MT6893!
            // mtkclient: dacode=0x6885, name="MT6885/MT6883/MT6889/MT6880/MT6890"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0816,
                ChipName = "MT6885",
                Description = "Dimensity 1000/1000+ (HW 0x0816)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });

            // 0x0950 = MT6891/MT6893 (Dimensity 1200) - 不是 MT6989!
            // mtkclient: dacode=0x6893, name="MT6891/MT6893"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0950,
                ChipName = "MT6893",
                Description = "Dimensity 1100/1200 (HW 0x0950)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });
            
            // ═══════════════════════════════════════════════════════════════════════
            // 保留原有 0x0813, 0x0600 等其他已验证的芯片
            // ═══════════════════════════════════════════════════════════════════════
            
            // 0x0813 = MT6833 (Dimensity 700)
            // mtkclient: dacode=0x6833
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0813,
                ChipName = "MT6833",
                Description = "Dimensity 700 5G",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });
            
            // 0x1066 = MT6781 (Helio G96)
            // mtkclient: name="MT6781"
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1066,
                ChipName = "MT6781",
                Description = "Helio G96",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsKamakiri2 = true,
                SupportsCarbonara = true,
                MiscLockAddr = 0x1001A100
            });

            // ═══════════════════════════════════════════════════════════════════════
            // XML 协议芯片 (Dimensity 8000+ 系列)
            // 不支持 Kamakiri2, 支持 Carbonara (直到 9200)
            // ═══════════════════════════════════════════════════════════════════════

            // 0x0907 = MT6983 (Dimensity 9000/9000+) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0907,
                ChipName = "MT6983",
                Description = "Dimensity 9000/9000+",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // 0x1129 = MT6855 (Dimensity 7020/930) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1129,
                ChipName = "MT6855",
                Description = "Dimensity 7020/930",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });

            // 0x1172 = MT6895 (Dimensity 8200) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1172,
                ChipName = "MT6895",
                Description = "Dimensity 8200",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Var1 = 0x0A,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // 0x1203 = MT6897 (Dimensity 8300 Ultra) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1203,
                ChipName = "MT6897",
                Description = "Dimensity 8300 Ultra",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // 0x1208 = MT6789 (Helio G99) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1208,
                ChipName = "MT6789",
                Description = "Helio G99",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // 0x1209 = MT6835 (Dimensity 6100+) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1209,
                ChipName = "MT6835",
                Description = "Dimensity 6100+",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // 0x1229 = MT6886 (Dimensity 7200 Ultra) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1229,
                ChipName = "MT6886",
                Description = "Dimensity 7200 Ultra",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });

            // 0x1296 = MT6985 (Dimensity 9200/9200+) - Carbonara 最后支持的芯片
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1296,
                ChipName = "MT6985",
                Description = "Dimensity 9200/9200+",
                WatchdogAddr = 0x1C00A000,
                UartAddr = 0x1C011000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,  // ★ Carbonara 最后支持的芯片
                SupportsAllinoneSignature = true,
                Codename = "rubens"
            });
            
            // 0x1375 = MT6878 (Dimensity 7300) - XML 协议, Carbonara 可能已修补
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1375,
                ChipName = "MT6878",
                Description = "Dimensity 7300",
                WatchdogAddr = 0x1C00A000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x2010000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature",
                CarbonaraPatched = true,  // 可能已修补
                SupportsAllinoneSignature = true
            });

            // ═══════════════════════════════════════════════════════════════════════
            // MT6989 (Dimensity 9300) - 需要确认真实 HW Code
            // 注意: mtkclient 中 0x0950 = MT6891/MT6893, 不是 MT6989!
            // MT6989 的 HW Code 可能是 0x1236 或其他值，需要设备验证
            // ═══════════════════════════════════════════════════════════════════════
            
            // MT6989 Preloader 模式 HW Code - 0x1236 (实测确认)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1236,
                ChipName = "MT6989",
                Description = "Dimensity 9300 (Preloader)",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x02000000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Var1 = 0x0A,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature",
                CarbonaraPatched = true,
                SupportsAllinoneSignature = true
            });
            
            // MT6855 (Dimensity 7020/930) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1129,
                ChipName = "MT6855",
                Description = "Dimensity 7020/930",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // MT6897 (Dimensity 8300 Ultra) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1203,
                ChipName = "MT6897",
                Description = "Dimensity 8300 Ultra",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // MT6789 (Helio G99) - XML 协议
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1208,
                ChipName = "MT6789",
                Description = "Helio G99",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara",
                SupportsCarbonara = true,
                SupportsAllinoneSignature = true
            });
            
            // MT6878 (Dimensity 7300) - XML 协议, 可能已修补
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1375,
                ChipName = "MT6878",
                Description = "Dimensity 7300",
                WatchdogAddr = 0x1C00A000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x2010000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature",
                CarbonaraPatched = true,  // 可能已修补
                SupportsAllinoneSignature = true
            });

            // ═══════════════════════════════════════════════════════════════
            // V6 新芯片 (BROM 已修补, 需要 V6 Loader)
            // 参考: mtkclient brom_config.py
            // 这些芯片需要使用 Preloader 模式 + 签名 Loader
            // ═══════════════════════════════════════════════════════════════

            // ═══════════════════════════════════════════════════════════════
            // 注意: 以下芯片的 HW Code 与已有芯片冲突，需要设备验证
            // 暂时注释，避免覆盖已验证的芯片配置
            // ═══════════════════════════════════════════════════════════════
            
            // MT6781 (Helio G96) - HW Code 0x0788 与 MT6873 冲突
            // MT6789 (Helio G99) - HW Code 0x0813 与 MT6833 冲突
            // MT6855 (Dimensity 930) - HW Code 0x0886 与 MT6885 冲突
            // MT6879 (Dimensity 920) - HW Code 0x0959 与 MT6877 Preloader 冲突
            // MT6886 (Dimensity 7050) - HW Code 0x0996 与 MT6895 冲突
            //
            // TODO: 需要真实设备验证这些芯片的 HW Code
            // 可能这些是 Preloader 模式的别名，需要添加到 _preloaderAliases

            // MT6897 (Dimensity 8300) - V6 协议
            // TODO: 需要验证真实 HW Code，0x0950 与 MT6989 冲突
            // 暂时注释，等待设备验证
            /*
            AddChip(new MtkChipRecord
            {
                HwCode = 0x????,  // 待验证
                ChipName = "MT6897",
                Description = "Dimensity 8300",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x02000000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                BromPatched = true,
                RequiresLoader = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature",
                Codename = "mt6897"
            });
            */

            // MT8168, MT8183, MT8185 - HW Code 冲突，暂时注释
            // MT8168/MT8183 的 0x0699 与 MT6739 冲突
            // MT8185 的 0x0717 与 MT6765 冲突
            // TODO: 需要设备验证真实 HW Code

            // MT8188 - V6 协议
            // TODO: 需要验证真实 HW Code，0x0950 与 MT6989 冲突
            // 暂时注释，等待设备验证
            /*
            AddChip(new MtkChipRecord
            {
                HwCode = 0x????,  // 待验证
                ChipName = "MT8188",
                Description = "IoT/平板旗舰芯片",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x40000000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                BromPatched = true,
                RequiresLoader = true,
                Codename = "mt8188"
            });
            */

            // MT8195, MT6769, MT6769V, MT6769Z, MT6750 - HW Code 冲突，暂时注释
            // MT8195 的 0x0816 与 MT6893 冲突
            // MT6769 的 0x0707 与 MT6762 冲突 (可能是同一芯片的不同命名)
            // MT6769V 的 0x0725 与 MT6765 变体冲突
            // MT6769Z 的 0x0562 与 MT6761 冲突
            // MT6750 的 0x0326 与 MT6755 冲突
            // TODO: 验证这些是否为同一芯片的不同市场命名

            // MT6580 (入门级芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6580,
                ChipName = "MT6580",
                Description = "入门级四核芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                Codename = "mt6580"
            });

            // MT6592 (八核芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6592,
                ChipName = "MT6592",
                Description = "八核芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                Codename = "mt6592"
            });

            // MT6595 (首款 64 位芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6595,
                ChipName = "MT6595",
                Description = "首款 64 位芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = true,
                Codename = "mt6595"
            });

            // MT6752, MT6753, MT6732 - HW Code 冲突，暂时注释
            // MT6752 的 0x0321 与 MT6735 冲突
            // MT6753 的 0x0337 是独立的，可以保留
            // MT6732 的 0x0335 与 MT6737 冲突
            
            // MT6753 (MT6752 增强版) - 独立 HW Code
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0337,
                ChipName = "MT6753",
                Description = "MT6752 增强版",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                Codename = "mt6753"
            });

            // MT6570 (入门级芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6570,
                ChipName = "MT6570",
                Description = "入门级芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                Codename = "mt6570"
            });

            // MT8127 (平板芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x8127,
                ChipName = "MT8127",
                Description = "平板芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false,
                Codename = "mt8127"
            });

            // MT8163 (平板芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x8163,
                ChipName = "MT8163",
                Description = "平板芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = true,
                Codename = "mt8163"
            });

            // MT8167 (平板芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x8167,
                ChipName = "MT8167",
                Description = "平板芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                Codename = "mt8167"
            });

            // MT8173 (Chromebook 芯片)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x8173,
                ChipName = "MT8173",
                Description = "Chromebook 芯片",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                Codename = "mt8173"
            });

            // MT8176 (MT8173 增强版)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x8176,
                ChipName = "MT8176",
                Description = "MT8173 增强版",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                Codename = "mt8176"
            });

            // ═══════════════════════════════════════════════════════════════
            // 新增 Preloader HW Code 别名映射 (部分芯片在不同模式下 HW Code 不同)
            // ═══════════════════════════════════════════════════════════════
            
            // TODO: 添加更多已验证的芯片
            // 芯片信息来源:
            // 1. mtkclient 项目 brom_config.py (已验证)
            // 2. 真实设备测试数据
            // 3. 官方技术文档
            //
            // 注意: 不要添加未验证的芯片或猜测的HW Code
        }

        /// <summary>
        /// 添加芯片记录
        /// </summary>
        private static void AddChip(MtkChipRecord chip)
        {
            _chips[chip.HwCode] = chip;
        }

        /// <summary>
        /// 根据 HW Code 获取芯片信息
        /// </summary>
        public static MtkChipRecord GetChip(ushort hwCode)
        {
            // 先直接查找
            if (_chips.TryGetValue(hwCode, out var chip))
                return chip;
            
            // 检查 Preloader 别名
            if (_preloaderAliases.TryGetValue(hwCode, out var bromHwCode))
            {
                if (_chips.TryGetValue(bromHwCode, out chip))
                    return chip;
            }
            
            return null;
        }
        
        /// <summary>
        /// 根据 HW Code 获取芯片信息 (包含 Preloader 模式检测)
        /// </summary>
        public static (MtkChipRecord chip, bool isPreloaderAlias) GetChipWithAlias(ushort hwCode)
        {
            // 先直接查找
            if (_chips.TryGetValue(hwCode, out var chip))
                return (chip, false);
            
            // 检查 Preloader 别名
            if (_preloaderAliases.TryGetValue(hwCode, out var bromHwCode))
            {
                if (_chips.TryGetValue(bromHwCode, out chip))
                    return (chip, true);
            }
            
            return (null, false);
        }

        /// <summary>
        /// 获取芯片名称
        /// </summary>
        public static string GetChipName(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            return chip?.ChipName ?? $"MT{hwCode:X4}";
        }

        /// <summary>
        /// 获取所有支持的芯片
        /// </summary>
        public static IReadOnlyList<MtkChipRecord> GetAllChips()
        {
            return _chips.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取支持漏洞利用的芯片列表
        /// </summary>
        public static IReadOnlyList<MtkChipRecord> GetExploitableChips()
        {
            return _chips.Values.Where(c => c.HasExploit).ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取支持指定漏洞类型的芯片列表
        /// </summary>
        /// <param name="exploitType">漏洞类型: Carbonara, AllinoneSignature, Kamakiri2</param>
        public static IReadOnlyList<MtkChipRecord> GetChipsByExploitType(string exploitType)
        {
            return _chips.Values
                .Where(c => c.HasExploit && 
                       string.Equals(c.ExploitType, exploitType, StringComparison.OrdinalIgnoreCase))
                .ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取支持 ALLINONE-SIGNATURE 漏洞的芯片列表
        /// </summary>
        public static IReadOnlyList<MtkChipRecord> GetAllinoneSignatureChips()
        {
            return GetChipsByExploitType("AllinoneSignature");
        }

        /// <summary>
        /// 检查芯片是否支持 ALLINONE-SIGNATURE 漏洞
        /// </summary>
        public static bool IsAllinoneSignatureSupported(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            return chip != null && 
                   chip.HasExploit && 
                   string.Equals(chip.ExploitType, "AllinoneSignature", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取芯片的漏洞类型
        /// </summary>
        public static string GetExploitType(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            return chip?.ExploitType ?? "None";
        }

        /// <summary>
        /// 转换为 MtkChipInfo
        /// </summary>
        public static MtkChipInfo ToChipInfo(MtkChipRecord record)
        {
            if (record == null) return null;

            return new MtkChipInfo
            {
                HwCode = record.HwCode,
                ChipName = record.ChipName,
                Description = record.Description,
                WatchdogAddr = record.WatchdogAddr,
                UartAddr = record.UartAddr,
                BromPayloadAddr = record.BromPayloadAddr,
                DaPayloadAddr = record.DaPayloadAddr,
                CqDmaBase = record.CqDmaBase,
                DaMode = record.DaMode,
                SupportsXFlash = record.SupportsXFlash,
                Is64Bit = record.Is64Bit,
                // V6 新字段
                BromPatched = record.BromPatched,
                RequiresLoader = record.RequiresLoader,
                LoaderName = record.LoaderName,
                Codename = record.Codename,
                ExploitType = record.ExploitType,
                HasExploit = record.HasExploit
            };
        }

        #region V6 协议相关方法

        /// <summary>
        /// 检查芯片是否需要 V6 Loader
        /// </summary>
        public static bool RequiresV6Loader(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            return chip?.RequiresLoader ?? false;
        }

        /// <summary>
        /// 检查 BROM 是否已被修补
        /// </summary>
        public static bool IsBromPatched(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            return chip?.BromPatched ?? false;
        }

        /// <summary>
        /// 获取需要 V6 Loader 的芯片列表
        /// </summary>
        public static IReadOnlyList<MtkChipRecord> GetV6LoaderChips()
        {
            return _chips.Values.Where(c => c.RequiresLoader).ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取 BROM 已修补的芯片列表
        /// </summary>
        public static IReadOnlyList<MtkChipRecord> GetBromPatchedChips()
        {
            return _chips.Values.Where(c => c.BromPatched).ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取芯片的 Loader 文件名
        /// </summary>
        public static string GetLoaderName(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            if (chip == null) return null;
            
            // 如果有指定的 Loader 名称则返回
            if (!string.IsNullOrEmpty(chip.LoaderName))
                return chip.LoaderName;
            
            // 否则根据芯片名生成默认名称
            return $"{chip.ChipName.ToLower()}_loader.bin";
        }

        /// <summary>
        /// 获取芯片的代号
        /// </summary>
        public static string GetCodename(ushort hwCode)
        {
            var chip = GetChip(hwCode);
            return chip?.Codename ?? $"mt{hwCode:x4}";
        }

        #endregion

        #region 统计方法

        /// <summary>
        /// 获取数据库统计信息
        /// </summary>
        public static MtkDatabaseStats GetStats()
        {
            var stats = new MtkDatabaseStats
            {
                TotalChips = _chips.Count,
                ExploitableChips = _chips.Values.Count(c => c.HasExploit),
                V6LoaderChips = _chips.Values.Count(c => c.RequiresLoader),
                BromPatchedChips = _chips.Values.Count(c => c.BromPatched),
                CarbonaraChips = _chips.Values.Count(c => c.ExploitType == "Carbonara"),
                AllinoneSignatureChips = _chips.Values.Count(c => c.ExploitType == "AllinoneSignature"),
                LegacyChips = _chips.Values.Count(c => c.DaMode == (int)DaMode.Legacy),
                XmlChips = _chips.Values.Count(c => c.DaMode == (int)DaMode.Xml),
                XFlashChips = _chips.Values.Count(c => c.DaMode == (int)DaMode.XFlash)
            };
            return stats;
        }

        #endregion
    }

    /// <summary>
    /// 芯片数据库统计信息
    /// </summary>
    public class MtkDatabaseStats
    {
        public int TotalChips { get; set; }
        public int ExploitableChips { get; set; }
        public int V6LoaderChips { get; set; }
        public int BromPatchedChips { get; set; }
        public int CarbonaraChips { get; set; }
        public int AllinoneSignatureChips { get; set; }
        public int LegacyChips { get; set; }
        public int XmlChips { get; set; }
        public int XFlashChips { get; set; }

        public override string ToString()
        {
            return $"MTK 芯片数据库统计:\n" +
                   $"  总芯片数: {TotalChips}\n" +
                   $"  可利用漏洞: {ExploitableChips}\n" +
                   $"  - Carbonara: {CarbonaraChips}\n" +
                   $"  - AllinoneSignature: {AllinoneSignatureChips}\n" +
                   $"  需要 V6 Loader: {V6LoaderChips}\n" +
                   $"  BROM 已修补: {BromPatchedChips}\n" +
                   $"  协议分布: Legacy={LegacyChips}, XML={XmlChips}, XFlash={XFlashChips}";
        }
    }
}
