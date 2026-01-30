// ============================================================================
// SakuraEDL - MediaTek 串口配置
// 基于 MTK META UTILITY V48 逆向分析优化
// ============================================================================
// 串口参数配置:
// - 波特率: 115200 / 921600
// - 缓冲区: 81920 字节 (0x14000)
// - 读取超时: 30000ms
// ============================================================================

using System;
using System.IO.Ports;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// MTK 串口配置常量 (基于 V48 分析)
    /// </summary>
    public static class MtkSerialConfig
    {
        // ═══════════════════════════════════════════════════════════════════
        // 波特率配置
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>默认波特率 (用于初始握手)</summary>
        public const int BAUD_RATE_DEFAULT = 115200;
        
        /// <summary>高速波特率 (用于数据传输)</summary>
        public const int BAUD_RATE_HIGH = 921600;
        
        /// <summary>备用波特率</summary>
        public const int BAUD_RATE_ALT = 460800;
        
        // ═══════════════════════════════════════════════════════════════════
        // 缓冲区配置 (基于 V48 sub_103F890 分析)
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>串口缓冲区大小 (V48: 0x14000 = 81920 bytes)</summary>
        public const int BUFFER_SIZE = 0x14000;  // 81920 bytes
        
        /// <summary>大文件传输缓冲区大小</summary>
        public const int LARGE_BUFFER_SIZE = 16 * 1024 * 1024;  // 16MB
        
        // ═══════════════════════════════════════════════════════════════════
        // 超时配置 (基于 V48 sub_103F890 分析)
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>默认读取超时 (ms)</summary>
        public const int READ_TIMEOUT_DEFAULT = 5000;
        
        /// <summary>V48 读取超时 (ms)</summary>
        public const int READ_TIMEOUT_V48 = 30000;
        
        /// <summary>握手超时 (ms)</summary>
        public const int HANDSHAKE_TIMEOUT = 30000;
        
        /// <summary>Exploit 操作超时 (ms)</summary>
        public const int EXPLOIT_TIMEOUT = 30000;
        
        /// <summary>写入超时 (ms)</summary>
        public const int WRITE_TIMEOUT = 5000;
        
        // ═══════════════════════════════════════════════════════════════════
        // 静默检测超时
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>静默超时 - 用于检测数据传输完成 (ms)</summary>
        public const int SILENCE_TIMEOUT = 2000;
        
        /// <summary>最大等待时间 (ms)</summary>
        public const int MAX_WAIT_TIMEOUT = 60000;
        
        // ═══════════════════════════════════════════════════════════════════
        // 公共方法
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// 配置串口为 V48 优化参数
        /// </summary>
        public static void ConfigureForV48(SerialPort port)
        {
            if (port == null) return;
            
            port.ReadBufferSize = BUFFER_SIZE;
            port.WriteBufferSize = BUFFER_SIZE;
            port.ReadTimeout = READ_TIMEOUT_V48;
            port.WriteTimeout = WRITE_TIMEOUT;
        }
        
        /// <summary>
        /// 配置串口为大文件传输
        /// </summary>
        public static void ConfigureForLargeTransfer(SerialPort port)
        {
            if (port == null) return;
            
            port.ReadBufferSize = LARGE_BUFFER_SIZE;
            port.WriteBufferSize = LARGE_BUFFER_SIZE;
            port.ReadTimeout = MAX_WAIT_TIMEOUT;
            port.WriteTimeout = MAX_WAIT_TIMEOUT;
        }
        
        /// <summary>
        /// 配置串口为 Exploit 操作
        /// </summary>
        public static void ConfigureForExploit(SerialPort port)
        {
            if (port == null) return;
            
            port.ReadBufferSize = BUFFER_SIZE;
            port.WriteBufferSize = BUFFER_SIZE;
            port.ReadTimeout = EXPLOIT_TIMEOUT;
            port.WriteTimeout = WRITE_TIMEOUT;
        }
        
        /// <summary>
        /// 创建优化配置的串口
        /// </summary>
        public static SerialPort CreateOptimizedPort(string portName, int baudRate = BAUD_RATE_DEFAULT)
        {
            var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadBufferSize = BUFFER_SIZE,
                WriteBufferSize = BUFFER_SIZE,
                ReadTimeout = READ_TIMEOUT_V48,
                WriteTimeout = WRITE_TIMEOUT,
                DtrEnable = true,
                RtsEnable = true
            };
            
            return port;
        }
        
        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public static string GetConfigSummary(SerialPort port)
        {
            if (port == null) return "Port is null";
            
            return $"Port: {port.PortName}, Baud: {port.BaudRate}, " +
                   $"Buffer: {port.ReadBufferSize}, ReadTimeout: {port.ReadTimeout}ms";
        }
    }
    
    /// <summary>
    /// 串口配置预设
    /// </summary>
    public enum SerialConfigPreset
    {
        /// <summary>默认配置</summary>
        Default,
        
        /// <summary>V48 优化配置</summary>
        V48Optimized,
        
        /// <summary>大文件传输</summary>
        LargeTransfer,
        
        /// <summary>Exploit 操作</summary>
        Exploit
    }
}
