using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace LoveAlways.Fastboot.Image
{
    /// <summary>
    /// Android Sparse 镜像解析器
    /// 基于 AOSP libsparse 实现
    /// 
    /// Sparse 镜像格式：
    /// - Header (28 bytes)
    /// - Chunk[] 
    ///   - Chunk Header (12 bytes)
    ///   - Chunk Data (variable)
    /// </summary>
    public class SparseImage : IDisposable
    {
        // Sparse 魔数
        public const uint SPARSE_HEADER_MAGIC = 0xED26FF3A;
        
        // Chunk 类型
        public const ushort CHUNK_TYPE_RAW = 0xCAC1;
        public const ushort CHUNK_TYPE_FILL = 0xCAC2;
        public const ushort CHUNK_TYPE_DONT_CARE = 0xCAC3;
        public const ushort CHUNK_TYPE_CRC32 = 0xCAC4;
        
        private Stream _stream;
        private SparseHeader _header;
        private List<SparseChunk> _chunks;
        private bool _isSparse;
        private bool _disposed;
        
        /// <summary>
        /// 是否是 Sparse 镜像
        /// </summary>
        public bool IsSparse => _isSparse;
        
        /// <summary>
        /// 原始文件大小（解压后）
        /// </summary>
        public long OriginalSize => _isSparse ? (long)_header.TotalBlocks * _header.BlockSize : _stream.Length;
        
        /// <summary>
        /// Sparse 文件大小
        /// </summary>
        public long SparseSize => _stream.Length;
        
        /// <summary>
        /// 块大小
        /// </summary>
        public uint BlockSize => _isSparse ? _header.BlockSize : 4096;
        
        /// <summary>
        /// 总块数
        /// </summary>
        public uint TotalBlocks => _isSparse ? _header.TotalBlocks : (uint)((_stream.Length + BlockSize - 1) / BlockSize);
        
        /// <summary>
        /// Chunk 数量
        /// </summary>
        public int ChunkCount => _chunks?.Count ?? 0;
        
        /// <summary>
        /// Sparse Header
        /// </summary>
        public SparseHeader Header => _header;
        
        /// <summary>
        /// 所有 Chunks
        /// </summary>
        public IReadOnlyList<SparseChunk> Chunks => _chunks;
        
        public SparseImage(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _chunks = new List<SparseChunk>();
            
            ParseHeader();
        }
        
        public SparseImage(string filePath)
            : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
        }
        
        private void ParseHeader()
        {
            _stream.Position = 0;
            
            // 读取魔数
            byte[] magicBytes = new byte[4];
            if (_stream.Read(magicBytes, 0, 4) != 4)
            {
                _isSparse = false;
                return;
            }
            
            uint magic = BitConverter.ToUInt32(magicBytes, 0);
            if (magic != SPARSE_HEADER_MAGIC)
            {
                _isSparse = false;
                return;
            }
            
            _isSparse = true;
            _stream.Position = 0;
            
            // 读取完整 header
            byte[] headerBytes = new byte[28];
            _stream.Read(headerBytes, 0, 28);
            
            _header = new SparseHeader
            {
                Magic = BitConverter.ToUInt32(headerBytes, 0),
                MajorVersion = BitConverter.ToUInt16(headerBytes, 4),
                MinorVersion = BitConverter.ToUInt16(headerBytes, 6),
                FileHeaderSize = BitConverter.ToUInt16(headerBytes, 8),
                ChunkHeaderSize = BitConverter.ToUInt16(headerBytes, 10),
                BlockSize = BitConverter.ToUInt32(headerBytes, 12),
                TotalBlocks = BitConverter.ToUInt32(headerBytes, 16),
                TotalChunks = BitConverter.ToUInt32(headerBytes, 20),
                ImageChecksum = BitConverter.ToUInt32(headerBytes, 24)
            };
            
            // 跳过额外的 header 数据
            if (_header.FileHeaderSize > 28)
            {
                _stream.Position = _header.FileHeaderSize;
            }
            
            // 解析所有 chunks
            ParseChunks();
        }
        
        private void ParseChunks()
        {
            _chunks.Clear();
            
            for (uint i = 0; i < _header.TotalChunks; i++)
            {
                byte[] chunkHeader = new byte[12];
                if (_stream.Read(chunkHeader, 0, 12) != 12)
                    break;
                
                var chunk = new SparseChunk
                {
                    Type = BitConverter.ToUInt16(chunkHeader, 0),
                    Reserved = BitConverter.ToUInt16(chunkHeader, 2),
                    ChunkBlocks = BitConverter.ToUInt32(chunkHeader, 4),
                    TotalSize = BitConverter.ToUInt32(chunkHeader, 8),
                    DataOffset = _stream.Position
                };
                
                // 计算数据大小
                uint dataSize = chunk.TotalSize - _header.ChunkHeaderSize;
                chunk.DataSize = dataSize;
                
                // 跳过数据部分
                _stream.Position += dataSize;
                
                _chunks.Add(chunk);
            }
        }
        
        /// <summary>
        /// 将 Sparse 镜像转换为原始数据流
        /// </summary>
        public Stream ToRawStream()
        {
            if (!_isSparse)
            {
                _stream.Position = 0;
                return _stream;
            }
            
            return new SparseToRawStream(this, _stream);
        }
        
        /// <summary>
        /// 分割为多个 Sparse 块用于传输
        /// </summary>
        /// <param name="maxSize">每块最大大小</param>
        public IEnumerable<SparseChunkData> SplitForTransfer(long maxSize)
        {
            if (!_isSparse)
            {
                // 非 Sparse 镜像，直接分块
                _stream.Position = 0;
                long remaining = _stream.Length;
                int chunkIndex = 0;
                
                while (remaining > 0)
                {
                    int chunkSize = (int)Math.Min(remaining, maxSize);
                    byte[] data = new byte[chunkSize];
                    _stream.Read(data, 0, chunkSize);
                    
                    yield return new SparseChunkData
                    {
                        Index = chunkIndex++,
                        TotalChunks = (int)((_stream.Length + maxSize - 1) / maxSize),
                        Data = data,
                        Size = chunkSize
                    };
                    
                    remaining -= chunkSize;
                }
            }
            else
            {
                // Sparse 镜像，按 chunk 分块
                int totalChunks = _chunks.Count;
                for (int i = 0; i < totalChunks; i++)
                {
                    var chunk = _chunks[i];
                    
                    // 读取 chunk header + data
                    _stream.Position = chunk.DataOffset - _header.ChunkHeaderSize;
                    
                    int totalSize = (int)chunk.TotalSize;
                    byte[] data = new byte[totalSize];
                    _stream.Read(data, 0, totalSize);
                    
                    yield return new SparseChunkData
                    {
                        Index = i,
                        TotalChunks = totalChunks,
                        Data = data,
                        Size = totalSize,
                        ChunkType = chunk.Type,
                        ChunkBlocks = chunk.ChunkBlocks
                    };
                }
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _stream?.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Sparse Header
    /// </summary>
    public struct SparseHeader
    {
        public uint Magic;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ushort FileHeaderSize;
        public ushort ChunkHeaderSize;
        public uint BlockSize;
        public uint TotalBlocks;
        public uint TotalChunks;
        public uint ImageChecksum;
    }
    
    /// <summary>
    /// Sparse Chunk
    /// </summary>
    public class SparseChunk
    {
        public ushort Type;
        public ushort Reserved;
        public uint ChunkBlocks;
        public uint TotalSize;
        public uint DataSize;
        public long DataOffset;
        
        public string TypeName
        {
            get
            {
                switch (Type)
                {
                    case SparseImage.CHUNK_TYPE_RAW: return "RAW";
                    case SparseImage.CHUNK_TYPE_FILL: return "FILL";
                    case SparseImage.CHUNK_TYPE_DONT_CARE: return "DONT_CARE";
                    case SparseImage.CHUNK_TYPE_CRC32: return "CRC32";
                    default: return $"UNKNOWN({Type:X4})";
                }
            }
        }
    }
    
    /// <summary>
    /// 用于传输的 Chunk 数据
    /// </summary>
    public class SparseChunkData
    {
        public int Index;
        public int TotalChunks;
        public byte[] Data;
        public int Size;
        public ushort ChunkType;
        public uint ChunkBlocks;
    }
    
    /// <summary>
    /// Sparse 到 Raw 的流转换器
    /// </summary>
    internal class SparseToRawStream : Stream
    {
        private readonly SparseImage _sparse;
        private readonly Stream _source;
        private long _position;
        private readonly long _length;
        
        public SparseToRawStream(SparseImage sparse, Stream source)
        {
            _sparse = sparse;
            _source = source;
            _length = sparse.OriginalSize;
        }
        
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => _position = value;
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            // 简化实现：遍历 chunks 找到对应位置的数据
            int totalRead = 0;
            long currentBlockOffset = 0;
            
            foreach (var chunk in _sparse.Chunks)
            {
                long chunkStartOffset = currentBlockOffset * _sparse.BlockSize;
                long chunkEndOffset = (currentBlockOffset + chunk.ChunkBlocks) * _sparse.BlockSize;
                
                if (_position >= chunkStartOffset && _position < chunkEndOffset)
                {
                    long posInChunk = _position - chunkStartOffset;
                    int toRead = (int)Math.Min(count - totalRead, chunkEndOffset - _position);
                    
                    switch (chunk.Type)
                    {
                        case SparseImage.CHUNK_TYPE_RAW:
                            _source.Position = chunk.DataOffset + posInChunk;
                            int read = _source.Read(buffer, offset + totalRead, toRead);
                            totalRead += read;
                            _position += read;
                            break;
                            
                        case SparseImage.CHUNK_TYPE_FILL:
                            _source.Position = chunk.DataOffset;
                            byte[] fillValue = new byte[4];
                            _source.Read(fillValue, 0, 4);
                            for (int i = 0; i < toRead; i++)
                            {
                                buffer[offset + totalRead + i] = fillValue[i % 4];
                            }
                            totalRead += toRead;
                            _position += toRead;
                            break;
                            
                        case SparseImage.CHUNK_TYPE_DONT_CARE:
                            Array.Clear(buffer, offset + totalRead, toRead);
                            totalRead += toRead;
                            _position += toRead;
                            break;
                    }
                    
                    if (totalRead >= count)
                        break;
                }
                
                currentBlockOffset += chunk.ChunkBlocks;
            }
            
            return totalRead;
        }
        
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: _position = offset; break;
                case SeekOrigin.Current: _position += offset; break;
                case SeekOrigin.End: _position = _length + offset; break;
            }
            return _position;
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
