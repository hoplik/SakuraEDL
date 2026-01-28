// ============================================================================
// SakuraEDL - MediaTek DA 解析器
// MediaTek Download Agent Parser
// ============================================================================
// 参考: Penumbra 项目 https://github.com/shomykohai/penumbra/blob/main/src/da/da.rs
// 支持多SoC DA文件解析，完整的DA结构提取
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// DA Region (DA段)
    /// </summary>
    public class DaRegion
    {
        /// <summary>DA文件中的偏移位置</summary>
        public uint FileOffset { get; set; }
        
        /// <summary>总长度（含签名）</summary>
        public uint TotalLength { get; set; }
        
        /// <summary>加载到设备的内存地址</summary>
        public uint LoadAddress { get; set; }
        
        /// <summary>Region长度（不含签名）</summary>
        public uint RegionLength { get; set; }
        
        /// <summary>签名长度</summary>
        public uint SignatureLength { get; set; }
        
        /// <summary>实际数据（不含签名）</summary>
        public byte[] Data { get; set; }
        
        /// <summary>签名数据</summary>
        public byte[] Signature { get; set; }

        public override string ToString()
        {
            return $"Region@0x{LoadAddress:X8}: Offset=0x{FileOffset:X}, Length=0x{TotalLength:X} (Data=0x{RegionLength:X}, Sig=0x{SignatureLength:X})";
        }
    }

    /// <summary>
    /// DA Entry (单个芯片的DA配置)
    /// </summary>
    public class DaEntry
    {
        /// <summary>Magic (通常为 "DADA")</summary>
        public ushort Magic { get; set; }
        
        /// <summary>HW Code (芯片代码，如 0x6768 for MT6768)</summary>
        public ushort HwCode { get; set; }
        
        /// <summary>HW Sub Code (芯片子代码，用于区分修订版)</summary>
        public ushort HwSubCode { get; set; }
        
        /// <summary>HW Version (芯片版本)</summary>
        public ushort HwVersion { get; set; }
        
        /// <summary>Entry Region Index</summary>
        public ushort RegionIndex { get; set; }
        
        /// <summary>Region数量</summary>
        public ushort RegionCount { get; set; }
        
        /// <summary>所有Region列表</summary>
        public List<DaRegion> Regions { get; set; }

        public DaEntry()
        {
            Regions = new List<DaRegion>();
        }

        public override string ToString()
        {
            return $"DA Entry: HW=0x{HwCode:X4}, SubCode=0x{HwSubCode:X4}, Ver=0x{HwVersion:X4}, Regions={RegionCount}";
        }
    }

    /// <summary>
    /// DA File (完整的DA文件)
    /// </summary>
    public class DaFile
    {
        /// <summary>DA文件魔术字符串 ("MTK_DOWNLOAD_AGENT")</summary>
        public string Magic { get; set; }
        
        /// <summary>DA文件ID ("MTK_AllInOne_DA_v3" for XFlash, "MTK_DA_v6" for XML)</summary>
        public string FileId { get; set; }
        
        /// <summary>DA版本（通常为4）</summary>
        public uint Version { get; set; }
        
        /// <summary>DA Magic (0x99886622)</summary>
        public uint DaMagic { get; set; }
        
        /// <summary>SoC数量（一个DA文件可包含多个芯片的配置）</summary>
        public ushort SocCount { get; set; }
        
        /// <summary>所有DA Entry列表</summary>
        public List<DaEntry> Entries { get; set; }
        
        /// <summary>是否为V6 (XML) DA</summary>
        public bool IsV6 => FileId?.Contains("v6") == true;
        
        /// <summary>是否为V5 (XFlash) DA</summary>
        public bool IsV5 => FileId?.Contains("v3") == true;  // "AllInOne_DA_v3" 实际是V5

        public DaFile()
        {
            Entries = new List<DaEntry>();
        }

        /// <summary>
        /// 根据HW Code查找对应的DA Entry
        /// </summary>
        public DaEntry FindEntry(ushort hwCode)
        {
            return Entries.Find(e => e.HwCode == hwCode);
        }

        public override string ToString()
        {
            return $"DA File: {FileId}, Version={Version}, SoCs={SocCount}, Type={(IsV6 ? "V6/XML" : IsV5 ? "V5/XFlash" : "Legacy")}";
        }
    }

    /// <summary>
    /// MTK DA 解析器
    /// 支持 Legacy (V3), XFlash (V5), XML (V6) DA文件
    /// </summary>
    public static class MtkDaParser
    {
        private const string EXPECTED_MAGIC = "MTK_DOWNLOAD_AGENT";
        private const uint EXPECTED_DA_MAGIC = 0x99886622;
        
        private const int LEGACY_ENTRY_SIZE = 0xD8;
        private const int XFLASH_ENTRY_SIZE = 0xDC;
        private const int REGION_SIZE = 0x20;

        /// <summary>
        /// 解析DA文件
        /// </summary>
        public static DaFile Parse(byte[] daData)
        {
            if (daData == null || daData.Length < 0x100)
                throw new ArgumentException("DA文件数据无效或过小");

            var da = new DaFile();
            int offset = 0;

            // 读取Magic (0x00-0x12, 18字节)
            da.Magic = ReadString(daData, offset, 18);
            offset += 0x20;  // 跳到0x20

            if (!da.Magic.StartsWith(EXPECTED_MAGIC))
                throw new FormatException($"无效的DA Magic: {da.Magic}");

            // 读取File ID (0x20-0x60, 64字节)
            da.FileId = ReadString(daData, offset, 64).TrimEnd('\0');
            offset += 0x40;  // 跳到0x60

            // 读取Version (0x60, 4字节)
            da.Version = ReadUInt32(daData, offset);
            offset += 4;

            // 读取DA Magic (0x64, 4字节)
            da.DaMagic = ReadUInt32(daData, offset);
            offset += 4;

            if (da.DaMagic != EXPECTED_DA_MAGIC)
                throw new FormatException($"无效的DA Magic: 0x{da.DaMagic:X8}, 期望: 0x{EXPECTED_DA_MAGIC:X8}");

            // 读取SoC Count (0x68, 4字节)
            da.SocCount = ReadUInt16(daData, offset);
            offset += 4;  // 跳过完整的4字节

            // 确定Entry大小 (Legacy=0xD8, XFlash/XML=0xDC)
            int entrySize = da.IsV5 || da.IsV6 ? XFLASH_ENTRY_SIZE : LEGACY_ENTRY_SIZE;

            // 解析所有DA Entry
            for (int i = 0; i < da.SocCount; i++)
            {
                var entry = ParseEntry(daData, offset, entrySize, daData);
                if (entry != null)
                {
                    da.Entries.Add(entry);
                }
                offset += entrySize;
            }

            return da;
        }

        /// <summary>
        /// 解析单个DA Entry
        /// </summary>
        private static DaEntry ParseEntry(byte[] data, int entryOffset, int entrySize, byte[] fullDaData)
        {
            if (entryOffset + entrySize > data.Length)
                return null;

            var entry = new DaEntry();
            int offset = entryOffset;

            // Magic (0x00, 2字节) - "DADA"
            entry.Magic = ReadUInt16(data, offset);
            offset += 2;

            // HW Code (0x02, 2字节)
            entry.HwCode = ReadUInt16(data, offset);
            offset += 2;

            // HW Sub Code (0x04, 2字节)
            entry.HwSubCode = ReadUInt16(data, offset);
            offset += 2;

            // HW Version (0x06, 2字节)
            entry.HwVersion = ReadUInt16(data, offset);
            offset += 2;

            // 跳过保留字段到0x10
            offset = entryOffset + 0x10;

            // Entry Region Index (0x10, 2字节)
            entry.RegionIndex = ReadUInt16(data, offset);
            offset += 2;

            // Entry Region Count (0x12, 2字节)
            entry.RegionCount = ReadUInt16(data, offset);
            offset += 2;

            // Region Table开始于0x14
            offset = entryOffset + 0x14;

            // 解析所有Region
            for (int i = 0; i < entry.RegionCount && i < 6; i++)  // 最多6个region
            {
                if (offset + REGION_SIZE > entryOffset + entrySize)
                    break;

                var region = ParseRegion(data, offset, fullDaData);
                if (region != null)
                {
                    entry.Regions.Add(region);
                }
                offset += REGION_SIZE;
            }

            return entry;
        }

        /// <summary>
        /// 解析单个Region
        /// </summary>
        private static DaRegion ParseRegion(byte[] entryData, int regionOffset, byte[] fullDaData)
        {
            var region = new DaRegion();

            // Offset (0x00, 4字节)
            region.FileOffset = ReadUInt32(entryData, regionOffset);

            // Total Length (0x04, 4字节)
            region.TotalLength = ReadUInt32(entryData, regionOffset + 4);

            // Load Address (0x08, 4字节)
            region.LoadAddress = ReadUInt32(entryData, regionOffset + 8);

            // Region Length (0x0C, 4字节)
            region.RegionLength = ReadUInt32(entryData, regionOffset + 12);

            // Signature Length (0x10, 4字节)
            region.SignatureLength = ReadUInt32(entryData, regionOffset + 16);

            // 提取实际数据（如果在DA文件范围内）
            if (region.FileOffset + region.TotalLength <= fullDaData.Length)
            {
                // 提取region数据（不含签名）
                region.Data = new byte[region.RegionLength];
                Array.Copy(fullDaData, region.FileOffset, region.Data, 0, region.RegionLength);

                // 提取签名数据
                if (region.SignatureLength > 0 && region.FileOffset + region.RegionLength + region.SignatureLength <= fullDaData.Length)
                {
                    region.Signature = new byte[region.SignatureLength];
                    Array.Copy(fullDaData, region.FileOffset + region.RegionLength, region.Signature, 0, region.SignatureLength);
                }
            }

            return region;
        }

        #region Helper Methods

        private static string ReadString(byte[] data, int offset, int maxLength)
        {
            int length = 0;
            for (int i = 0; i < maxLength && offset + i < data.Length; i++)
            {
                if (data[offset + i] == 0) break;
                length++;
            }
            return Encoding.ASCII.GetString(data, offset, length);
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        #endregion

        #region DA提取辅助方法

        /// <summary>
        /// 提取DA1数据（第一个Region）
        /// </summary>
        public static byte[] ExtractDa1(DaFile daFile, ushort hwCode)
        {
            var entry = daFile.FindEntry(hwCode);
            if (entry == null || entry.Regions.Count == 0)
                return null;

            return entry.Regions[0].Data;
        }

        /// <summary>
        /// 提取DA2数据（第二个Region）
        /// </summary>
        public static byte[] ExtractDa2(DaFile daFile, ushort hwCode)
        {
            var entry = daFile.FindEntry(hwCode);
            if (entry == null || entry.Regions.Count < 2)
                return null;

            return entry.Regions[1].Data;
        }

        /// <summary>
        /// 获取DA1签名长度
        /// </summary>
        public static uint GetDa1SigLen(DaFile daFile, ushort hwCode)
        {
            var entry = daFile.FindEntry(hwCode);
            if (entry == null || entry.Regions.Count == 0)
                return 0;

            return entry.Regions[0].SignatureLength;
        }

        /// <summary>
        /// 获取DA2签名长度
        /// </summary>
        public static uint GetDa2SigLen(DaFile daFile, ushort hwCode)
        {
            var entry = daFile.FindEntry(hwCode);
            if (entry == null || entry.Regions.Count < 2)
                return 0;

            return entry.Regions[1].SignatureLength;
        }

        #endregion
    }
}
