// ============================================================================
// LoveAlways - MediaTek DA 数据库
// MediaTek Download Agent Database
// ============================================================================
// DA 加载器数据库管理
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoveAlways.MediaTek.Models;
using LoveAlways.MediaTek.Protocol;

namespace LoveAlways.MediaTek.Database
{
    /// <summary>
    /// DA 记录
    /// </summary>
    public class MtkDaRecord
    {
        /// <summary>HW Code</summary>
        public ushort HwCode { get; set; }
        
        /// <summary>DA 名称</summary>
        public string Name { get; set; }
        
        /// <summary>DA 类型 (Legacy/XFlash/XML)</summary>
        public int DaType { get; set; }
        
        /// <summary>DA 版本</summary>
        public int Version { get; set; }
        
        /// <summary>DA1 加载地址</summary>
        public uint Da1Address { get; set; }
        
        /// <summary>DA2 加载地址</summary>
        public uint Da2Address { get; set; }
        
        /// <summary>DA1 签名长度</summary>
        public int Da1SigLen { get; set; }
        
        /// <summary>DA2 签名长度</summary>
        public int Da2SigLen { get; set; }
        
        /// <summary>嵌入式 DA1 数据 (如果有)</summary>
        public byte[] EmbeddedDa1Data { get; set; }
        
        /// <summary>嵌入式 DA2 数据 (如果有)</summary>
        public byte[] EmbeddedDa2Data { get; set; }
        
        /// <summary>是否支持 Exploit</summary>
        public bool SupportsExploit { get; set; }
    }

    /// <summary>
    /// MTK DA 数据库
    /// </summary>
    public static class MtkDaDatabase
    {
        private static readonly Dictionary<ushort, MtkDaRecord> _daRecords = new Dictionary<ushort, MtkDaRecord>();
        private static byte[] _allInOneDaData = null;
        private static string _daFilePath = null;

