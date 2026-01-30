// ============================================================================
// SakuraEDL - MTK Logger | 联发科日志系统
// ============================================================================
// [ZH] 日志系统 - 统一的日志格式化输出，支持多种级别和样式
// [EN] Logger System - Unified log formatting with multiple levels and styles
// [JA] ログシステム - 統一されたログ形式、複数レベルとスタイル対応
// [KO] 로그 시스템 - 통합 로그 형식, 다양한 레벨 및 스타일 지원
// [RU] Система логирования - Единый формат с разными уровнями и стилями
// [ES] Sistema de registro - Formato unificado con niveles y estilos
// ============================================================================
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试信息</summary>
        Debug = 0,
        
        /// <summary>详细信息</summary>
        Verbose = 1,
        
        /// <summary>一般信息</summary>
        Info = 2,
        
        /// <summary>成功消息</summary>
        Success = 3,
        
        /// <summary>警告消息</summary>
        Warning = 4,
        
        /// <summary>错误消息</summary>
        Error = 5,
        
        /// <summary>严重错误</summary>
        Critical = 6
    }

    /// <summary>
    /// 日志类别
    /// </summary>
    public enum LogCategory
    {
        General,      // 通用
        Brom,         // BROM协议
        Da,           // DA协议
        XFlash,       // XFlash (V5)
        Xml,          // XML (V6)
        Exploit,      // 漏洞利用
        Security,     // 安全相关
        Device,       // 设备操作
        Network,      // 网络/串口
        Protocol      // 协议层
    }

    /// <summary>
    /// MTK 日志记录
    /// </summary>
    public class MtkLogEntry
    {
        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>日志级别</summary>
        public LogLevel Level { get; set; }
        
        /// <summary>日志类别</summary>
        public LogCategory Category { get; set; }
        
        /// <summary>消息内容</summary>
        public string Message { get; set; }
        
        /// <summary>附加数据</summary>
        public object Data { get; set; }
        
        /// <summary>异常信息</summary>
        public Exception Exception { get; set; }

        public MtkLogEntry()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// MTK 日志管理器
    /// </summary>
    public class MtkLogger
    {
        private readonly string _name;
        private readonly List<MtkLogEntry> _history;
        private readonly Action<string> _outputHandler;
        private LogLevel _minLevel;
        private bool _showTimestamp;
        private bool _showCategory;
        private bool _useColors;

        #region 构造函数

        public MtkLogger(string name = "MTK", Action<string> outputHandler = null)
        {
            _name = name;
            _outputHandler = outputHandler ?? Console.WriteLine;
            _history = new List<MtkLogEntry>();
            _minLevel = LogLevel.Info;
            _showTimestamp = true;
            _showCategory = true;
            _useColors = false;  // 默认不使用颜色（兼容性）
        }

        #endregion

        #region 配置方法

        /// <summary>设置最小日志级别</summary>
        public MtkLogger SetMinLevel(LogLevel level)
        {
            _minLevel = level;
            return this;
        }

        /// <summary>设置是否显示时间戳</summary>
        public MtkLogger ShowTimestamp(bool show)
        {
            _showTimestamp = show;
            return this;
        }

        /// <summary>设置是否显示类别</summary>
        public MtkLogger ShowCategory(bool show)
        {
            _showCategory = show;
            return this;
        }

        /// <summary>设置是否使用颜色（需要终端支持）</summary>
        public MtkLogger UseColors(bool use)
        {
            _useColors = use;
            return this;
        }

        #endregion

        #region 日志记录方法

        /// <summary>记录调试日志</summary>
        public void Debug(string message, LogCategory category = LogCategory.General)
        {
            Log(LogLevel.Debug, category, message);
        }

        /// <summary>记录详细日志</summary>
        public void Verbose(string message, LogCategory category = LogCategory.General)
        {
            Log(LogLevel.Verbose, category, message);
        }

        /// <summary>记录信息日志</summary>
        public void Info(string message, LogCategory category = LogCategory.General)
        {
            Log(LogLevel.Info, category, message);
        }

        /// <summary>记录成功日志</summary>
        public void Success(string message, LogCategory category = LogCategory.General)
        {
            Log(LogLevel.Success, category, message);
        }

        /// <summary>记录警告日志</summary>
        public void Warning(string message, LogCategory category = LogCategory.General)
        {
            Log(LogLevel.Warning, category, message);
        }

        /// <summary>记录错误日志</summary>
        public void Error(string message, LogCategory category = LogCategory.General, Exception ex = null)
        {
            var entry = new MtkLogEntry
            {
                Level = LogLevel.Error,
                Category = category,
                Message = message,
                Exception = ex
            };
            LogEntry(entry);
        }

        /// <summary>记录严重错误日志</summary>
        public void Critical(string message, LogCategory category = LogCategory.General, Exception ex = null)
        {
            var entry = new MtkLogEntry
            {
                Level = LogLevel.Critical,
                Category = category,
                Message = message,
                Exception = ex
            };
            LogEntry(entry);
        }

        /// <summary>记录基本日志</summary>
        public void Log(LogLevel level, LogCategory category, string message, object data = null)
        {
            var entry = new MtkLogEntry
            {
                Level = level,
                Category = category,
                Message = message,
                Data = data
            };
            LogEntry(entry);
        }

        #endregion

        #region 特殊格式日志

        /// <summary>记录十六进制数据</summary>
        public void LogHex(string label, byte[] data, int maxLength = 32, LogLevel level = LogLevel.Verbose)
        {
            if (data == null || level < _minLevel) return;

            var hex = BytesToHex(data, maxLength);
            var msg = $"{label}: {hex}";
            if (data.Length > maxLength)
                msg += $" ... ({data.Length} 字节)";
            
            Log(level, LogCategory.Protocol, msg);
        }

        /// <summary>记录协议命令</summary>
        public void LogCommand(string command, uint cmdCode, LogCategory category = LogCategory.Protocol)
        {
            var msg = $"→ {command} (0x{cmdCode:X8})";
            Log(LogLevel.Info, category, msg);
        }

        /// <summary>记录协议响应</summary>
        public void LogResponse(string response, uint statusCode, LogCategory category = LogCategory.Protocol)
        {
            var level = (statusCode == 0) ? LogLevel.Success : LogLevel.Warning;
            var msg = $"← {response} (0x{statusCode:X8})";
            Log(level, category, msg);
        }

        /// <summary>记录错误码</summary>
        public void LogErrorCode(uint errorCode, LogCategory category = LogCategory.General)
        {
            var formatted = MtkErrorCodes.FormatError(errorCode);
            var level = MtkErrorCodes.IsError(errorCode) ? LogLevel.Error : LogLevel.Warning;
            Log(level, category, formatted);
        }

        /// <summary>记录进度</summary>
        public void LogProgress(string operation, int current, int total, LogCategory category = LogCategory.General)
        {
            var percentage = total > 0 ? (current * 100 / total) : 0;
            var msg = $"{operation}: {current}/{total} ({percentage}%)";
            Log(LogLevel.Info, category, msg);
        }

        /// <summary>记录设备信息</summary>
        public void LogDeviceInfo(string key, object value, LogCategory category = LogCategory.Device)
        {
            var msg = $"  {key,-20}: {value}";
            Log(LogLevel.Info, category, msg);
        }

        /// <summary>记录分隔线</summary>
        public void LogSeparator(char character = '=', int length = 60)
        {
            var line = new string(character, length);
            _outputHandler?.Invoke(line);
        }

        /// <summary>记录标题</summary>
        public void LogHeader(string title, char borderChar = '=')
        {
            int totalWidth = 60;
            int padding = (totalWidth - title.Length - 2) / 2;
            
            LogSeparator(borderChar, totalWidth);
            var header = new string(' ', padding) + title + new string(' ', padding);
            if (header.Length < totalWidth) header += " ";
            _outputHandler?.Invoke(header);
            LogSeparator(borderChar, totalWidth);
        }

        #endregion

        #region 格式化输出

        private void LogEntry(MtkLogEntry entry)
        {
            if (entry.Level < _minLevel) return;

            // 添加到历史记录
            _history.Add(entry);

            // 格式化并输出
            var formatted = FormatEntry(entry);
            _outputHandler?.Invoke(formatted);

            // 如果有异常，输出异常详情
            if (entry.Exception != null && entry.Level >= LogLevel.Error)
            {
                var exDetails = FormatException(entry.Exception);
                _outputHandler?.Invoke(exDetails);
            }
        }

        private string FormatEntry(MtkLogEntry entry)
        {
            var sb = new StringBuilder();

            // 时间戳
            if (_showTimestamp)
            {
                sb.Append($"[{entry.Timestamp:HH:mm:ss.fff}] ");
            }

            // 日志级别
            var levelStr = GetLevelString(entry.Level);
            sb.Append(levelStr);

            // 类别
            if (_showCategory && entry.Category != LogCategory.General)
            {
                sb.Append($" [{entry.Category}]");
            }

            // 消息
            sb.Append(" ");
            sb.Append(entry.Message);

            return sb.ToString();
        }

        private string GetLevelString(LogLevel level)
        {
            if (_useColors)
            {
                return level switch
                {
                    LogLevel.Debug => "[DBG]",
                    LogLevel.Verbose => "[VRB]",
                    LogLevel.Info => "[INF]",
                    LogLevel.Success => "[✓]",
                    LogLevel.Warning => "[WRN]",
                    LogLevel.Error => "[ERR]",
                    LogLevel.Critical => "[!!!]",
                    _ => "[???]"
                };
            }
            else
            {
                return level switch
                {
                    LogLevel.Debug => "[DEBUG]",
                    LogLevel.Verbose => "[VERBOSE]",
                    LogLevel.Info => "[INFO]",
                    LogLevel.Success => "[SUCCESS]",
                    LogLevel.Warning => "[WARNING]",
                    LogLevel.Error => "[ERROR]",
                    LogLevel.Critical => "[CRITICAL]",
                    _ => "[UNKNOWN]"
                };
            }
        }

        private string FormatException(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("  异常详情:");
            sb.AppendLine($"    类型: {ex.GetType().Name}");
            sb.AppendLine($"    消息: {ex.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                sb.AppendLine("    堆栈跟踪:");
                var lines = ex.StackTrace.Split('\n');
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"      {line.Trim()}");
                }
            }
            if (ex.InnerException != null)
            {
                sb.AppendLine("  内部异常:");
                sb.Append(FormatException(ex.InnerException));
            }
            return sb.ToString();
        }

        #endregion

        #region 辅助方法

        /// <summary>字节数组转十六进制字符串</summary>
        private string BytesToHex(byte[] data, int maxLength)
        {
            if (data == null || data.Length == 0)
                return "[]";

            int length = Math.Min(data.Length, maxLength);
            var sb = new StringBuilder();
            
            for (int i = 0; i < length; i++)
            {
                sb.Append($"{data[i]:X2}");
                if (i < length - 1)
                    sb.Append(" ");
            }

            return sb.ToString();
        }

        /// <summary>获取历史记录</summary>
        public IReadOnlyList<MtkLogEntry> GetHistory()
        {
            return _history.AsReadOnly();
        }

        /// <summary>清除历史记录</summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        /// <summary>导出日志到文件</summary>
        public void ExportToFile(string filePath)
        {
            var lines = new List<string>();
            foreach (var entry in _history)
            {
                lines.Add(FormatEntry(entry));
            }
            System.IO.File.WriteAllLines(filePath, lines);
        }

        #endregion
    }

    /// <summary>
    /// 全局日志实例
    /// </summary>
    public static class MtkLog
    {
        private static MtkLogger _instance;
        private static readonly object _lock = new object();

        /// <summary>获取全局日志实例</summary>
        public static MtkLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MtkLogger("MTK")
                                .SetMinLevel(LogLevel.Info)
                                .ShowTimestamp(true)
                                .ShowCategory(true);
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>设置自定义日志实例</summary>
        public static void SetInstance(MtkLogger logger)
        {
            lock (_lock)
            {
                _instance = logger;
            }
        }

        /// <summary>重置为默认实例</summary>
        public static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        // 便捷方法
        public static void Debug(string message) => Instance.Debug(message);
        public static void Verbose(string message) => Instance.Verbose(message);
        public static void Info(string message) => Instance.Info(message);
        public static void Success(string message) => Instance.Success(message);
        public static void Warning(string message) => Instance.Warning(message);
        public static void Error(string message, Exception ex = null) => Instance.Error(message, LogCategory.General, ex);
        public static void Critical(string message, Exception ex = null) => Instance.Critical(message, LogCategory.General, ex);
    }

    /// <summary>
    /// 格式化的日志构建器（链式调用）
    /// </summary>
    public class MtkLogBuilder
    {
        private readonly MtkLogger _logger;
        private LogLevel _level = LogLevel.Info;
        private LogCategory _category = LogCategory.General;
        private readonly StringBuilder _message = new StringBuilder();
        private object _data;
        private Exception _exception;

        public MtkLogBuilder(MtkLogger logger)
        {
            _logger = logger;
        }

        public MtkLogBuilder Level(LogLevel level)
        {
            _level = level;
            return this;
        }

        public MtkLogBuilder Category(LogCategory category)
        {
            _category = category;
            return this;
        }

        public MtkLogBuilder Message(string msg)
        {
            _message.Clear();
            _message.Append(msg);
            return this;
        }

        public MtkLogBuilder Append(string text)
        {
            _message.Append(text);
            return this;
        }

        public MtkLogBuilder AppendLine(string text = "")
        {
            _message.AppendLine(text);
            return this;
        }

        public MtkLogBuilder Data(object data)
        {
            _data = data;
            return this;
        }

        public MtkLogBuilder Exception(Exception ex)
        {
            _exception = ex;
            return this;
        }

        public void Write()
        {
            if (_exception != null)
            {
                _logger.Error(_message.ToString(), _category, _exception);
            }
            else
            {
                _logger.Log(_level, _category, _message.ToString(), _data);
            }
        }
    }
}
