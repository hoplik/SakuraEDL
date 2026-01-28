// ============================================================================
// SakuraEDL - MediaTek Key Extractor
// 提取设备密钥信息 (seccfg, efuse, rpmb keys)
// ============================================================================
// 参考: mtkclient keys.py, seccfg parser
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SakuraEDL.MediaTek.Security
{
    /// <summary>
    /// 密钥类型
    /// </summary>
    public enum KeyType
    {
        Unknown,
        MeId,           // ME ID (Mobile Equipment ID)
        SocId,          // SoC ID
        PrdKey,         // Production Key
        RpmbKey,        // RPMB Key
        FdeKey,         // Full Disk Encryption Key
        SeccfgKey,      // Seccfg Encryption Key
        HrId,           // Hardware Root ID
        PlatformKey,    // Platform Key
        OemKey,         // OEM Key
        DaaKey          // DAA Key
    }

    /// <summary>
    /// 提取的密钥信息
    /// </summary>
    public class ExtractedKey
    {
        public KeyType Type { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
        public int Length => Data?.Length ?? 0;
        public string HexString => Data != null ? BitConverter.ToString(Data).Replace("-", "") : "";
        
        public override string ToString()
        {
            return $"{Name}: {HexString} ({Length} bytes)";
        }
    }

    /// <summary>
    /// Seccfg 分区结构
    /// </summary>
    public class SeccfgData
    {
        /// <summary>魔数</summary>
        public uint Magic { get; set; }
        
        /// <summary>版本</summary>
        public uint Version { get; set; }
        
        /// <summary>锁定状态 (0=解锁, 1=锁定)</summary>
        public uint LockState { get; set; }
        
        /// <summary>关键锁定状态</summary>
        public uint CriticalLockState { get; set; }
        
        /// <summary>SBC 标志</summary>
        public uint SbcFlag { get; set; }
        
        /// <summary>防回滚版本</summary>
        public uint AntiRollbackVersion { get; set; }
        
        /// <summary>加密数据</summary>
        public byte[] EncryptedData { get; set; }
        
        /// <summary>哈希</summary>
        public byte[] Hash { get; set; }
        
        /// <summary>是否已解锁</summary>
        public bool IsUnlocked => LockState == 0;
        
        /// <summary>原始数据</summary>
        public byte[] RawData { get; set; }
    }

    /// <summary>
    /// eFuse 数据
    /// </summary>
    public class EfuseData
    {
        /// <summary>安全启动状态</summary>
        public bool SecureBootEnabled { get; set; }
        
        /// <summary>SLA 状态</summary>
        public bool SlaEnabled { get; set; }
        
        /// <summary>DAA 状态</summary>
        public bool DaaEnabled { get; set; }
        
        /// <summary>SBC 状态</summary>
        public bool SbcEnabled { get; set; }
        
        /// <summary>Root Key Hash</summary>
        public byte[] RootKeyHash { get; set; }
        
        /// <summary>防回滚版本</summary>
        public uint AntiRollbackVersion { get; set; }
        
        /// <summary>原始 eFuse 数据</summary>
        public byte[] RawData { get; set; }
    }

    /// <summary>
    /// MediaTek 密钥提取器
    /// </summary>
    public static class KeyExtractor
    {
        // Seccfg 魔数
        private const uint SECCFG_MAGIC = 0x53454343;  // "SECC"
        private const uint SECCFG_MAGIC_V2 = 0x4D4D4D01;  // MTK V2
        private const uint SECCFG_MAGIC_V3 = 0x53454346;  // "SECF"
        
        // 默认密钥 (用于未加密的 seccfg)
        private static readonly byte[] DefaultKey = new byte[16]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        /// <summary>
        /// 解析 Seccfg 分区数据
        /// </summary>
        public static SeccfgData ParseSeccfg(byte[] data)
        {
            if (data == null || data.Length < 64)
                return null;

            var seccfg = new SeccfgData
            {
                RawData = data
            };

            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                // 读取魔数
                seccfg.Magic = br.ReadUInt32();
                
                // 验证魔数
                if (seccfg.Magic != SECCFG_MAGIC && 
                    seccfg.Magic != SECCFG_MAGIC_V2 && 
                    seccfg.Magic != SECCFG_MAGIC_V3)
                {
                    // 尝试解析为未加密格式
                    ms.Position = 0;
                    return ParseSeccfgUnencrypted(data);
                }
                
                // 版本
                seccfg.Version = br.ReadUInt32();
                
                // 根据版本解析
                if (seccfg.Magic == SECCFG_MAGIC_V3)
                {
                    // V3 格式
                    ParseSeccfgV3(br, seccfg);
                }
                else if (seccfg.Magic == SECCFG_MAGIC_V2)
                {
                    // V2 格式
                    ParseSeccfgV2(br, seccfg);
                }
                else
                {
                    // V1 格式
                    ParseSeccfgV1(br, seccfg);
                }
            }

            return seccfg;
        }

        private static void ParseSeccfgV1(BinaryReader br, SeccfgData seccfg)
        {
            // 偏移 8: 锁定状态
            seccfg.LockState = br.ReadUInt32();
            seccfg.CriticalLockState = br.ReadUInt32();
            seccfg.SbcFlag = br.ReadUInt32();
            
            // 跳过保留字段
            br.ReadBytes(20);
            
            // 哈希 (32 字节)
            seccfg.Hash = br.ReadBytes(32);
        }

        private static void ParseSeccfgV2(BinaryReader br, SeccfgData seccfg)
        {
            // V2 格式有更多字段
            seccfg.LockState = br.ReadUInt32();
            seccfg.CriticalLockState = br.ReadUInt32();
            seccfg.SbcFlag = br.ReadUInt32();
            seccfg.AntiRollbackVersion = br.ReadUInt32();
            
            // 保留字段
            br.ReadBytes(16);
            
            // 哈希 (32 字节)
            seccfg.Hash = br.ReadBytes(32);
            
            // 加密数据 (如果存在)
            if (br.BaseStream.Position < br.BaseStream.Length - 64)
            {
                int remaining = (int)(br.BaseStream.Length - br.BaseStream.Position);
                seccfg.EncryptedData = br.ReadBytes(remaining);
            }
        }

        private static void ParseSeccfgV3(BinaryReader br, SeccfgData seccfg)
        {
            // V3 格式
            uint flags = br.ReadUInt32();
            seccfg.LockState = flags & 0x01;
            seccfg.CriticalLockState = (flags >> 1) & 0x01;
            seccfg.SbcFlag = (flags >> 2) & 0x01;
            
            seccfg.AntiRollbackVersion = br.ReadUInt32();
            
            // 保留字段
            br.ReadBytes(24);
            
            // 哈希 (32 字节 SHA256)
            seccfg.Hash = br.ReadBytes(32);
        }

        private static SeccfgData ParseSeccfgUnencrypted(byte[] data)
        {
            // 尝试解析未加密的 seccfg
            var seccfg = new SeccfgData
            {
                RawData = data,
                Magic = 0,
                Version = 0
            };
            
            // 搜索锁定状态标记
            // 通常在固定偏移位置
            if (data.Length >= 8)
            {
                seccfg.LockState = BitConverter.ToUInt32(data, 4);
            }
            
            return seccfg;
        }

        /// <summary>
        /// 解析 eFuse 数据
        /// </summary>
        public static EfuseData ParseEfuse(byte[] data)
        {
            if (data == null || data.Length < 32)
                return null;

            var efuse = new EfuseData
            {
                RawData = data
            };

            // eFuse 布局取决于具体芯片
            // 这是一个通用解析
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                // 安全配置通常在前 4 字节
                uint secConfig = br.ReadUInt32();
                
                efuse.SecureBootEnabled = (secConfig & 0x01) != 0;
                efuse.SlaEnabled = (secConfig & 0x02) != 0;
                efuse.DaaEnabled = (secConfig & 0x04) != 0;
                efuse.SbcEnabled = (secConfig & 0x08) != 0;
                
                // 防回滚版本
                efuse.AntiRollbackVersion = br.ReadUInt32();
                
                // Root Key Hash (如果存在)
                if (data.Length >= 40)
                {
                    br.ReadBytes(8);  // 跳过保留字段
                    efuse.RootKeyHash = br.ReadBytes(32);
                }
            }

            return efuse;
        }

        /// <summary>
        /// 从 ME ID 和 SoC ID 派生密钥
        /// </summary>
        public static byte[] DeriveKey(byte[] meId, byte[] socId)
        {
            if (meId == null || socId == null)
                return null;

            // 组合 ME ID 和 SoC ID
            var combined = new byte[meId.Length + socId.Length];
            Array.Copy(meId, 0, combined, 0, meId.Length);
            Array.Copy(socId, 0, combined, meId.Length, socId.Length);

            // 使用 SHA256 派生密钥
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(combined);
            }
        }

        /// <summary>
        /// 生成 RPMB Key
        /// </summary>
        public static byte[] GenerateRpmbKey(byte[] meId, byte[] socId, byte[] hwId = null)
        {
            // RPMB Key 通常基于设备唯一标识符派生
            var baseKey = DeriveKey(meId, socId);
            
            if (hwId != null && hwId.Length > 0)
            {
                // 如果有 HW ID，进一步派生
                using (var sha256 = SHA256.Create())
                {
                    var combined = new byte[baseKey.Length + hwId.Length];
                    Array.Copy(baseKey, 0, combined, 0, baseKey.Length);
                    Array.Copy(hwId, 0, combined, baseKey.Length, hwId.Length);
                    return sha256.ComputeHash(combined).Take(32).ToArray();
                }
            }
            
            return baseKey.Take(32).ToArray();
        }

        /// <summary>
        /// 解密 Seccfg 数据
        /// </summary>
        public static byte[] DecryptSeccfg(byte[] encryptedData, byte[] key)
        {
            if (encryptedData == null || key == null)
                return null;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = key.Take(16).ToArray();
                    aes.IV = new byte[16];  // 零 IV

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream())
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(encryptedData, 0, encryptedData.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 提取所有可用密钥
        /// </summary>
        public static List<ExtractedKey> ExtractAllKeys(
            byte[] seccfgData = null,
            byte[] efuseData = null,
            byte[] meId = null,
            byte[] socId = null)
        {
            var keys = new List<ExtractedKey>();

            // ME ID
            if (meId != null && meId.Length > 0)
            {
                keys.Add(new ExtractedKey
                {
                    Type = KeyType.MeId,
                    Name = "ME ID",
                    Data = meId
                });
            }

            // SoC ID
            if (socId != null && socId.Length > 0)
            {
                keys.Add(new ExtractedKey
                {
                    Type = KeyType.SocId,
                    Name = "SoC ID",
                    Data = socId
                });
            }

            // 派生密钥
            if (meId != null && socId != null)
            {
                var derivedKey = DeriveKey(meId, socId);
                if (derivedKey != null)
                {
                    keys.Add(new ExtractedKey
                    {
                        Type = KeyType.PrdKey,
                        Name = "Derived Key (ME+SoC)",
                        Data = derivedKey
                    });

                    // RPMB Key
                    var rpmbKey = GenerateRpmbKey(meId, socId);
                    keys.Add(new ExtractedKey
                    {
                        Type = KeyType.RpmbKey,
                        Name = "RPMB Key",
                        Data = rpmbKey
                    });
                }
            }

            // Seccfg 相关
            if (seccfgData != null)
            {
                var seccfg = ParseSeccfg(seccfgData);
                if (seccfg != null && seccfg.Hash != null)
                {
                    keys.Add(new ExtractedKey
                    {
                        Type = KeyType.SeccfgKey,
                        Name = "Seccfg Hash",
                        Data = seccfg.Hash
                    });
                }
            }

            // eFuse 相关
            if (efuseData != null)
            {
                var efuse = ParseEfuse(efuseData);
                if (efuse != null && efuse.RootKeyHash != null)
                {
                    keys.Add(new ExtractedKey
                    {
                        Type = KeyType.HrId,
                        Name = "Root Key Hash",
                        Data = efuse.RootKeyHash
                    });
                }
            }

            return keys;
        }

        /// <summary>
        /// 验证 Seccfg 完整性
        /// </summary>
        public static bool VerifySeccfgIntegrity(SeccfgData seccfg)
        {
            if (seccfg == null || seccfg.RawData == null || seccfg.Hash == null)
                return false;

            // 计算数据哈希 (不包括哈希字段本身)
            using (var sha256 = SHA256.Create())
            {
                // 找到哈希字段的位置
                int hashOffset = Array.IndexOf(seccfg.RawData, seccfg.Hash[0]);
                if (hashOffset < 0)
                    return false;

                // 计算前半部分的哈希
                var toHash = new byte[hashOffset];
                Array.Copy(seccfg.RawData, 0, toHash, 0, hashOffset);
                
                var calculated = sha256.ComputeHash(toHash);
                
                // 比较
                return calculated.Take(32).SequenceEqual(seccfg.Hash);
            }
        }

        /// <summary>
        /// 生成解锁的 Seccfg
        /// </summary>
        public static byte[] GenerateUnlockedSeccfg(SeccfgData original)
        {
            if (original == null || original.RawData == null)
                return null;

            var unlocked = (byte[])original.RawData.Clone();

            // 修改锁定状态
            // 这取决于具体的 seccfg 格式
            if (original.Magic == SECCFG_MAGIC_V3)
            {
                // V3: 标志在偏移 8
                unlocked[8] = 0x00;  // 清除锁定位
            }
            else
            {
                // V1/V2: 锁定状态在偏移 8
                unlocked[8] = 0x00;
                unlocked[9] = 0x00;
                unlocked[10] = 0x00;
                unlocked[11] = 0x00;
            }

            // 重新计算哈希 (如果需要)
            // 注意: 没有正确的密钥无法生成有效的哈希
            // 这里只是修改数据，实际使用需要正确签名

            return unlocked;
        }

        /// <summary>
        /// 导出密钥到文件
        /// </summary>
        public static bool ExportKeys(List<ExtractedKey> keys, string outputPath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# MediaTek Device Keys");
                sb.AppendLine($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                foreach (var key in keys)
                {
                    sb.AppendLine($"[{key.Type}]");
                    sb.AppendLine($"Name={key.Name}");
                    sb.AppendLine($"Length={key.Length}");
                    sb.AppendLine($"Hex={key.HexString}");
                    sb.AppendLine();
                }

                File.WriteAllText(outputPath, sb.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取密钥提取器说明
        /// </summary>
        public static string GetDescription()
        {
            return @"MediaTek Key Extractor
================================================
功能:
  - 解析 seccfg 分区 (锁定状态、安全配置)
  - 解析 eFuse 数据 (安全启动状态、防回滚版本)
  - 从 ME ID 和 SoC ID 派生密钥
  - 生成 RPMB Key
  - 验证和修改 seccfg 完整性

支持的格式:
  - Seccfg V1/V2/V3
  - eFuse 通用格式

注意:
  - 修改 seccfg 需要正确的签名密钥
  - RPMB Key 派生算法可能因厂商而异
  - 某些操作可能使设备变砖，请谨慎操作";
        }
    }
}
