using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LoveAlways.Fastboot.Image;
using LoveAlways.Fastboot.Transport;

namespace LoveAlways.Fastboot.Protocol
{
    /// <summary>
    /// Fastboot 客户端核心类
    /// 基于 Google AOSP fastboot 源码重写的 C# 实现
    /// 
    /// 支持功能：
    /// - 设备检测和连接
    /// - 变量读取 (getvar)
    /// - 分区刷写 (flash) - 支持 Sparse 镜像
    /// - 分区擦除 (erase)
    /// - 重启操作 (reboot)
    /// - A/B 槽位切换
    /// - Bootloader 解锁/锁定
    /// - 实时进度回调
    /// </summary>
    public class FastbootClient : IDisposable
    {
        private IFastbootTransport _transport;
        private readonly Action<string> _log;
        private readonly Action<string> _logDetail;
        private bool _disposed;
        
        // 设备信息缓存
        private Dictionary<string, string> _variables;
        private long _maxDownloadSize = 512 * 1024 * 1024; // 默认 512MB
        
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _transport?.IsConnected ?? false;
        
        /// <summary>
        /// 设备序列号
        /// </summary>
        public string Serial => _transport?.DeviceId;
        
        /// <summary>
        /// 最大下载大小
        /// </summary>
        public long MaxDownloadSize => _maxDownloadSize;
        
        /// <summary>
        /// 设备变量
        /// </summary>
        public IReadOnlyDictionary<string, string> Variables => _variables;
        
        /// <summary>
        /// 进度更新事件
        /// </summary>
        public event EventHandler<FastbootProgressEventArgs> ProgressChanged;
        
        public FastbootClient(Action<string> log = null, Action<string> logDetail = null)
        {
            _log = log ?? (msg => { });
            _logDetail = logDetail ?? (msg => { });
            _variables = new Dictionary<string, string>();
        }
        
        #region 设备连接
        
        /// <summary>
        /// 枚举所有 Fastboot 设备
        /// </summary>
        public static List<FastbootDeviceDescriptor> GetDevices()
        {
            return UsbTransport.EnumerateDevices();
        }
        
        /// <summary>
        /// 连接到设备
        /// </summary>
        public async Task<bool> ConnectAsync(FastbootDeviceDescriptor device, CancellationToken ct = default)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            
            Disconnect();
            
            _log($"连接设备: {device}");
            
            if (device.Type == TransportType.Usb)
            {
                _transport = new UsbTransport(device);
            }
            else
            {
                throw new NotSupportedException("暂不支持 TCP 连接");
            }
            
            if (!await _transport.ConnectAsync(ct))
            {
                _log("连接失败");
                return false;
            }
            
            _log("连接成功");
            
            // 读取设备信息
            await RefreshDeviceInfoAsync(ct);
            
