// ============================================================================
// SakuraEDL - MediaTek 校验和计算
// MediaTek Checksum Utilities
// ============================================================================
// 参考: mtkclient 项目校验和算法
// ============================================================================

using System;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// MTK 校验和计算工具类
    /// </summary>
    public static class MtkChecksum
    {
        /// <summary>
        /// 计算 16 位 XOR 校验和 (用于 DA 上传)
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>16 位校验和</returns>
        public static ushort CalculateXor16(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            ushort checksum = 0;
            int i = 0;

            // 每 2 字节计算 XOR
            for (; i + 1 < data.Length; i += 2)
            {
                ushort word = (ushort)(data[i] | (data[i + 1] << 8));
                checksum ^= word;
            }

            // 处理剩余的单字节
            if (i < data.Length)
            {
                checksum ^= data[i];
            }

            return checksum;
        }

        /// <summary>
        /// 计算 XFlash 32 位校验和
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>32 位校验和</returns>
        public static uint CalculateXFlash32(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            uint checksum = 0;
            int pos = 0;

            // 每 4 字节累加
            for (int i = 0; i < data.Length / 4; i++)
            {
                checksum += BitConverter.ToUInt32(data, i * 4);
                pos += 4;
            }

            // 处理剩余字节
            if (data.Length % 4 != 0)
            {
                for (int i = 0; i < 4 - (data.Length % 4); i++)
                {
                    if (pos < data.Length)
                    {
                        checksum += data[pos];
                        pos++;
                    }
                }
            }

            return checksum;
        }

        /// <summary>
        /// 准备数据用于上传 (计算校验和并处理签名)
        /// </summary>
        /// <param name="data">主数据</param>
        /// <param name="sigData">签名数据 (可选)</param>
        /// <param name="maxSize">最大大小 (0 表示使用完整数据)</param>
        /// <returns>(校验和, 处理后的数据)</returns>
        public static (ushort checksum, byte[] processedData) PrepareData(byte[] data, byte[] sigData = null, int maxSize = 0)
        {
            if (data == null)
                data = Array.Empty<byte>();
            
            if (sigData == null)
                sigData = Array.Empty<byte>();

            // 裁剪数据到 maxSize
            byte[] trimmedData = data;
            if (maxSize > 0 && data.Length > maxSize)
            {
                trimmedData = new byte[maxSize];
                Array.Copy(data, trimmedData, maxSize);
            }

            // 合并数据和签名
            byte[] combined = new byte[trimmedData.Length + sigData.Length];
            Array.Copy(trimmedData, 0, combined, 0, trimmedData.Length);
            Array.Copy(sigData, 0, combined, trimmedData.Length, sigData.Length);

            // 如果长度为奇数，补零
            if (combined.Length % 2 != 0)
            {
                byte[] padded = new byte[combined.Length + 1];
                Array.Copy(combined, padded, combined.Length);
                combined = padded;
            }

            // 计算 16 位 XOR 校验和
            ushort checksum = CalculateXor16(combined);

            return (checksum, combined);
        }

        /// <summary>
        /// 验证 16 位校验和
        /// </summary>
        public static bool VerifyXor16(byte[] data, ushort expectedChecksum)
        {
            ushort calculated = CalculateXor16(data);
            return calculated == expectedChecksum;
        }

        /// <summary>
        /// 验证 XFlash 校验和
        /// </summary>
        public static bool VerifyXFlash32(byte[] data, uint expectedChecksum)
        {
            uint calculated = CalculateXFlash32(data);
            return calculated == expectedChecksum;
        }

        /// <summary>
        /// 计算 CRC16-CCITT 校验和
        /// </summary>
        public static ushort CalculateCrc16Ccitt(byte[] data, ushort initial = 0xFFFF)
        {
            if (data == null || data.Length == 0)
                return initial;

            ushort crc = initial;

            foreach (byte b in data)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }

            return crc;
        }

        /// <summary>
        /// 计算简单字节累加校验和
        /// </summary>
        public static byte CalculateByteSum(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            byte sum = 0;
            foreach (byte b in data)
            {
                sum += b;
            }
            return sum;
        }

        /// <summary>
        /// 计算 32 位字累加校验和
        /// </summary>
        public static uint CalculateWordSum32(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            uint sum = 0;
            int i = 0;

            // 每 4 字节累加
            for (; i + 3 < data.Length; i += 4)
            {
                sum += BitConverter.ToUInt32(data, i);
            }

            // 处理剩余字节
            uint remaining = 0;
            int shift = 0;
            for (; i < data.Length; i++)
            {
                remaining |= (uint)(data[i] << shift);
                shift += 8;
            }
            sum += remaining;

            return sum;
        }
    }

    /// <summary>
    /// MTK 数据打包工具
    /// </summary>
    public static class MtkDataPacker
    {
        /// <summary>
        /// 打包 32 位无符号整数 (Big-Endian)
        /// </summary>
        public static byte[] PackUInt32BE(uint value)
        {
            return new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        /// <summary>
        /// 打包 32 位无符号整数 (Little-Endian)
        /// </summary>
        public static byte[] PackUInt32LE(uint value)
        {
            return new byte[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF)
            };
        }

        /// <summary>
        /// 打包 16 位无符号整数 (Big-Endian)
        /// </summary>
        public static byte[] PackUInt16BE(ushort value)
        {
            return new byte[]
            {
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        /// <summary>
        /// 打包 16 位无符号整数 (Little-Endian)
        /// </summary>
        public static byte[] PackUInt16LE(ushort value)
        {
            return new byte[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF)
            };
        }

        /// <summary>
        /// 解包 32 位无符号整数 (Big-Endian)
        /// </summary>
        public static uint UnpackUInt32BE(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + 4)
                throw new ArgumentException("数据不足");

            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        /// <summary>
        /// 解包 32 位无符号整数 (Little-Endian)
        /// </summary>
        public static uint UnpackUInt32LE(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + 4)
                throw new ArgumentException("数据不足");

            return data[offset] |
                   ((uint)data[offset + 1] << 8) |
                   ((uint)data[offset + 2] << 16) |
                   ((uint)data[offset + 3] << 24);
        }

        /// <summary>
        /// 解包 16 位无符号整数 (Big-Endian)
        /// </summary>
        public static ushort UnpackUInt16BE(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + 2)
                throw new ArgumentException("数据不足");

            return (ushort)(((uint)data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>
        /// 解包 16 位无符号整数 (Little-Endian)
        /// </summary>
        public static ushort UnpackUInt16LE(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + 2)
                throw new ArgumentException("数据不足");

            return (ushort)(data[offset] | ((uint)data[offset + 1] << 8));
        }

        /// <summary>
        /// 写入 32 位到缓冲区 (Big-Endian)
        /// </summary>
        public static void WriteUInt32BE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        /// <summary>
        /// 写入 32 位到缓冲区 (Little-Endian)
        /// </summary>
        public static void WriteUInt32LE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>
        /// 写入 16 位到缓冲区 (Big-Endian)
        /// </summary>
        public static void WriteUInt16BE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }

        /// <summary>
        /// 写入 16 位到缓冲区 (Little-Endian)
        /// </summary>
        public static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>
        /// 打包 64 位无符号整数 (Little-Endian)
        /// </summary>
        public static byte[] PackUInt64LE(ulong value)
        {
            return new byte[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 56) & 0xFF)
            };
        }

        /// <summary>
        /// 解包 64 位无符号整数 (Little-Endian)
        /// </summary>
        public static ulong UnpackUInt64LE(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + 8)
                throw new ArgumentException("数据不足");

            return (ulong)data[offset] |
                   ((ulong)data[offset + 1] << 8) |
                   ((ulong)data[offset + 2] << 16) |
                   ((ulong)data[offset + 3] << 24) |
                   ((ulong)data[offset + 4] << 32) |
                   ((ulong)data[offset + 5] << 40) |
                   ((ulong)data[offset + 6] << 48) |
                   ((ulong)data[offset + 7] << 56);
        }

        /// <summary>
        /// 写入 64 位到缓冲区 (Little-Endian)
        /// </summary>
        public static void WriteUInt64LE(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 4] = (byte)((value >> 32) & 0xFF);
            buffer[offset + 5] = (byte)((value >> 40) & 0xFF);
            buffer[offset + 6] = (byte)((value >> 48) & 0xFF);
            buffer[offset + 7] = (byte)((value >> 56) & 0xFF);
        }

        /// <summary>
        /// 打包 64 位无符号整数 (Big-Endian)
        /// </summary>
        public static byte[] PackUInt64BE(ulong value)
        {
            return new byte[]
            {
                (byte)((value >> 56) & 0xFF),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        /// <summary>
        /// 解包 64 位无符号整数 (Big-Endian)
        /// </summary>
        public static ulong UnpackUInt64BE(byte[] data, int offset = 0)
        {
            if (data == null || data.Length < offset + 8)
                throw new ArgumentException("数据不足");

            return ((ulong)data[offset] << 56) |
                   ((ulong)data[offset + 1] << 48) |
                   ((ulong)data[offset + 2] << 40) |
                   ((ulong)data[offset + 3] << 32) |
                   ((ulong)data[offset + 4] << 24) |
                   ((ulong)data[offset + 5] << 16) |
                   ((ulong)data[offset + 6] << 8) |
                   (ulong)data[offset + 7];
        }

        /// <summary>
        /// 写入 64 位到缓冲区 (Big-Endian)
        /// </summary>
        public static void WriteUInt64BE(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)((value >> 56) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 48) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 40) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 32) & 0xFF);
            buffer[offset + 4] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 5] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 6] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 7] = (byte)(value & 0xFF);
        }
    }
}
