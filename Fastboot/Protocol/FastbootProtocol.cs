using System;
using System.Text;

namespace LoveAlways.Fastboot.Protocol
{
    /// <summary>
    /// Fastboot 协议定义
    /// 基于 Google AOSP platform/system/core/fastboot 源码分析
    /// 
    /// 协议格式：
    /// - 命令：ASCII 字符串，最大 64 字节
    /// - 响应：4 字节前缀 + 可选数据
    ///   - "OKAY" - 命令成功
    ///   - "FAIL" - 命令失败，后跟错误信息
    ///   - "DATA" - 准备接收数据，后跟 8 字节十六进制长度
    ///   - "INFO" - 信息消息，后跟文本
    /// </summary>
    public static class FastbootProtocol
    {
        // 协议常量
        public const int MAX_COMMAND_LENGTH = 64;
        public const int MAX_RESPONSE_LENGTH = 256;
        public const int RESPONSE_PREFIX_LENGTH = 4;
        public const int DEFAULT_TIMEOUT_MS = 30000;
        public const int DATA_TIMEOUT_MS = 60000;
        
        // USB 常量
        public const int USB_VENDOR_ID_GOOGLE = 0x18D1;
        public const int USB_PRODUCT_ID_FASTBOOT = 0x4EE0;
        public const int USB_CLASS_FASTBOOT = 0xFF;
        public const int USB_SUBCLASS_FASTBOOT = 0x42;
        public const int USB_PROTOCOL_FASTBOOT = 0x03;
        
        // 响应前缀
        public const string RESPONSE_OKAY = "OKAY";
        public const string RESPONSE_FAIL = "FAIL";
        public const string RESPONSE_DATA = "DATA";
        public const string RESPONSE_INFO = "INFO";
        public const string RESPONSE_TEXT = "TEXT";
        
        // 标准命令
        public const string CMD_GETVAR = "getvar";
        public const string CMD_DOWNLOAD = "download";
        public const string CMD_FLASH = "flash";
        public const string CMD_ERASE = "erase";
        public const string CMD_BOOT = "boot";
        public const string CMD_REBOOT = "reboot";
        public const string CMD_REBOOT_BOOTLOADER = "reboot-bootloader";
        public const string CMD_REBOOT_FASTBOOT = "reboot-fastboot";
        public const string CMD_REBOOT_RECOVERY = "reboot-recovery";
        public const string CMD_CONTINUE = "continue";
        public const string CMD_SET_ACTIVE = "set_active";
        public const string CMD_FLASHING_UNLOCK = "flashing unlock";
        public const string CMD_FLASHING_LOCK = "flashing lock";
        public const string CMD_OEM = "oem";
        
        // 常用变量名
        public const string VAR_VERSION = "version";
        public const string VAR_PRODUCT = "product";
        public const string VAR_SERIALNO = "serialno";
        public const string VAR_SECURE = "secure";
        public const string VAR_UNLOCKED = "unlocked";
        public const string VAR_MAX_DOWNLOAD_SIZE = "max-download-size";
        public const string VAR_CURRENT_SLOT = "current-slot";
        public const string VAR_SLOT_COUNT = "slot-count";
        public const string VAR_HAS_SLOT = "has-slot";
        public const string VAR_PARTITION_SIZE = "partition-size";
        public const string VAR_PARTITION_TYPE = "partition-type";
        public const string VAR_IS_LOGICAL = "is-logical";
        public const string VAR_IS_USERSPACE = "is-userspace";
        public const string VAR_SNAPSHOT_UPDATE_STATUS = "snapshot-update-status";
        public const string VAR_ALL = "all";
        
        /// <summary>
        /// 构建命令字节
        /// </summary>
        public static byte[] BuildCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command));
                
            if (command.Length > MAX_COMMAND_LENGTH)
                throw new ArgumentException($"命令长度超过 {MAX_COMMAND_LENGTH} 字节");
                
            return Encoding.ASCII.GetBytes(command);
        }
        
        /// <summary>
        /// 构建带参数的命令
        /// </summary>
        public static byte[] BuildCommand(string command, string argument)
        {
            return BuildCommand($"{command}:{argument}");
        }
        
        /// <summary>
        /// 构建下载命令（指定数据大小）
        /// </summary>
        public static byte[] BuildDownloadCommand(long size)
        {
            // 格式: download:XXXXXXXX (8位十六进制)
            return BuildCommand($"{CMD_DOWNLOAD}:{size:x8}");
        }
        
        /// <summary>
        /// 解析响应
        /// </summary>
        public static FastbootResponse ParseResponse(byte[] data, int length)
        {
            if (data == null || length < RESPONSE_PREFIX_LENGTH)
            {
                return new FastbootResponse
                {
                    Type = ResponseType.Unknown,
                    RawData = data,
                    Message = "响应数据无效"
                };
            }
            
            string response = Encoding.ASCII.GetString(data, 0, length);
            string prefix = response.Substring(0, RESPONSE_PREFIX_LENGTH);
            string payload = length > RESPONSE_PREFIX_LENGTH 
                ? response.Substring(RESPONSE_PREFIX_LENGTH) 
                : string.Empty;
            
            var result = new FastbootResponse
            {
                RawData = data,
                RawString = response,
                Message = payload
            };
            
            switch (prefix)
            {
                case RESPONSE_OKAY:
                    result.Type = ResponseType.Okay;
                    break;
                    
                case RESPONSE_FAIL:
                    result.Type = ResponseType.Fail;
                    break;
                    
                case RESPONSE_DATA:
                    result.Type = ResponseType.Data;
                    // 解析数据长度 (8位十六进制)
                    if (payload.Length >= 8)
                    {
                        try
                        {
                            result.DataSize = Convert.ToInt64(payload.Substring(0, 8), 16);
                        }
                        catch { }
                    }
                    break;
                    
                case RESPONSE_INFO:
                    result.Type = ResponseType.Info;
                    break;
                    
                case RESPONSE_TEXT:
                    result.Type = ResponseType.Text;
                    break;
                    
                default:
                    result.Type = ResponseType.Unknown;
                    result.Message = response;
                    break;
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 响应类型
    /// </summary>
    public enum ResponseType
    {
        Unknown,
        Okay,       // 命令成功
        Fail,       // 命令失败
        Data,       // 准备接收数据
        Info,       // 信息消息
        Text        // 文本消息
    }
    
    /// <summary>
    /// Fastboot 响应
    /// </summary>
    public class FastbootResponse
    {
        public ResponseType Type { get; set; }
        public string Message { get; set; }
        public string RawString { get; set; }
        public byte[] RawData { get; set; }
        public long DataSize { get; set; }
        
        public bool IsSuccess => Type == ResponseType.Okay;
        public bool IsFail => Type == ResponseType.Fail;
        public bool IsData => Type == ResponseType.Data;
        public bool IsInfo => Type == ResponseType.Info;
        
        public override string ToString()
        {
            return $"[{Type}] {Message}";
        }
    }
}
