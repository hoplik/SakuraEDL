// ============================================================================
// SakuraEDL - CRC32 计算工具
// 参考: mtkclient 校验和实现
// ============================================================================

using System;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// CRC32 计算工具 (与 MTK DA 兼容)
    /// </summary>
    public static class MtkCrc32
    {
        // CRC32 查找表 (标准多项式 0xEDB88320)
        private static readonly uint[] CrcTable = new uint[256];
        
        static MtkCrc32()
        {
            // 初始化 CRC 表
            const uint polynomial = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                CrcTable[i] = crc;
            }
        }

        /// <summary>
        /// 计算数据的 CRC32 校验和
        /// </summary>
        public static uint Compute(byte[] data)
        {
            return Compute(data, 0, data.Length);
        }

        /// <summary>
        /// 计算数据的 CRC32 校验和 (指定范围)
        /// </summary>
        public static uint Compute(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            
            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// 增量计算 CRC32 (初始化)
        /// </summary>
        public static uint Initialize()
        {
            return 0xFFFFFFFF;
        }

        /// <summary>
        /// 增量计算 CRC32 (更新)
        /// </summary>
        public static uint Update(uint crc, byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc;
        }

        /// <summary>
        /// 增量计算 CRC32 (完成)
        /// </summary>
        public static uint Finalize(uint crc)
        {
            return crc ^ 0xFFFFFFFF;
        }

        /// <summary>
        /// 验证数据的 CRC32 校验和
        /// </summary>
        public static bool Verify(byte[] data, uint expectedCrc)
        {
            return Compute(data) == expectedCrc;
        }

        /// <summary>
        /// 验证数据的 CRC32 校验和 (指定范围)
        /// </summary>
        public static bool Verify(byte[] data, int offset, int length, uint expectedCrc)
        {
            return Compute(data, offset, length) == expectedCrc;
        }
    }
}
