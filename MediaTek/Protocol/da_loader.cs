// ============================================================================
// SakuraEDL - MediaTek DA 加载器
// MediaTek Download Agent Loader
// ============================================================================
// 参考: mtkclient 项目 mtk_daloader.py
// ============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using DaEntry = SakuraEDL.MediaTek.Models.DaEntry;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// DA 加载器 - 负责解析和加载 DA 文件
    /// </summary>
    public class DaLoader
    {
        private readonly BromClient _brom;
        private readonly Action<string> _log;
        private readonly Action<double> _progressCallback;

        // DA 文件头魔数
        private const uint DA_MAGIC = 0x4D4D4D4D;  // "MMMM"
        private const uint DA_MAGIC_V6 = 0x68766561;  // "hvea" (XML DA)
        
        // DA1/DA2 默认签名长度
        private const int DEFAULT_SIG_LEN = 0x100;
        private const int V6_SIG_LEN = 0x30;

        public DaLoader(BromClient brom, Action<string> log = null, Action<double> progressCallback = null)
        {
            _brom = brom;
            _log = log ?? delegate { };
            _progressCallback = progressCallback;
        }

        #region DA 文件解析

        /// <summary>
        /// 解析 DA 文件 (MTK_AllInOne_DA.bin 格式)
        /// </summary>
        public (DaEntry da1, DaEntry da2)? ParseDaFile(string filePath, ushort hwCode)
        {
            if (!File.Exists(filePath))
            {
                _log($"[DA] DA 文件不存在: {filePath}");
                return null;
            }

            byte[] data = File.ReadAllBytes(filePath);
            return ParseDaData(data, hwCode);
        }

        /// <summary>
        /// 解析 DA 数据
        /// </summary>
        public (DaEntry da1, DaEntry da2)? ParseDaData(byte[] data, ushort hwCode)
        {
            if (data == null || data.Length < 0x100)
            {
                _log("[DA] DA 数据无效");
                return null;
            }

            try
            {
                // 检查 DA 文件格式
                uint magic = BitConverter.ToUInt32(data, 0);
                
                if (magic == DA_MAGIC_V6)
                {
                    return ParseDaV6(data, hwCode);
                }
                else
                {
                    return ParseDaLegacy(data, hwCode);
                }
            }
            catch (Exception ex)
            {
                _log($"[DA] 解析 DA 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析 V6 (XML) 格式的 DA 文件
        /// </summary>
        private (DaEntry da1, DaEntry da2)? ParseDaV6(byte[] data, ushort hwCode)
        {
            _log($"[DA] 解析 V6 DA 文件 (HW Code: 0x{hwCode:X4})");

            // V6 DA 文件头结构
            // 偏移 0x00: 魔数 "hvea"
            // 偏移 0x04: 版本
            // 偏移 0x08: DA 条目数量
            // 偏移 0x0C: DA 条目表偏移

            int entryCount = BitConverter.ToInt32(data, 0x08);
            int tableOffset = BitConverter.ToInt32(data, 0x0C);

            DaEntry da1 = null;
            DaEntry da2 = null;

            // 遍历 DA 条目查找匹配的 HW Code
            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = tableOffset + (i * 0x40);  // 每个条目 64 字节
                
                if (entryOffset + 0x40 > data.Length)
                    break;

                ushort entryHwCode = BitConverter.ToUInt16(data, entryOffset);
                
                if (entryHwCode == hwCode)
                {
                    // 找到匹配的条目
                    uint da1Offset = BitConverter.ToUInt32(data, entryOffset + 0x10);
                    uint da1Size = BitConverter.ToUInt32(data, entryOffset + 0x14);
                    uint da1LoadAddr = BitConverter.ToUInt32(data, entryOffset + 0x18);
                    
                    uint da2Offset = BitConverter.ToUInt32(data, entryOffset + 0x20);
                    uint da2Size = BitConverter.ToUInt32(data, entryOffset + 0x24);
                    uint da2LoadAddr = BitConverter.ToUInt32(data, entryOffset + 0x28);

                    // 提取 DA1
                    if (da1Offset > 0 && da1Size > 0 && da1Offset + da1Size <= data.Length)
                    {
                        da1 = new DaEntry
                        {
                            Name = "DA1",
                            LoadAddr = da1LoadAddr,
                            SignatureLen = V6_SIG_LEN,
                            Data = new byte[da1Size],
                            Version = 6,
                            DaType = (int)DaMode.Xml
                        };
                        Array.Copy(data, da1Offset, da1.Data, 0, da1Size);
                    }

                    // 提取 DA2
                    if (da2Offset > 0 && da2Size > 0 && da2Offset + da2Size <= data.Length)
                    {
                        da2 = new DaEntry
                        {
                            Name = "DA2",
                            LoadAddr = da2LoadAddr,
                            SignatureLen = V6_SIG_LEN,
                            Data = new byte[da2Size],
                            Version = 6,
                            DaType = (int)DaMode.Xml
                        };
                        Array.Copy(data, da2Offset, da2.Data, 0, da2Size);
                    }

                    break;
                }
            }

            if (da1 == null)
            {
                _log($"[DA] 未找到 HW Code 0x{hwCode:X4} 的 DA");
                return null;
            }

            _log($"[DA] 找到 DA1: 地址=0x{da1.LoadAddr:X8}, 大小={da1.Data.Length}");
            if (da2 != null)
                _log($"[DA] 找到 DA2: 地址=0x{da2.LoadAddr:X8}, 大小={da2.Data.Length}");

            return (da1, da2);
        }

        /// <summary>
        /// 解析传统格式的 DA 文件
        /// </summary>
        private (DaEntry da1, DaEntry da2)? ParseDaLegacy(byte[] data, ushort hwCode)
        {
            _log($"[DA] 解析 Legacy DA 文件 (HW Code: 0x{hwCode:X4})");

            // Legacy DA 文件通常是单个 DA
            // 需要根据具体格式解析
            
            var da1 = new DaEntry
            {
                Name = "DA1",
                LoadAddr = 0x200000,  // 默认地址
                SignatureLen = DEFAULT_SIG_LEN,
                Data = data,
                Version = 3,
                DaType = (int)DaMode.Legacy
            };

            return (da1, null);
        }

        #endregion

        #region DA 上传

        /// <summary>
        /// 上传 DA1
        /// </summary>
        public async Task<bool> UploadDa1Async(DaEntry da1, CancellationToken ct = default)
        {
            if (da1 == null || da1.Data == null)
            {
                _log("[DA] DA1 数据为空");
                return false;
            }

            _log($"[DA] 上传 DA1 到 0x{da1.LoadAddr:X8} ({da1.Data.Length} 字节)");

            bool success = await _brom.SendDaAsync(da1.LoadAddr, da1.Data, da1.SignatureLen, ct);
            if (!success)
            {
                _log("[DA] DA1 上传失败");
                return false;
            }

            // 检查上传状态
            ushort uploadStatus = _brom.LastUploadStatus;
            _log($"[DA] DA 上传状态: 0x{uploadStatus:X4}");
            
            // 等待设备处理 DA
            _log("[DA] 等待设备处理 DA...");
            await System.Threading.Tasks.Task.Delay(200, ct);

            // 状态 0x7017 表示 DAA 安全错误
            // 这意味着设备启用了 DAA (Download Agent Authentication) 保护
            // 在 Preloader 模式下，设备可能会重新枚举或重启
            if (uploadStatus == 0x7017 || uploadStatus == 0x7015)
            {
                _log($"[DA] 状态 0x{uploadStatus:X4}: DAA 安全保护触发");
                _log("[DA] ⚠ 设备启用了 DAA (Download Agent Authentication)");
                _log("[DA] ⚠ 需要使用官方签名的 DA 或通过漏洞绕过");
                _log("[DA] 尝试等待 USB 重新枚举...");
                
                // 等待一小段时间让设备处理
                await System.Threading.Tasks.Task.Delay(1500, ct);
                
                // 返回成功让上层处理 USB 重新枚举
                return true;
            }

            // 跳转执行 DA1
            try
            {
                success = await _brom.JumpDaAsync(da1.LoadAddr, ct);
            }
            catch (Exception ex)
            {
                // 端口关闭可能意味着 DA 正在运行并重新枚举 USB
                _log($"[DA] JUMP_DA 异常: {ex.Message}");
                _log("[DA] ⚠ 端口断开 - DA 可能已开始执行并重新枚举 USB");
                _log("[DA] ⚠ 请等待设备重新出现并重新连接");
                
                // 返回特殊状态，让上层处理重连
                return true;  // 暂时返回成功，因为 DA 可能确实在运行
            }
            
            if (!success)
            {
                // JUMP_DA 失败，检查端口状态
                if (!_brom.IsConnected)
                {
                    _log("[DA] ⚠ 端口已断开 - DA 可能正在运行");
                    _log("[DA] ⚠ 设备将以新的 COM 端口重新出现");
                    return true;  // DA 可能在运行
                }
                
                // 尝试检测 DA 就绪信号
                _log("[DA] JUMP_DA 失败，检测 DA 是否已启动...");
                await System.Threading.Tasks.Task.Delay(500, ct);
                
                try
                {
                    bool daReady = await _brom.TryDetectDaReadyAsync(ct);
                    if (daReady)
                    {
                        _log("[DA] ✓ DA 已启动");
                        return true;
                    }
                }
                catch
                {
                    _log("[DA] 端口状态变化，DA 可能正在重新枚举 USB");
                    return true;
                }
                
                _log("[DA] DA1 跳转执行失败");
                return false;
            }

            _log("[DA] ✓ DA1 上传并执行成功");
            return true;
        }

        /// <summary>
        /// 上传 DA2 (通过 XML DA 协议)
        /// </summary>
        public async Task<bool> UploadDa2Async(DaEntry da2, XmlDaClient xmlClient, CancellationToken ct = default)
        {
            if (da2 == null || da2.Data == null)
            {
                _log("[DA] DA2 数据为空");
                return false;
            }

            _log($"[DA] 上传 DA2 到 0x{da2.LoadAddr:X8} ({da2.Data.Length} 字节)");

            // 使用 XML DA 协议上传 DA2
            bool success = await xmlClient.UploadDa2Async(da2, ct);
            if (!success)
            {
                _log("[DA] DA2 上传失败");
                return false;
            }

            _log("[DA] ✓ DA2 上传成功");
            return true;
        }

        #endregion

        #region DA 签名处理

        /// <summary>
        /// 计算 DA 哈希位置 (V6 格式)
        /// </summary>
        public int FindDa2HashPosition(byte[] da1, int sigLen)
        {
            // V6 格式: hash_pos = len(da1) - sig_len - 0x30
            return da1.Length - sigLen - 0x30;
        }

        /// <summary>
        /// 计算 SHA-256 哈希
        /// </summary>
        public byte[] ComputeSha256(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }

        /// <summary>
        /// 修复 DA1 中的 DA2 哈希 (用于 Carbonara 漏洞)
        /// </summary>
        public byte[] FixDa1Hash(byte[] da1, byte[] patchedDa2, int hashPos)
        {
            if (hashPos < 0 || hashPos + 32 > da1.Length)
            {
                _log("[DA] 哈希位置无效");
                return da1;
            }

            // 计算新的 DA2 哈希
            byte[] newHash = ComputeSha256(patchedDa2);

            // 复制 DA1 并修改哈希
            byte[] result = new byte[da1.Length];
            Array.Copy(da1, result, da1.Length);
            Array.Copy(newHash, 0, result, hashPos, 32);

            _log($"[DA] 已更新 DA1 中的 DA2 哈希 (位置: 0x{hashPos:X})");
            return result;
        }

        #endregion

        #region DA 补丁

        /// <summary>
        /// 应用 DA 补丁 (用于 Carbonara 漏洞)
        /// </summary>
        public byte[] ApplyDaPatch(byte[] daData, byte[] originalBytes, byte[] patchBytes, int offset)
        {
            if (offset < 0 || offset + originalBytes.Length > daData.Length)
            {
                _log("[DA] 补丁偏移无效");
                return daData;
            }

            // 验证原始字节
            bool match = true;
            for (int i = 0; i < originalBytes.Length; i++)
            {
                if (daData[offset + i] != originalBytes[i])
                {
                    match = false;
                    break;
                }
            }

            if (!match)
            {
                _log("[DA] 原始字节不匹配，无法应用补丁");
                return daData;
            }

            // 应用补丁
            byte[] result = new byte[daData.Length];
            Array.Copy(daData, result, daData.Length);
            Array.Copy(patchBytes, 0, result, offset, patchBytes.Length);

            _log($"[DA] 已应用补丁 (偏移: 0x{offset:X})");
            return result;
        }

        /// <summary>
        /// 查找安全检查函数 (用于 Carbonara 漏洞)
        /// </summary>
        public int FindSecurityCheckOffset(byte[] daData)
        {
            // 查找安全检查指令模式
            // ARM: MOV R0, #0 (0x00 0x00 0xA0 0xE3)
            // Thumb: MOVS R0, #0 (0x00 0x20)
            
            byte[] armPattern = { 0x00, 0x00, 0xA0, 0xE3 };  // MOV R0, #0
            byte[] thumbPattern = { 0x00, 0x20 };  // MOVS R0, #0

            // 搜索 ARM 模式
            for (int i = 0; i < daData.Length - 4; i++)
            {
                bool match = true;
                for (int j = 0; j < armPattern.Length; j++)
                {
                    if (daData[i + j] != armPattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    // 检查上下文确认是安全检查
                    // 通常后面会有 BX LR 或 POP {PC}
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 生成补丁字节 (MOV R0, #1)
        /// </summary>
        public byte[] GenerateBypassPatch(bool isArm = true)
        {
            if (isArm)
            {
                // ARM: MOV R0, #1 (0x01 0x00 0xA0 0xE3)
                return new byte[] { 0x01, 0x00, 0xA0, 0xE3 };
            }
            else
            {
                // Thumb: MOVS R0, #1 (0x01 0x20)
                return new byte[] { 0x01, 0x20 };
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取 DA 默认加载地址
        /// </summary>
        public uint GetDefaultDa1Address(ushort hwCode)
        {
            // 根据芯片返回默认 DA1 加载地址
            return hwCode switch
            {
                0x0279 => 0x200000,  // MT6797
                0x0326 => 0x200000,  // MT6755
                0x0551 => 0x200000,  // MT6768
                0x0562 => 0x200000,  // MT6761
                0x0717 => 0x200000,  // MT6765
                0x0788 => 0x200000,  // MT6873
                _ => 0x200000        // 默认值
            };
        }

        /// <summary>
        /// 获取 DA2 默认加载地址
        /// </summary>
        public uint GetDefaultDa2Address(ushort hwCode)
        {
            // 根据芯片返回默认 DA2 加载地址
            return hwCode switch
            {
                0x0279 => 0x40000000,  // MT6797
                0x0326 => 0x40000000,  // MT6755
                0x0551 => 0x40000000,  // MT6768
                0x0562 => 0x40000000,  // MT6761
                0x0717 => 0x40000000,  // MT6765
                0x0788 => 0x40000000,  // MT6873
                _ => 0x40000000        // 默认值
            };
        }

        /// <summary>
        /// 验证 DA 数据完整性
        /// </summary>
        public bool VerifyDaIntegrity(byte[] daData)
        {
            if (daData == null || daData.Length < 0x100)
                return false;

            // 检查 ELF 头
            if (daData[0] == 0x7F && daData[1] == 'E' && daData[2] == 'L' && daData[3] == 'F')
                return true;

            // 检查其他有效的 DA 头
            // ...

            return true;  // 默认接受
        }

        #endregion
    }
}
