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
            // MT6261 系列 (功能机)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x6261,
                ChipName = "MT6261",
                Description = "功能机芯片",
                WatchdogAddr = 0xA0030000,
                DaMode = (int)DaMode.Legacy,
                Is64Bit = false
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
                Is64Bit = false
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
                Is64Bit = false
            });

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
                Is64Bit = true
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
                Is64Bit = true
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
                Is64Bit = true
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
                Is64Bit = true
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6765 (Helio P35)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0717,
                ChipName = "MT6765",
                Description = "Helio P35",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6771 (Helio P60)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0688,
                ChipName = "MT6771",
                Description = "Helio P60",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
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
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6797 (Helio X20/X25)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0279,
                ChipName = "MT6797",
                Description = "Helio X20/X25",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true
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
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6833 (Dimensity 700)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0813,
                ChipName = "MT6833",
                Description = "Dimensity 700",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6853 (Dimensity 720)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0600,
                ChipName = "MT6853",
                Description = "Dimensity 720",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6873 (Dimensity 800)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0788,
                ChipName = "MT6873",
                Description = "Dimensity 800/820",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6877 (Dimensity 900) - BROM 模式
            // 参考 Config.xml: DA1Address=2097152 (0x200000)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0766,
                ChipName = "MT6877",
                Description = "Dimensity 900",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,   // Config.xml: 0x200000
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });
            
            // MT6877 (Dimensity 900) - Preloader 模式 HW Code
            // 参考 Config.xml: DA1Address=2097152 (0x200000)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0959,
                ChipName = "MT6877",
                Description = "Dimensity 900 (Preloader)",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,   // Config.xml: 0x200000
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.XFlash,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6885 (Dimensity 1000)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0886,
                ChipName = "MT6885",
                Description = "Dimensity 1000/1000+",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6891 (Dimensity 1100)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0989,
                ChipName = "MT6891",
                Description = "Dimensity 1100",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6893 (Dimensity 1200)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0816,
                ChipName = "MT6893",
                Description = "Dimensity 1200",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6895 (Dimensity 8000)
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0996,
                ChipName = "MT6895",
                Description = "Dimensity 8000/8100",
                WatchdogAddr = 0x10007000,
                UartAddr = 0x11002000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x200000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // MT6895 (Dimensity 8200) - HW Code 0x1172
            // 注意: 之前误标为 MT6983，根据实际测试 HW Code 0x1172 = MT6895
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1172,
                ChipName = "MT6895",
                Description = "Dimensity 8200",
                WatchdogAddr = 0x1C007000,  // 根据截图修正
                UartAddr = 0x11001000,      // 根据截图修正
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x201000,   // 根据截图: 0x201000
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Var1 = 0x0A,                // 根据截图: Var1 = A
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "Carbonara"
            });

            // ═══════════════════════════════════════════════════════════════
            // MT6989 (Dimensity 9300) - iQOO Z9 Turbo, VIVO 等
            // 支持 ALLINONE-SIGNATURE 漏洞
            // ChimeraTool 日志确认 DA1 地址: 0x02000000
            // ═══════════════════════════════════════════════════════════════
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0950,
                ChipName = "MT6989",
                Description = "Dimensity 9300",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x02000000,  // DA1 地址 (ChimeraTool 确认)
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Var1 = 0x0A,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature"
            });

            // MT6989 Preloader 模式 HW Code - 0x1236 (实测确认)
            // VIVO 设备在 Preloader 模式下报告此 HW Code
            // ChimeraTool 日志确认 DA1 地址: 0x02000000
            AddChip(new MtkChipRecord
            {
                HwCode = 0x1236,
                ChipName = "MT6989",
                Description = "Dimensity 9300 (Preloader)",
                WatchdogAddr = 0x1C007000,  // Preloader 模式下可能无法访问
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x02000000,  // DA1 地址 (ChimeraTool 确认)
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Var1 = 0x0A,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature"  // 使用 DA2 级别漏洞
            });

            // MT6989 其他可能的 Preloader HW Code
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0951,
                ChipName = "MT6989",
                Description = "Dimensity 9300 (Preloader Alt)",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x02000000,  // DA1 地址
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Var1 = 0x0A,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature"
            });

            // MT6983 (Dimensity 9000) - 可能也支持 ALLINONE-SIGNATURE
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0900,
                ChipName = "MT6983",
                Description = "Dimensity 9000",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x40000000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature"
            });

            // MT6985 (Dimensity 9200) - 可能也支持 ALLINONE-SIGNATURE
            AddChip(new MtkChipRecord
            {
                HwCode = 0x0930,
                ChipName = "MT6985",
                Description = "Dimensity 9200",
                WatchdogAddr = 0x1C007000,
                UartAddr = 0x11001000,
                BromPayloadAddr = 0x100A00,
                DaPayloadAddr = 0x40000000,
                CqDmaBase = 0x10212000,
                DaMode = (int)DaMode.Xml,
                Is64Bit = true,
                HasExploit = true,
                ExploitType = "AllinoneSignature",
                BromPatched = true,
                RequiresLoader = true,
                Codename = "rubens"
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
