// ============================================================================
// SakuraEDL - Preloader Parser | Preloader 解析器
// ============================================================================
// [ZH] Preloader 解析 - 解析 MTK Preloader 结构和 EMI 配置
// [EN] Preloader Parser - Parse MTK Preloader structure and EMI config
// [JA] Preloader解析 - MTK Preloader構造とEMI設定の解析
// [KO] Preloader 파서 - MTK Preloader 구조 및 EMI 설정 분석
// [RU] Парсер Preloader - Разбор структуры MTK Preloader и конфигурации EMI
// [ES] Analizador Preloader - Análisis de estructura y configuración EMI
// ============================================================================
// Based on MTK META UTILITY reverse engineering
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================
// 功能:
// - 从内存中转储 Preloader
// - 解析 MTK_BLOADER_INFO 结构
// - 提取 EMI 配置信息
// - 设备识别和信息提取
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// MTK_BLOADER_INFO 结构 (来自 MTK META UTILITY 逆向分析)
    /// </summary>
    public class MtkBloaderInfo
    {
        /// <summary>魔数标识 "MTK_BLOADER_INFO"</summary>
        public const string MAGIC = "MTK_BLOADER_INFO";
        
        /// <summary>Preloader 版本</summary>
        public string Version { get; set; }
        
        /// <summary>平台名称 (如 MT6877)</summary>
        public string Platform { get; set; }
        
        /// <summary>EMI 名称 (内存配置标识)</summary>
        public string EmiName { get; set; }
        
        /// <summary>编译时间</summary>
        public string BuildTime { get; set; }
        
        /// <summary>编译器版本</summary>
        public string Compiler { get; set; }
        
        /// <summary>安全配置</summary>
        public uint SecurityConfig { get; set; }
        
        /// <summary>Preloader 起始地址</summary>
        public uint StartAddress { get; set; }
        
        /// <summary>Preloader 大小</summary>
        public uint Size { get; set; }
        
        /// <summary>原始数据偏移</summary>
        public int RawOffset { get; set; }
        
        /// <summary>原始数据</summary>
        public byte[] RawData { get; set; }
        
        public override string ToString()
        {
            return $"Platform: {Platform}\n" +
                   $"Version: {Version}\n" +
                   $"EMI Name: {EmiName}\n" +
                   $"Build Time: {BuildTime}\n" +
                   $"Security Config: 0x{SecurityConfig:X8}";
        }
    }

    /// <summary>
    /// Preloader Dump 响应码
    /// </summary>
    public enum PreloaderDumpStatus
    {
        /// <summary>成功 (0xA1A2A3A4)</summary>
        Success = 0,
        
        /// <summary>Bypass ACK (用于安全绕过)</summary>
        BypassAck = 1,
        
        /// <summary>Dump ACK (用于内存转储)</summary>
        DumpAck = 2,
        
        /// <summary>超时</summary>
        Timeout = 3,
        
        /// <summary>失败</summary>
        Failed = 4
    }

    /// <summary>
    /// Preloader 解析器
    /// </summary>
    public class PreloaderParser
    {
        private Action<string> _log;
        
        // 响应码常量 (来自 MTK META UTILITY 逆向分析)
        public const uint ACK_BYPASS = 0xA1A2A3A4;  // Exploit Bypass 成功
        public const uint ACK_DUMP = 0xC1C2C3C4;    // Preloader Dump 成功

        public PreloaderParser(Action<string> log = null)
        {
            _log = log ?? (s => { });
        }

        /// <summary>
        /// 从二进制数据中解析 MTK_BLOADER_INFO
        /// </summary>
        public MtkBloaderInfo ParseFromData(byte[] data)
        {
            if (data == null || data.Length < 256)
                return null;

            // 搜索魔数
            int offset = FindMagic(data, Encoding.ASCII.GetBytes(MtkBloaderInfo.MAGIC));
            if (offset < 0)
            {
                _log("[PreloaderParser] 未找到 MTK_BLOADER_INFO 魔数");
                return null;
            }

            _log($"[PreloaderParser] 在偏移 0x{offset:X} 找到 MTK_BLOADER_INFO");

            try
            {
                var info = new MtkBloaderInfo
                {
                    RawOffset = offset,
                    RawData = new byte[Math.Min(512, data.Length - offset)]
                };
                
                Array.Copy(data, offset, info.RawData, 0, info.RawData.Length);

                // 解析各字段 (基于 MTK META UTILITY 逆向分析)
                // 结构布局:
                // +0x00: "MTK_BLOADER_INFO" (16 bytes)
                // +0x10: 版本信息
                // +0x30: 平台名称
                // +0x50: EMI 名称
                // +0x70: 编译时间
                // +0x90: 编译器信息

                int pos = offset + 16;  // 跳过魔数
                
                // 尝试读取版本信息
                info.Version = ReadNullTerminatedString(data, pos, 32);
                pos += 32;
                
                // 平台名称
                info.Platform = ReadNullTerminatedString(data, pos, 32);
                pos += 32;
                
                // EMI 名称 - 这是关键信息
                info.EmiName = ExtractEmiName(data, offset);
                
                // 编译时间
                info.BuildTime = ReadNullTerminatedString(data, pos, 32);
                pos += 32;
                
                // 编译器
                info.Compiler = ReadNullTerminatedString(data, pos, 32);

                return info;
            }
            catch (Exception ex)
            {
                _log($"[PreloaderParser] 解析异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从 Preloader 文件解析
        /// </summary>
        public MtkBloaderInfo ParseFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _log($"[PreloaderParser] 文件不存在: {filePath}");
                return null;
            }

            byte[] data = File.ReadAllBytes(filePath);
            return ParseFromData(data);
        }

        /// <summary>
        /// 提取 EMI 名称 (使用多种方法)
        /// </summary>
        private string ExtractEmiName(byte[] data, int infoOffset)
        {
            // 方法1: 从 MTK_BLOADER_INFO 后的固定偏移读取
            string emiName = SearchForEmiPattern(data, infoOffset);
            if (!string.IsNullOrEmpty(emiName))
                return emiName;

            // 方法2: 搜索 "eminame" 字符串
            int emiOffset = FindMagic(data, Encoding.ASCII.GetBytes("eminame="));
            if (emiOffset >= 0)
            {
                emiName = ReadNullTerminatedString(data, emiOffset + 8, 64);
                if (!string.IsNullOrEmpty(emiName))
                    return emiName;
            }

            // 方法3: 搜索常见的 EMI 配置标识
            string[] emiPatterns = new[]
            {
                "LPDDR", "DDR", "PCDDR", "LPDDR4", "LPDDR4X", "LPDDR5"
            };

            foreach (var pattern in emiPatterns)
            {
                int patternOffset = FindMagic(data, Encoding.ASCII.GetBytes(pattern));
                if (patternOffset >= 0)
                {
                    // 向前向后扩展读取完整的 EMI 名称
                    int start = patternOffset;
                    while (start > 0 && IsPrintableAscii(data[start - 1]))
                        start--;
                    
                    int end = patternOffset;
                    while (end < data.Length - 1 && IsPrintableAscii(data[end + 1]))
                        end++;
                    
                    if (end > start)
                    {
                        emiName = Encoding.ASCII.GetString(data, start, end - start + 1);
                        if (emiName.Length > 4)
                            return emiName;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 搜索 EMI 模式
        /// </summary>
        private string SearchForEmiPattern(byte[] data, int startOffset)
        {
            // 在 MTK_BLOADER_INFO 附近搜索 EMI 相关字符串
            int searchStart = Math.Max(0, startOffset - 1024);
            int searchEnd = Math.Min(data.Length, startOffset + 4096);

            for (int i = searchStart; i < searchEnd - 64; i++)
            {
                // 检查是否为有效的 EMI 名称开头
                if (data[i] == 'K' || // K4xxx (Samsung)
                    data[i] == 'H' || // Hxxxx (Hynix)
                    data[i] == 'M')   // MTxxx (Micron)
                {
                    string potential = ReadNullTerminatedString(data, i, 64);
                    if (IsValidEmiName(potential))
                        return potential;
                }
            }

            return null;
        }

        /// <summary>
        /// 验证 EMI 名称是否有效
        /// </summary>
        private bool IsValidEmiName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 4)
                return false;

            // EMI 名称通常包含内存类型标识
            string[] validPrefixes = { "K4", "H9", "MT", "LPDDR", "DDR" };
            foreach (var prefix in validPrefixes)
            {
                if (name.Contains(prefix))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 搜索魔数
        /// </summary>
        private int FindMagic(byte[] data, byte[] magic)
        {
            if (data == null || magic == null || data.Length < magic.Length)
                return -1;

            for (int i = 0; i <= data.Length - magic.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < magic.Length; j++)
                {
                    if (data[i + j] != magic[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 读取以 null 结尾的字符串
        /// </summary>
        private string ReadNullTerminatedString(byte[] data, int offset, int maxLength)
        {
            if (offset < 0 || offset >= data.Length)
                return null;

            int end = offset;
            while (end < data.Length && end < offset + maxLength && data[end] != 0)
            {
                if (!IsPrintableAscii(data[end]))
                    break;
                end++;
            }

            if (end > offset)
            {
                return Encoding.ASCII.GetString(data, offset, end - offset);
            }
            return null;
        }

        /// <summary>
        /// 检查是否为可打印 ASCII 字符
        /// </summary>
        private bool IsPrintableAscii(byte b)
        {
            return b >= 0x20 && b <= 0x7E;
        }

        /// <summary>
        /// 验证 Preloader Dump 响应
        /// </summary>
        public PreloaderDumpStatus ValidateDumpResponse(byte[] response)
        {
            if (response == null || response.Length < 4)
                return PreloaderDumpStatus.Timeout;

            uint ack = BitConverter.ToUInt32(response, 0);
            
            if (ack == ACK_BYPASS)
            {
                _log("[PreloaderParser] 收到 Bypass ACK (0xA1A2A3A4)");
                return PreloaderDumpStatus.BypassAck;
            }
            
            if (ack == ACK_DUMP)
            {
                _log("[PreloaderParser] 收到 Dump ACK (0xC1C2C3C4)");
                return PreloaderDumpStatus.DumpAck;
            }

            _log($"[PreloaderParser] 未知响应: 0x{ack:X8}");
            return PreloaderDumpStatus.Failed;
        }

        /// <summary>
        /// 保存 Preloader 到文件
        /// </summary>
        public bool SavePreloader(byte[] data, string outputPath, MtkBloaderInfo info = null)
        {
            try
            {
                File.WriteAllBytes(outputPath, data);
                _log($"[PreloaderParser] Preloader 已保存到: {outputPath} ({data.Length} 字节)");

                // 如果有解析信息，保存到同目录下的 .txt 文件
                if (info != null)
                {
                    string infoPath = Path.ChangeExtension(outputPath, ".txt");
                    File.WriteAllText(infoPath, info.ToString());
                    _log($"[PreloaderParser] 信息已保存到: {infoPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _log($"[PreloaderParser] 保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从 Preloader 提取安全配置
        /// </summary>
        public Dictionary<string, bool> ExtractSecurityConfig(byte[] data)
        {
            var config = new Dictionary<string, bool>();
            
            // 搜索安全相关字符串
            string[] securityStrings = {
                "SBC", "SLA", "DAA", "SDA", 
                "SECURE_BOOT", "VERIFIED_BOOT",
                "ANTI_ROLLBACK", "DEVICE_LOCK"
            };

            foreach (var str in securityStrings)
            {
                int offset = FindMagic(data, Encoding.ASCII.GetBytes(str));
                config[str] = offset >= 0;
            }

            return config;
        }

        /// <summary>
        /// 检测 Preloader 是否启用了安全保护
        /// </summary>
        public bool IsSecurePreloader(byte[] data)
        {
            if (data == null || data.Length < 256)
                return false;

            // 检查是否包含安全启动签名
            // 签名通常位于 Preloader 末尾
            
            // 方法1: 检查末尾是否有 RSA 签名 (256 或 512 字节)
            if (data.Length > 512)
            {
                byte[] tail = new byte[512];
                Array.Copy(data, data.Length - 512, tail, 0, 512);
                
                // RSA 签名通常不会全为 0 或 0xFF
                int nonZeroCount = 0;
                int nonFfCount = 0;
                foreach (byte b in tail)
                {
                    if (b != 0x00) nonZeroCount++;
                    if (b != 0xFF) nonFfCount++;
                }

                // 如果尾部看起来像有效签名数据
                if (nonZeroCount > 128 && nonFfCount > 128)
                {
                    _log("[PreloaderParser] 检测到可能的安全签名");
                    return true;
                }
            }

            // 方法2: 检查 MTK_BLOADER_INFO 中的安全标志
            var info = ParseFromData(data);
            if (info != null && info.SecurityConfig != 0)
            {
                return true;
            }

            return false;
        }
    }
}