        static MtkDaDatabase()
        {
            InitializeDatabase();
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        private static void InitializeDatabase()
        {
            // V6 (XML DA) 默认配置 (仅包含已验证的芯片)
            var v6Chips = new ushort[]
            {
                0x0551, 0x0562, 0x0588, 0x0600, 0x0690, 0x0699, 0x0707,
                0x0717, 0x0725, 0x0766, 0x0788, 0x0813, 0x0816, 0x0886,
                0x0959, 0x0989, 0x0996, 0x1172, 0x1208
            };

            foreach (var hwCode in v6Chips)
            {
                AddDaRecord(new MtkDaRecord
                {
                    HwCode = hwCode,
                    Name = $"DA_V6_{hwCode:X4}",
                    DaType = (int)DaMode.Xml,
                    Version = 6,
                    Da1Address = 0x200000,
                    Da2Address = 0x40000000,
                    Da1SigLen = 0x30,
                    Da2SigLen = 0x30,
                    SupportsExploit = true
                });
            }

            // TODO: 添加特殊配置芯片 (需验证)
            // 某些高端芯片使用特殊的DA1地址 (如 0x1000000)

            // XFlash DA 配置
            var xflashChips = new ushort[]
            {
                0x0279, 0x0321, 0x0326, 0x0335, 0x0601, 0x0688
            };

            foreach (var hwCode in xflashChips)
            {
                AddDaRecord(new MtkDaRecord
                {
                    HwCode = hwCode,
                    Name = $"DA_XFlash_{hwCode:X4}",
                    DaType = (int)DaMode.XFlash,
                    Version = 5,
                    Da1Address = 0x200000,
                    Da2Address = 0x40000000,
                    Da1SigLen = 0x100,
                    Da2SigLen = 0x100,
                    SupportsExploit = false
                });
            }

            // Legacy DA 配置
            var legacyChips = new ushort[]
            {
                0x6261, 0x6572, 0x6582, 0x6589, 0x6592, 0x6752, 0x6795
            };

            foreach (var hwCode in legacyChips)
            {
                AddDaRecord(new MtkDaRecord
                {
                    HwCode = hwCode,
                    Name = $"DA_Legacy_{hwCode:X4}",
                    DaType = (int)DaMode.Legacy,
                    Version = 3,
                    Da1Address = 0x200000,
                    Da2Address = 0x40000000,
                    Da1SigLen = 0x100,
                    Da2SigLen = 0x100,
                    SupportsExploit = false
                });
            }
        }

        /// <summary>
        /// 添加 DA 记录
        /// </summary>
        private static void AddDaRecord(MtkDaRecord record)
        {
            _daRecords[record.HwCode] = record;
        }

        /// <summary>
        /// 获取 DA 记录
        /// </summary>
        public static MtkDaRecord GetDaRecord(ushort hwCode)
        {
            return _daRecords.TryGetValue(hwCode, out var record) ? record : null;
        }

        /// <summary>
        /// 获取所有 DA 记录
        /// </summary>
        public static IReadOnlyList<MtkDaRecord> GetAllDaRecords()
        {
            return _daRecords.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// 设置 AllInOne DA 文件路径
        /// </summary>
        public static void SetDaFilePath(string filePath)
        {
            if (File.Exists(filePath))
            {
                _daFilePath = filePath;
                _allInOneDaData = null;  // 清除缓存
            }
        }

        /// <summary>
        /// 加载 AllInOne DA 数据
        /// </summary>
        public static bool LoadAllInOneDa()
        {
            if (!string.IsNullOrEmpty(_daFilePath) && File.Exists(_daFilePath))
            {
                _allInOneDaData = File.ReadAllBytes(_daFilePath);
                return true;
            }

            // 尝试默认路径
            var defaultPaths = new[]
            {
                "MtkResources/MTK_AllInOne_DA.bin",
                "Resources/MTK_AllInOne_DA.bin",
                "DA/MTK_AllInOne_DA.bin"
            };

            foreach (var path in defaultPaths)
            {
                if (File.Exists(path))
                {
                    _allInOneDaData = File.ReadAllBytes(path);
                    _daFilePath = path;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取 AllInOne DA 数据
        /// </summary>
        public static byte[] GetAllInOneDaData()
        {
            if (_allInOneDaData == null)
            {
                LoadAllInOneDa();
            }
            return _allInOneDaData;
        }

        /// <summary>
        /// 获取芯片的 DA1 加载地址
        /// </summary>
        public static uint GetDa1Address(ushort hwCode)
        {
            var record = GetDaRecord(hwCode);
            if (record != null)
                return record.Da1Address;

            // 根据芯片类型返回默认值
            var chip = MtkChipDatabase.GetChip(hwCode);
            if (chip != null)
                return chip.DaPayloadAddr;

            return 0x200000;  // 默认值
        }

        /// <summary>
        /// 获取芯片的 DA2 加载地址
        /// </summary>
        public static uint GetDa2Address(ushort hwCode)
        {
            var record = GetDaRecord(hwCode);
            return record?.Da2Address ?? 0x40000000;
        }

        /// <summary>
        /// 获取芯片的 DA 类型
        /// </summary>
        public static Protocol.DaMode GetDaMode(ushort hwCode)
        {
            var record = GetDaRecord(hwCode);
            if (record != null)
                return (Protocol.DaMode)record.DaType;

            var chip = MtkChipDatabase.GetChip(hwCode);
            if (chip != null)
                return (Protocol.DaMode)chip.DaMode;

            return Protocol.DaMode.Xml;  // 默认使用 XML DA
        }

        /// <summary>
        /// 获取芯片的签名长度
        /// </summary>
        public static int GetSignatureLength(ushort hwCode, bool isDa2 = false)
        {
            var record = GetDaRecord(hwCode);
            if (record != null)
                return isDa2 ? record.Da2SigLen : record.Da1SigLen;

            // 根据 DA 类型返回默认签名长度
            var daMode = GetDaMode(hwCode);
            return daMode switch
            {
                DaMode.Xml => 0x30,
                DaMode.XFlash => 0x100,
                DaMode.Legacy => 0x100,
                _ => 0x30
            };
        }

        /// <summary>
        /// 检查芯片是否支持漏洞利用
        /// </summary>
        public static bool SupportsExploit(ushort hwCode)
        {
            var record = GetDaRecord(hwCode);
            return record?.SupportsExploit ?? false;
        }

        /// <summary>
        /// 从 AllInOne DA 文件中提取指定芯片的 DA
        /// </summary>
        public static (DaEntry da1, DaEntry da2)? ExtractDaFromAllInOne(ushort hwCode, DaLoader loader)
        {
            var data = GetAllInOneDaData();
            if (data == null)
                return null;

            return loader.ParseDaData(data, hwCode);
        }

        /// <summary>
        /// 注册自定义 DA 数据
        /// </summary>
        public static void RegisterCustomDa(ushort hwCode, byte[] da1Data, byte[] da2Data = null)
        {
            var record = GetDaRecord(hwCode);
            if (record == null)
            {
                record = new MtkDaRecord
                {
                    HwCode = hwCode,
                    Name = $"Custom_DA_{hwCode:X4}",
                    DaType = (int)DaMode.Xml,
                    Version = 6,
                    Da1Address = 0x200000,
                    Da2Address = 0x40000000,
                    Da1SigLen = 0x30,
                    Da2SigLen = 0x30
                };
                AddDaRecord(record);
            }

            record.EmbeddedDa1Data = da1Data;
            record.EmbeddedDa2Data = da2Data;
        }

        /// <summary>
        /// 获取自定义 DA 数据
        /// </summary>
        public static (byte[] da1, byte[] da2) GetCustomDa(ushort hwCode)
        {
            var record = GetDaRecord(hwCode);
            if (record == null)
                return (null, null);

            return (record.EmbeddedDa1Data, record.EmbeddedDa2Data);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public static (int total, int v6Count, int xflashCount, int legacyCount) GetStatistics()
        {
            int total = _daRecords.Count;
            int v6Count = _daRecords.Values.Count(r => r.DaType == (int)DaMode.Xml);
            int xflashCount = _daRecords.Values.Count(r => r.DaType == (int)DaMode.XFlash);
            int legacyCount = _daRecords.Values.Count(r => r.DaType == (int)DaMode.Legacy);

            return (total, v6Count, xflashCount, legacyCount);
        }
    }
}
