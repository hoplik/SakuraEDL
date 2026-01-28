// ============================================================================
// SakuraEDL - MediaTek XFlash 二进制协议客户端
// 参考: mtkclient/Library/DA/xflash/xflash_lib.py
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Models;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// XFlash 二进制协议客户端
    /// </summary>
    public class XFlashClient : IDisposable
    {
        private SerialPort _port;
        private readonly Action<string> _log;
        private readonly Action<double> _progressCallback;
        private readonly SemaphoreSlim _portLock;
        private readonly bool _ownsPortLock;
        private bool _disposed;

        // 协议配置
        private ChecksumAlgorithm _checksumLevel = ChecksumAlgorithm.None;
        private int _packetLength = 0x1000;  // 默认 4KB
        private StorageType _storageType = StorageType.Unknown;

        // 常量
        private const int DEFAULT_TIMEOUT_MS = 30000;
        private const int MAX_BUFFER_SIZE = 0x200000;  // 2MB

        // 状态
        public bool IsConnected { get; private set; }
        public StorageType Storage => _storageType;

        public XFlashClient(SerialPort port, Action<string> log = null, Action<double> progressCallback = null, SemaphoreSlim portLock = null)
        {
            _port = port;
            _log = log ?? delegate { };
            _progressCallback = progressCallback;

            if (portLock != null)
            {
                _portLock = portLock;
                _ownsPortLock = false;
            }
            else
            {
                _portLock = new SemaphoreSlim(1, 1);
                _ownsPortLock = true;
            }
        }

        #region 底层协议

        /// <summary>
        /// 发送二进制命令
        /// </summary>
        private async Task<bool> SendCommandAsync(uint command, byte[] args = null, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                // 构建数据包: [magic(4)] [dataType(4)] [length(4)] [command(4)] [args...]
                int argsLen = args?.Length ?? 0;
                int dataLen = 4 + argsLen;  // command + args
                byte[] packet = new byte[12 + dataLen];

                MtkDataPacker.WriteUInt32LE(packet, 0, XFlashCmd.MAGIC);
                MtkDataPacker.WriteUInt32LE(packet, 4, (uint)XFlashDataType.ProtocolFlow);
                MtkDataPacker.WriteUInt32LE(packet, 8, (uint)dataLen);
                MtkDataPacker.WriteUInt32LE(packet, 12, command);

                if (args != null && args.Length > 0)
                {
                    Array.Copy(args, 0, packet, 16, args.Length);
                }

                // 计算校验和 (如果启用)
                if (_checksumLevel == ChecksumAlgorithm.CRC32)
                {
                    uint crc = MtkCrc32.Compute(packet, 12, dataLen);
                    byte[] crcPacket = new byte[packet.Length + 4];
                    Array.Copy(packet, crcPacket, packet.Length);
                    MtkDataPacker.WriteUInt32LE(crcPacket, packet.Length, crc);
                    packet = crcPacket;
                }

                _port.Write(packet, 0, packet.Length);
                return true;
            }
            catch (Exception ex)
            {
                _log($"[XFlash] 发送命令失败: {ex.Message}");
                return false;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 接收响应
        /// </summary>
        private async Task<byte[]> ReceiveResponseAsync(int timeoutMs = DEFAULT_TIMEOUT_MS, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                // 读取头部 (12 bytes)
                byte[] header = await ReadBytesAsync(12, timeoutMs, ct);
                if (header == null)
                    return null;

                uint magic = MtkDataPacker.UnpackUInt32LE(header, 0);
                if (magic != XFlashCmd.MAGIC)
                {
                    _log($"[XFlash] 魔数不匹配: 0x{magic:X8}");
                    return null;
                }

                uint dataType = MtkDataPacker.UnpackUInt32LE(header, 4);
                uint length = MtkDataPacker.UnpackUInt32LE(header, 8);

                if (length == 0)
                    return new byte[0];

                if (length > MAX_BUFFER_SIZE)
                {
                    _log($"[XFlash] 数据长度异常: {length}");
                    return null;
                }

                // 读取数据
                byte[] data = await ReadBytesAsync((int)length, timeoutMs, ct);
                
                // 验证校验和 (如果启用)
                if (_checksumLevel == ChecksumAlgorithm.CRC32 && data != null)
                {
                    byte[] crcBytes = await ReadBytesAsync(4, timeoutMs, ct);
                    if (crcBytes != null)
                    {
                        uint expectedCrc = MtkDataPacker.UnpackUInt32LE(crcBytes, 0);
                        uint actualCrc = MtkCrc32.Compute(data);
                        if (expectedCrc != actualCrc)
                        {
                            _log($"[XFlash] CRC 校验失败: 期望 0x{expectedCrc:X8}, 实际 0x{actualCrc:X8}");
                            return null;
                        }
                    }
                }

                return data;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 读取指定字节数
        /// </summary>
        private async Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct)
        {
            byte[] buffer = new byte[count];
            int received = 0;
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);

            while (received < count && DateTime.Now < deadline)
            {
                ct.ThrowIfCancellationRequested();

                int available = _port.BytesToRead;
                if (available > 0)
                {
                    int toRead = Math.Min(available, count - received);
                    int read = _port.Read(buffer, received, toRead);
                    received += read;
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }

            return received == count ? buffer : null;
        }

        /// <summary>
        /// 发送 ACK
        /// </summary>
        private async Task<bool> SendAckAsync(CancellationToken ct = default)
        {
            byte[] ack = new byte[12];
            MtkDataPacker.WriteUInt32LE(ack, 0, XFlashCmd.MAGIC);
            MtkDataPacker.WriteUInt32LE(ack, 4, (uint)XFlashDataType.ProtocolResponse);
            MtkDataPacker.WriteUInt32LE(ack, 8, 0);

            await _portLock.WaitAsync(ct);
            try
            {
                _port.Write(ack, 0, ack.Length);
                return true;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// 发送原始数据
        /// </summary>
        private async Task<bool> SendRawDataAsync(byte[] data, CancellationToken ct = default)
        {
            await _portLock.WaitAsync(ct);
            try
            {
                // 数据包头
                byte[] header = new byte[12];
                MtkDataPacker.WriteUInt32LE(header, 0, XFlashCmd.MAGIC);
                MtkDataPacker.WriteUInt32LE(header, 4, (uint)XFlashDataType.ProtocolRaw);
                MtkDataPacker.WriteUInt32LE(header, 8, (uint)data.Length);

                _port.Write(header, 0, header.Length);
                _port.Write(data, 0, data.Length);

                // 发送校验和 (如果启用)
                if (_checksumLevel == ChecksumAlgorithm.CRC32)
                {
                    uint crc = MtkCrc32.Compute(data);
                    byte[] crcBytes = new byte[4];
                    MtkDataPacker.WriteUInt32LE(crcBytes, 0, crc);
                    _port.Write(crcBytes, 0, 4);
                }

                return true;
            }
            finally
            {
                _portLock.Release();
            }
        }

        #endregion

        #region 初始化和配置

        /// <summary>
        /// 设置校验和级别
        /// </summary>
        public async Task<bool> SetChecksumLevelAsync(ChecksumAlgorithm level, CancellationToken ct = default)
        {
            _log($"[XFlash] 设置校验和级别: {level}");

            byte[] args = new byte[4];
            MtkDataPacker.WriteUInt32LE(args, 0, (uint)level);

            if (!await SendCommandAsync(XFlashCmd.SET_CHECKSUM_LEVEL, args, ct))
                return false;

            var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
            if (response == null || response.Length < 4)
                return false;

            int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
            if (status == XFlashError.OK)
            {
                _checksumLevel = level;
                _log($"[XFlash] ✓ 校验和级别已设置: {level}");
                return true;
            }

            _log($"[XFlash] 设置校验和失败: {XFlashError.GetErrorMessage(status)}");
            return false;
        }

        /// <summary>
        /// 获取数据包长度
        /// </summary>
        public async Task<int> GetPacketLengthAsync(CancellationToken ct = default)
        {
            _log("[XFlash] 获取数据包长度...");

            if (!await SendCommandAsync(XFlashCmd.GET_PACKET_LENGTH, null, ct))
                return -1;

            var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
            if (response == null || response.Length < 8)
                return -1;

            int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
            if (status == XFlashError.OK)
            {
                _packetLength = (int)MtkDataPacker.UnpackUInt32LE(response, 4);
                _log($"[XFlash] ✓ 数据包长度: {_packetLength} bytes");
                return _packetLength;
            }

            return -1;
        }

        /// <summary>
        /// 获取存储信息 (自动检测类型)
        /// </summary>
        public async Task<bool> DetectStorageAsync(CancellationToken ct = default)
        {
            _log("[XFlash] 检测存储类型...");

            // 尝试 eMMC
            if (await SendCommandAsync(XFlashCmd.GET_EMMC_INFO, null, ct))
            {
                var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
                if (response != null && response.Length >= 4)
                {
                    int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
                    if (status == XFlashError.OK)
                    {
                        _storageType = StorageType.EMMC;
                        _log("[XFlash] ✓ 检测到 eMMC 存储");
                        return true;
                    }
                }
            }

            // 尝试 UFS
            if (await SendCommandAsync(XFlashCmd.GET_UFS_INFO, null, ct))
            {
                var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
                if (response != null && response.Length >= 4)
                {
                    int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
                    if (status == XFlashError.OK)
                    {
                        _storageType = StorageType.UFS;
                        _log("[XFlash] ✓ 检测到 UFS 存储");
                        return true;
                    }
                }
            }

            // 尝试 NAND
            if (await SendCommandAsync(XFlashCmd.GET_NAND_INFO, null, ct))
            {
                var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
                if (response != null && response.Length >= 4)
                {
                    int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
                    if (status == XFlashError.OK)
                    {
                        _storageType = StorageType.NAND;
                        _log("[XFlash] ✓ 检测到 NAND 存储");
                        return true;
                    }
                }
            }

            _log("[XFlash] 未能检测到存储类型");
            return false;
        }

        #endregion

        #region 分区操作

        /// <summary>
        /// 读取分区表 (二进制协议)
        /// </summary>
        public async Task<List<MtkPartitionInfo>> ReadPartitionTableAsync(CancellationToken ct = default)
        {
            _log("[XFlash] 读取分区表...");

            if (!await SendCommandAsync(XFlashCmd.GET_PARTITION_TBL_CATA, null, ct))
                return null;

            var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS * 2, ct);
            if (response == null || response.Length < 4)
                return null;

            int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
            if (status != XFlashError.OK)
            {
                _log($"[XFlash] 读取分区表失败: {XFlashError.GetErrorMessage(status)}");
                return null;
            }

            // 解析分区表
            var partitions = new List<MtkPartitionInfo>();
            int offset = 4;

            // 读取分区数量
            if (response.Length < offset + 4)
                return partitions;

            int count = (int)MtkDataPacker.UnpackUInt32LE(response, offset);
            offset += 4;

            _log($"[XFlash] 分区数量: {count}");

            for (int i = 0; i < count && offset + 64 <= response.Length; i++)
            {
                // 分区条目格式: [name(32)] [start(8)] [size(8)] [type(4)] [flags(4)]
                string name = System.Text.Encoding.ASCII.GetString(response, offset, 32).TrimEnd('\0');
                offset += 32;

                ulong startSector = MtkDataPacker.UnpackUInt64LE(response, offset);
                offset += 8;

                ulong size = MtkDataPacker.UnpackUInt64LE(response, offset);
                offset += 8;

                uint type = MtkDataPacker.UnpackUInt32LE(response, offset);
                offset += 4;

                uint flags = MtkDataPacker.UnpackUInt32LE(response, offset);
                offset += 4;

                // 跳过可能的填充
                offset += 8;

                partitions.Add(new MtkPartitionInfo
                {
                    Name = name,
                    StartSector = startSector,
                    Size = size,
                    SectorCount = size / 512,
                    Type = type.ToString()
                });
            }

            _log($"[XFlash] ✓ 读取到 {partitions.Count} 个分区");
            return partitions;
        }

        /// <summary>
        /// 读取分区数据 (二进制协议)
        /// </summary>
        public async Task<byte[]> ReadPartitionAsync(string partitionName, ulong offset, ulong size, 
            EmmcPartitionType partType = EmmcPartitionType.User, CancellationToken ct = default)
        {
            _log($"[XFlash] 读取分区: {partitionName}, 偏移: 0x{offset:X}, 大小: {size}");

            // 构建参数: [partition_type(4)] [addr(8)] [size(8)] [storage_type(4)]
            byte[] args = new byte[24];
            MtkDataPacker.WriteUInt32LE(args, 0, (uint)partType);
            MtkDataPacker.WriteUInt64LE(args, 4, offset);
            MtkDataPacker.WriteUInt64LE(args, 12, size);
            MtkDataPacker.WriteUInt32LE(args, 20, (uint)_storageType);

            if (!await SendCommandAsync(XFlashCmd.READ_DATA, args, ct))
                return null;

            // 接收状态响应
            var statusResponse = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
            if (statusResponse == null || statusResponse.Length < 4)
                return null;

            int status = (int)MtkDataPacker.UnpackUInt32LE(statusResponse, 0);
            if (status != XFlashError.OK)
            {
                _log($"[XFlash] 读取分区失败: {XFlashError.GetErrorMessage(status)}");
                return null;
            }

            // 接收数据
            using (var ms = new MemoryStream())
            {
                ulong received = 0;
                int chunkSize = _packetLength > 0 ? _packetLength : 0x10000;

                while (received < size)
                {
                    ct.ThrowIfCancellationRequested();

                    var chunk = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
                    if (chunk == null || chunk.Length == 0)
                        break;

                    ms.Write(chunk, 0, chunk.Length);
                    received += (ulong)chunk.Length;

                    // 发送 ACK
                    await SendAckAsync(ct);

                    // 更新进度
                    double progress = (double)received * 100 / size;
                    _progressCallback?.Invoke(progress);
                }

                _log($"[XFlash] ✓ 读取完成: {received} bytes");
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 写入分区数据 (二进制协议)
        /// </summary>
        public async Task<bool> WritePartitionAsync(string partitionName, ulong offset, byte[] data,
            EmmcPartitionType partType = EmmcPartitionType.User, CancellationToken ct = default)
        {
            _log($"[XFlash] 写入分区: {partitionName}, 偏移: 0x{offset:X}, 大小: {data.Length}");

            // 构建参数: [partition_type(4)] [addr(8)] [size(8)] [storage_type(4)]
            byte[] args = new byte[24];
            MtkDataPacker.WriteUInt32LE(args, 0, (uint)partType);
            MtkDataPacker.WriteUInt64LE(args, 4, offset);
            MtkDataPacker.WriteUInt64LE(args, 12, (ulong)data.Length);
            MtkDataPacker.WriteUInt32LE(args, 20, (uint)_storageType);

            if (!await SendCommandAsync(XFlashCmd.WRITE_DATA, args, ct))
                return false;

            // 接收状态响应
            var statusResponse = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
            if (statusResponse == null || statusResponse.Length < 4)
                return false;

            int status = (int)MtkDataPacker.UnpackUInt32LE(statusResponse, 0);
            if (status != XFlashError.OK)
            {
                _log($"[XFlash] 写入准备失败: {XFlashError.GetErrorMessage(status)}");
                return false;
            }

            // 分块发送数据
            int chunkSize = _packetLength > 0 ? _packetLength : 0x10000;
            int sent = 0;

            while (sent < data.Length)
            {
                ct.ThrowIfCancellationRequested();

                int remaining = data.Length - sent;
                int toSend = Math.Min(remaining, chunkSize);

                byte[] chunk = new byte[toSend];
                Array.Copy(data, sent, chunk, 0, toSend);

                if (!await SendRawDataAsync(chunk, ct))
                    return false;

                // 等待 ACK
                var ack = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
                if (ack == null)
                {
                    _log("[XFlash] 未收到 ACK");
                    return false;
                }

                sent += toSend;

                // 更新进度
                double progress = (double)sent * 100 / data.Length;
                _progressCallback?.Invoke(progress);
            }

            // 等待完成确认
            var finalResponse = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS * 2, ct);
            if (finalResponse != null && finalResponse.Length >= 4)
            {
                status = (int)MtkDataPacker.UnpackUInt32LE(finalResponse, 0);
                if (status == XFlashError.OK)
                {
                    _log($"[XFlash] ✓ 写入完成: {sent} bytes");
                    return true;
                }
            }

            _log("[XFlash] 写入未完成");
            return false;
        }

        /// <summary>
        /// 擦除分区
        /// </summary>
        public async Task<bool> FormatPartitionAsync(string partitionName, CancellationToken ct = default)
        {
            _log($"[XFlash] 擦除分区: {partitionName}");

            // 构建参数: 分区名 (32 bytes, null terminated)
            byte[] args = new byte[36];
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(partitionName);
            Array.Copy(nameBytes, 0, args, 0, Math.Min(nameBytes.Length, 31));
            MtkDataPacker.WriteUInt32LE(args, 32, (uint)_storageType);

            if (!await SendCommandAsync(XFlashCmd.FORMAT_PARTITION, args, ct))
                return false;

            var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS * 3, ct);
            if (response == null || response.Length < 4)
                return false;

            int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
            if (status == XFlashError.OK)
            {
                _log($"[XFlash] ✓ 擦除完成: {partitionName}");
                return true;
            }

            _log($"[XFlash] 擦除失败: {XFlashError.GetErrorMessage(status)}");
            return false;
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task<bool> RebootAsync(CancellationToken ct = default)
        {
            _log("[XFlash] 重启设备...");
            return await SendCommandAsync(XFlashCmd.SHUTDOWN, null, ct);
        }

        /// <summary>
        /// 获取芯片 ID
        /// </summary>
        public async Task<string> GetChipIdAsync(CancellationToken ct = default)
        {
            _log("[XFlash] 获取芯片 ID...");

            if (!await SendCommandAsync(XFlashCmd.GET_CHIP_ID, null, ct))
                return null;

            var response = await ReceiveResponseAsync(DEFAULT_TIMEOUT_MS, ct);
            if (response == null || response.Length < 8)
                return null;

            int status = (int)MtkDataPacker.UnpackUInt32LE(response, 0);
            if (status != XFlashError.OK)
                return null;

            // 解析芯片 ID
            uint chipId = MtkDataPacker.UnpackUInt32LE(response, 4);
            string chipIdStr = $"MT{chipId:X4}";
            _log($"[XFlash] ✓ 芯片 ID: {chipIdStr}");
            return chipIdStr;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_ownsPortLock)
                {
                    _portLock?.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