            return true;
        }
        
        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _transport?.Disconnect();
            _transport?.Dispose();
            _transport = null;
            _variables.Clear();
        }
        
        #endregion
        
        #region 基础命令
        
        /// <summary>
        /// 发送命令并等待响应
        /// </summary>
        public async Task<FastbootResponse> SendCommandAsync(string command, int timeoutMs = FastbootProtocol.DEFAULT_TIMEOUT_MS, CancellationToken ct = default)
        {
            EnsureConnected();
            
            _logDetail($">>> {command}");
            
            byte[] cmdBytes = FastbootProtocol.BuildCommand(command);
            byte[] response = await _transport.TransferAsync(cmdBytes, timeoutMs, ct);
            
            if (response == null || response.Length == 0)
            {
                return new FastbootResponse { Type = ResponseType.Fail, Message = "无响应" };
            }
            
            var result = FastbootProtocol.ParseResponse(response, response.Length);
            _logDetail($"<<< {result}");
            
            // 处理 INFO 消息（可能有多个）
            while (result.IsInfo)
            {
                _log($"INFO: {result.Message}");
                
                // 继续读取下一个响应
                response = await ReceiveResponseAsync(timeoutMs, ct);
                if (response == null) break;
                
                result = FastbootProtocol.ParseResponse(response, response.Length);
                _logDetail($"<<< {result}");
            }
            
            return result;
        }
        
        private async Task<byte[]> ReceiveResponseAsync(int timeoutMs, CancellationToken ct)
        {
            byte[] buffer = new byte[FastbootProtocol.MAX_RESPONSE_LENGTH];
            int received = await _transport.ReceiveAsync(buffer, 0, buffer.Length, timeoutMs, ct);
            
            if (received > 0)
            {
                byte[] result = new byte[received];
                Array.Copy(buffer, result, received);
                return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取变量值
        /// </summary>
        public async Task<string> GetVariableAsync(string name, CancellationToken ct = default)
        {
            var response = await SendCommandAsync($"{FastbootProtocol.CMD_GETVAR}:{name}", FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            
            if (response.IsSuccess)
            {
                return response.Message;
            }
            
            return null;
        }
        
        /// <summary>
        /// 刷新设备信息
        /// </summary>
        public async Task RefreshDeviceInfoAsync(CancellationToken ct = default)
        {
            _variables.Clear();
            
            // 读取常用变量
            string[] importantVars = {
                FastbootProtocol.VAR_PRODUCT,
                FastbootProtocol.VAR_SERIALNO,
                FastbootProtocol.VAR_SECURE,
                FastbootProtocol.VAR_UNLOCKED,
                FastbootProtocol.VAR_MAX_DOWNLOAD_SIZE,
                FastbootProtocol.VAR_CURRENT_SLOT,
                FastbootProtocol.VAR_SLOT_COUNT,
                FastbootProtocol.VAR_IS_USERSPACE
            };
            
            foreach (var varName in importantVars)
            {
                try
                {
                    string value = await GetVariableAsync(varName, ct);
                    if (!string.IsNullOrEmpty(value))
                    {
                        _variables[varName] = value;
                        
                        // 解析 max-download-size
                        if (varName == FastbootProtocol.VAR_MAX_DOWNLOAD_SIZE)
                        {
                            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            {
                                _maxDownloadSize = Convert.ToInt64(value.Substring(2), 16);
                            }
                            else if (long.TryParse(value, out long size))
                            {
                                _maxDownloadSize = size;
                            }
                        }
                    }
                }
                catch { }
            }
            
            _log($"设备: {GetVariableValue(FastbootProtocol.VAR_PRODUCT, "未知")}");
            _log($"序列号: {GetVariableValue(FastbootProtocol.VAR_SERIALNO, "未知")}");
            _log($"最大下载: {_maxDownloadSize / 1024 / 1024} MB");
        }
        
        private string GetVariableValue(string key, string defaultValue = null)
        {
            if (_variables.TryGetValue(key, out string value))
                return value;
            return defaultValue;
        }
        
        #endregion
        
        #region 刷写操作
        
        /// <summary>
        /// 刷写分区
        /// </summary>
        /// <param name="partition">分区名</param>
        /// <param name="imagePath">镜像文件路径</param>
        /// <param name="progress">进度回调</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> FlashAsync(string partition, string imagePath, 
            IProgress<FastbootProgressEventArgs> progress = null, CancellationToken ct = default)
        {
            if (!File.Exists(imagePath))
            {
                _log($"文件不存在: {imagePath}");
                return false;
            }
            
            using (var image = new SparseImage(imagePath))
            {
                return await FlashAsync(partition, image, progress, ct);
            }
        }
        
        /// <summary>
        /// 刷写分区（从 SparseImage）
        /// </summary>
        public async Task<bool> FlashAsync(string partition, SparseImage image,
            IProgress<FastbootProgressEventArgs> progress = null, CancellationToken ct = default)
        {
            EnsureConnected();
            
            long totalSize = image.SparseSize;
            _log($"刷写 {partition}: {totalSize / 1024} KB ({(image.IsSparse ? "Sparse" : "Raw")})");
            
            // 如果文件大于 max-download-size，需要分块
            if (totalSize > _maxDownloadSize && !image.IsSparse)
            {
                _log($"文件过大，需要 Resparse");
                // TODO: 实现 resparse
                return false;
            }
            
            // 分块传输
            int chunkIndex = 0;
            int totalChunks = 0;
            long totalSent = 0;
            
            foreach (var chunk in image.SplitForTransfer(_maxDownloadSize))
            {
                ct.ThrowIfCancellationRequested();
                
                if (totalChunks == 0)
                    totalChunks = chunk.TotalChunks;
                
                // 报告进度: Sending
                var progressArgs = new FastbootProgressEventArgs
                {
                    Partition = partition,
                    Stage = ProgressStage.Sending,
                    CurrentChunk = chunkIndex + 1,
                    TotalChunks = totalChunks,
                    BytesSent = totalSent,
                    TotalBytes = totalSize,
                    Percent = totalChunks > 1 
                        ? (chunkIndex * 100.0 / totalChunks) 
                        : (totalSent * 100.0 / totalSize)
                };
                
                ReportProgress(progressArgs);
                progress?.Report(progressArgs);
                
                // 发送 download 命令
                var downloadResponse = await SendCommandAsync(
                    $"{FastbootProtocol.CMD_DOWNLOAD}:{chunk.Size:x8}",
                    FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
                
                if (!downloadResponse.IsData)
                {
                    _log($"下载失败: {downloadResponse.Message}");
                    return false;
                }
                
                // 发送数据
                long expectedSize = downloadResponse.DataSize;
                if (expectedSize != chunk.Size)
                {
                    _log($"数据大小不匹配: 期望 {expectedSize}, 实际 {chunk.Size}");
                }
                
                // 分块发送数据
                int offset = 0;
                int blockSize = 512 * 1024; // 512KB 块
                while (offset < chunk.Size)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    int toSend = Math.Min(blockSize, chunk.Size - offset);
                    await _transport.SendAsync(chunk.Data, offset, toSend, ct);
                    
                    offset += toSend;
                    totalSent += toSend;
                    
                    // 更新进度
                    progressArgs.BytesSent = totalSent;
                    progressArgs.Percent = totalSent * 50.0 / totalSize; // Sending 占 50%
                    ReportProgress(progressArgs);
                    progress?.Report(progressArgs);
                }
                
                // 等待 OKAY
                var dataResponse = await ReceiveResponseAsync(FastbootProtocol.DATA_TIMEOUT_MS, ct);
                if (dataResponse == null)
                {
                    _log("数据传输超时");
                    return false;
                }
                
                var dataResult = FastbootProtocol.ParseResponse(dataResponse, dataResponse.Length);
                if (!dataResult.IsSuccess)
                {
                    _log($"数据传输失败: {dataResult.Message}");
                    return false;
                }
                
                // 发送 flash 命令
                progressArgs.Stage = ProgressStage.Writing;
                progressArgs.Percent = 50 + (chunkIndex + 1) * 50.0 / totalChunks;
                ReportProgress(progressArgs);
                progress?.Report(progressArgs);
                
                string flashCmd = totalChunks > 1
                    ? $"{FastbootProtocol.CMD_FLASH}:{partition}:{chunkIndex}/{totalChunks}"
                    : $"{FastbootProtocol.CMD_FLASH}:{partition}";
                
                var flashResponse = await SendCommandAsync(flashCmd, FastbootProtocol.DATA_TIMEOUT_MS, ct);
                
                if (!flashResponse.IsSuccess)
                {
                    _log($"刷写失败: {flashResponse.Message}");
                    return false;
                }
                
                chunkIndex++;
            }
            
            // 完成
            var completeArgs = new FastbootProgressEventArgs
            {
                Partition = partition,
                Stage = ProgressStage.Complete,
                CurrentChunk = totalChunks,
                TotalChunks = totalChunks,
                BytesSent = totalSize,
                TotalBytes = totalSize,
                Percent = 100
            };
            ReportProgress(completeArgs);
            progress?.Report(completeArgs);
            
            _log($"刷写 {partition} 完成");
            return true;
        }
        
        /// <summary>
        /// 擦除分区
        /// </summary>
        public async Task<bool> EraseAsync(string partition, CancellationToken ct = default)
        {
            EnsureConnected();
            
            _log($"擦除 {partition}...");
            
            var response = await SendCommandAsync(
                $"{FastbootProtocol.CMD_ERASE}:{partition}",
                FastbootProtocol.DATA_TIMEOUT_MS, ct);
            
            if (response.IsSuccess)
            {
                _log($"擦除 {partition} 完成");
                return true;
            }
            
            _log($"擦除失败: {response.Message}");
            return false;
        }
        
        #endregion
        
        #region 重启操作
        
        /// <summary>
        /// 重启到系统
        /// </summary>
        public async Task<bool> RebootAsync(CancellationToken ct = default)
        {
            _log("重启到系统...");
            var response = await SendCommandAsync(FastbootProtocol.CMD_REBOOT, FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            Disconnect();
            return response.IsSuccess;
        }
        
        /// <summary>
        /// 重启到 Bootloader
        /// </summary>
        public async Task<bool> RebootBootloaderAsync(CancellationToken ct = default)
        {
            _log("重启到 Bootloader...");
            var response = await SendCommandAsync(FastbootProtocol.CMD_REBOOT_BOOTLOADER, FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            return response.IsSuccess;
        }
        
        /// <summary>
        /// 重启到 Fastbootd
        /// </summary>
        public async Task<bool> RebootFastbootdAsync(CancellationToken ct = default)
        {
            _log("重启到 Fastbootd...");
            var response = await SendCommandAsync(FastbootProtocol.CMD_REBOOT_FASTBOOT, FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            return response.IsSuccess;
        }
        
        /// <summary>
        /// 重启到 Recovery
        /// </summary>
        public async Task<bool> RebootRecoveryAsync(CancellationToken ct = default)
        {
            _log("重启到 Recovery...");
            var response = await SendCommandAsync(FastbootProtocol.CMD_REBOOT_RECOVERY, FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            Disconnect();
            return response.IsSuccess;
        }
        
        #endregion
        
        #region 解锁/锁定
        
        /// <summary>
        /// 解锁 Bootloader
        /// </summary>
        public async Task<bool> UnlockAsync(CancellationToken ct = default)
        {
            _log("解锁 Bootloader...");
            var response = await SendCommandAsync(FastbootProtocol.CMD_FLASHING_UNLOCK, FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            return response.IsSuccess;
        }
        
        /// <summary>
        /// 锁定 Bootloader
        /// </summary>
        public async Task<bool> LockAsync(CancellationToken ct = default)
        {
            _log("锁定 Bootloader...");
            var response = await SendCommandAsync(FastbootProtocol.CMD_FLASHING_LOCK, FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            return response.IsSuccess;
        }
        
        #endregion
        
        #region A/B 槽位
        
        /// <summary>
        /// 设置活动槽位
        /// </summary>
        public async Task<bool> SetActiveSlotAsync(string slot, CancellationToken ct = default)
        {
            _log($"设置活动槽位: {slot}");
            var response = await SendCommandAsync(
                $"{FastbootProtocol.CMD_SET_ACTIVE}:{slot}",
                FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
            return response.IsSuccess;
        }
        
        /// <summary>
        /// 获取当前槽位
        /// </summary>
        public async Task<string> GetCurrentSlotAsync(CancellationToken ct = default)
        {
            return await GetVariableAsync(FastbootProtocol.VAR_CURRENT_SLOT, ct);
        }
        
        #endregion
        
        #region OEM 命令
        
        /// <summary>
        /// 执行 OEM 命令
        /// </summary>
        public async Task<FastbootResponse> OemCommandAsync(string command, CancellationToken ct = default)
        {
            return await SendCommandAsync($"{FastbootProtocol.CMD_OEM} {command}", FastbootProtocol.DEFAULT_TIMEOUT_MS, ct);
        }
        
        #endregion
        
        #region 辅助方法
        
        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("设备未连接");
        }
        
        private void ReportProgress(FastbootProgressEventArgs args)
        {
            ProgressChanged?.Invoke(this, args);
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 进度阶段
    /// </summary>
    public enum ProgressStage
    {
        Idle,
        Sending,
        Writing,
        Complete,
        Failed
    }
    
    /// <summary>
    /// 进度事件参数
    /// </summary>
    public class FastbootProgressEventArgs : EventArgs
    {
        public string Partition { get; set; }
        public ProgressStage Stage { get; set; }
        public int CurrentChunk { get; set; }
        public int TotalChunks { get; set; }
        public long BytesSent { get; set; }
        public long TotalBytes { get; set; }
        public double Percent { get; set; }
        public double SpeedBps { get; set; }
        public string Message { get; set; }
        
        public string PercentFormatted => $"{Percent:F1}%";
        
        public string SpeedFormatted
        {
            get
            {
                if (SpeedBps >= 1024 * 1024)
                    return $"{SpeedBps / 1024 / 1024:F2} MB/s";
                if (SpeedBps >= 1024)
                    return $"{SpeedBps / 1024:F2} KB/s";
                return $"{SpeedBps:F0} B/s";
            }
        }
    }
}
