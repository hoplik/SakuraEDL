// ============================================================================
// SakuraEDL - Spreadtrum Diag Client | 展讯 Diag 客户端
// ============================================================================
// [ZH] 展讯 Diag 客户端 - IMEI/NV 读写诊断协议
// [EN] Spreadtrum Diag Client - IMEI/NV read/write diagnostic protocol
// [JA] Spreadtrum Diagクライアント - IMEI/NV読み書き診断プロトコル
// [KO] Spreadtrum Diag 클라이언트 - IMEI/NV 읽기/쓰기 진단 프로토콜
// [RU] Клиент Diag Spreadtrum - Протокол диагностики IMEI/NV
// [ES] Cliente Diag Spreadtrum - Protocolo de diagnóstico IMEI/NV
// ============================================================================
// Pure C# implementation
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SakuraEDL.Spreadtrum.Protocol
{
    /// <summary>
    /// 展讯 Diag 协议客户端
    /// 用于设备诊断、读取 NV、IMEI 等操作
    /// </summary>
    public class DiagClient : IDisposable
    {
        private SerialPort _port;
        private bool _isConnected;
        private readonly object _lock = new object();

        // HDLC 帧定界符
        private const byte HDLC_FLAG = 0x7E;
        private const byte HDLC_ESCAPE = 0x7D;
        private const byte HDLC_ESCAPE_XOR = 0x20;

        // Diag 命令
        public const byte DIAG_CMD_VERSION = 0x00;
        public const byte DIAG_CMD_IMEI_READ = 0x01;
        public const byte DIAG_CMD_IMEI_WRITE = 0x02;
        public const byte DIAG_CMD_NV_READ = 0x26;
        public const byte DIAG_CMD_NV_WRITE = 0x27;
        public const byte DIAG_CMD_SPC_UNLOCK = 0x47;
        public const byte DIAG_CMD_AT_COMMAND = 0x4B;
        public const byte DIAG_CMD_EFS_READ = 0x59;
        public const byte DIAG_CMD_EFS_WRITE = 0x5A;
        public const byte DIAG_CMD_RESTART = 0x29;
        public const byte DIAG_CMD_POWER_OFF = 0x3E;

        // NV ID 常量
        public const ushort NV_IMEI1 = 0x0005;
        public const ushort NV_IMEI2 = 0x0179;
        public const ushort NV_SN = 0x0006;
        public const ushort NV_BT_ADDR = 0x0191;
        public const ushort NV_WIFI_ADDR = 0x0192;

        // 事件
        public event Action<string> OnLog;

        // 属性
        public bool IsConnected => _isConnected;
        public string PortName => _port?.PortName;

        public DiagClient()
        {
        }

        #region 连接管理

        /// <summary>
        /// 连接 Diag 端口
        /// </summary>
        public async Task<bool> ConnectAsync(string portName, int baudRate = 115200)
        {
            try
            {
                if (_port != null && _port.IsOpen)
                    _port.Close();

                _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                _port.ReadTimeout = 5000;
                _port.WriteTimeout = 5000;
                _port.Open();

                // 发送握手
                if (await HandshakeAsync())
                {
                    _isConnected = true;
                    Log("[Diag] 连接成功: {0}", portName);
                    return true;
                }
                else
                {
                    _port.Close();
                    Log("[Diag] 握手失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log("[Diag] 连接失败: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            if (_port != null && _port.IsOpen)
            {
                _port.Close();
            }
            _isConnected = false;
            Log("[Diag] 已断开");
        }

        /// <summary>
        /// 握手
        /// </summary>
        private async Task<bool> HandshakeAsync()
        {
            // 发送 Diag 版本请求
            byte[] versionCmd = BuildDiagFrame(DIAG_CMD_VERSION, null);
            await WriteAsync(versionCmd);

            // 等待响应
            byte[] response = await ReadFrameAsync(2000);
            if (response != null && response.Length > 0)
            {
                Log("[Diag] 版本响应: {0} bytes", response.Length);
                return true;
            }

            return false;
        }

        #endregion

        #region IMEI 操作

        /// <summary>
        /// 读取 IMEI
        /// </summary>
        public async Task<string> ReadImeiAsync(int slot = 1)
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            ushort nvId = slot == 1 ? NV_IMEI1 : NV_IMEI2;
            byte[] data = await ReadNvAsync(nvId, 8);

            if (data == null || data.Length < 8)
                return null;

            return ParseImei(data);
        }

        /// <summary>
        /// 写入 IMEI
        /// </summary>
        public async Task<bool> WriteImeiAsync(string imei, int slot = 1)
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            if (string.IsNullOrEmpty(imei) || imei.Length != 15)
                throw new ArgumentException("IMEI 长度必须为 15 位");

            ushort nvId = slot == 1 ? NV_IMEI1 : NV_IMEI2;
            byte[] data = EncodeImei(imei);

            return await WriteNvAsync(nvId, data);
        }

        /// <summary>
        /// 解析 IMEI (BCD 格式)
        /// </summary>
        private string ParseImei(byte[] data)
        {
            if (data == null || data.Length < 8)
                return null;

            var sb = new StringBuilder();

            // 第一个字节的高 4 位是第一位
            sb.Append((data[0] >> 4) & 0x0F);

            // 后续字节
            for (int i = 1; i < 8; i++)
            {
                sb.Append(data[i] & 0x0F);
                sb.Append((data[i] >> 4) & 0x0F);
            }

            string imei = sb.ToString().TrimEnd('F');
            return imei.Length == 15 ? imei : null;
        }

        /// <summary>
        /// 编码 IMEI (BCD 格式)
        /// </summary>
        private byte[] EncodeImei(string imei)
        {
            byte[] data = new byte[8];

            // 第一个字节
            data[0] = (byte)(((imei[0] - '0') << 4) | 0x0A);

            // 后续字节
            for (int i = 1; i < 8; i++)
            {
                int idx = (i - 1) * 2 + 1;
                byte low = (byte)(imei[idx] - '0');
                byte high = (byte)(idx + 1 < imei.Length ? (imei[idx + 1] - '0') : 0x0F);
                data[i] = (byte)((high << 4) | low);
            }

            return data;
        }

        #endregion

        #region NV 操作

        /// <summary>
        /// 读取 NV 数据
        /// </summary>
        public async Task<byte[]> ReadNvAsync(ushort nvId, int length)
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            // 构建 NV 读取命令
            byte[] payload = new byte[4];
            payload[0] = (byte)(nvId & 0xFF);
            payload[1] = (byte)((nvId >> 8) & 0xFF);
            payload[2] = (byte)(length & 0xFF);
            payload[3] = (byte)((length >> 8) & 0xFF);

            byte[] frame = BuildDiagFrame(DIAG_CMD_NV_READ, payload);
            await WriteAsync(frame);

            byte[] response = await ReadFrameAsync(3000);
            if (response == null || response.Length < 5)
                return null;

            // 检查响应状态
            if (response[0] != DIAG_CMD_NV_READ)
            {
                Log("[Diag] NV 读取失败: 错误响应 0x{0:X2}", response[0]);
                return null;
            }

            // 提取数据 (跳过命令和 NV ID)
            byte[] data = new byte[response.Length - 3];
            Array.Copy(response, 3, data, 0, data.Length);
            return data;
        }

        /// <summary>
        /// 写入 NV 数据
        /// </summary>
        public async Task<bool> WriteNvAsync(ushort nvId, byte[] data)
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            // 构建 NV 写入命令
            byte[] payload = new byte[2 + data.Length];
            payload[0] = (byte)(nvId & 0xFF);
            payload[1] = (byte)((nvId >> 8) & 0xFF);
            Array.Copy(data, 0, payload, 2, data.Length);

            byte[] frame = BuildDiagFrame(DIAG_CMD_NV_WRITE, payload);
            await WriteAsync(frame);

            byte[] response = await ReadFrameAsync(3000);
            if (response == null || response.Length < 1)
                return false;

            return response[0] == DIAG_CMD_NV_WRITE;
        }

        #endregion

        #region AT 命令

        /// <summary>
        /// 发送 AT 命令
        /// </summary>
        public async Task<string> SendAtCommandAsync(string command, int timeout = 5000)
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            // 确保命令以 \r 结尾
            if (!command.EndsWith("\r"))
                command += "\r";

            byte[] payload = Encoding.ASCII.GetBytes(command);
            byte[] frame = BuildDiagFrame(DIAG_CMD_AT_COMMAND, payload);

            await WriteAsync(frame);

            byte[] response = await ReadFrameAsync(timeout);
            if (response == null || response.Length < 2)
                return null;

            // 跳过命令字节
            return Encoding.ASCII.GetString(response, 1, response.Length - 1).Trim();
        }

        #endregion

        #region 设备控制

        /// <summary>
        /// 重启设备
        /// </summary>
        public async Task<bool> RestartAsync()
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            byte[] frame = BuildDiagFrame(DIAG_CMD_RESTART, null);
            await WriteAsync(frame);

            // 重启命令通常不会有响应
            await Task.Delay(500);
            return true;
        }

        /// <summary>
        /// 关机
        /// </summary>
        public async Task<bool> PowerOffAsync()
        {
            if (!_isConnected)
                throw new InvalidOperationException("未连接");

            byte[] frame = BuildDiagFrame(DIAG_CMD_POWER_OFF, null);
            await WriteAsync(frame);

            await Task.Delay(500);
            return true;
        }

        /// <summary>
        /// 切换到下载模式
        /// </summary>
        public async Task<bool> SwitchToDownloadModeAsync()
        {
            // 发送 Diag 通道切换命令
            // 格式: 7E 00 00 00 00 08 00 FE 81 7E
            byte[] switchCmd = new byte[] { 0x7E, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0xFE, 0x81, 0x7E };
            
            await WriteAsync(switchCmd);
            Log("[Diag] 切换到下载模式命令已发送");

            // 设备会重新枚举
            _isConnected = false;
            return true;
        }

        #endregion

        #region 底层通信

        /// <summary>
        /// 构建 Diag 帧
        /// </summary>
        private byte[] BuildDiagFrame(byte cmd, byte[] payload)
        {
            var data = new List<byte>();
            data.Add(cmd);
            if (payload != null)
                data.AddRange(payload);

            // 计算 CRC
            ushort crc = CalculateCrc16(data.ToArray());

            // 构建帧
            var frame = new List<byte>();
            frame.Add(HDLC_FLAG);

            // 转义写入数据
            foreach (byte b in data)
            {
                WriteEscaped(frame, b);
            }

            // 转义写入 CRC
            WriteEscaped(frame, (byte)(crc & 0xFF));
            WriteEscaped(frame, (byte)((crc >> 8) & 0xFF));

            frame.Add(HDLC_FLAG);
            return frame.ToArray();
        }

        /// <summary>
        /// 转义写入
        /// </summary>
        private void WriteEscaped(List<byte> frame, byte b)
        {
            if (b == HDLC_FLAG || b == HDLC_ESCAPE)
            {
                frame.Add(HDLC_ESCAPE);
                frame.Add((byte)(b ^ HDLC_ESCAPE_XOR));
            }
            else
            {
                frame.Add(b);
            }
        }

        /// <summary>
        /// 解析 Diag 帧
        /// </summary>
        private byte[] ParseDiagFrame(byte[] frame)
        {
            if (frame == null || frame.Length < 4)
                return null;

            if (frame[0] != HDLC_FLAG || frame[frame.Length - 1] != HDLC_FLAG)
                return null;

            // 反转义
            var data = new List<byte>();
            bool escaped = false;

            for (int i = 1; i < frame.Length - 1; i++)
            {
                if (escaped)
                {
                    data.Add((byte)(frame[i] ^ HDLC_ESCAPE_XOR));
                    escaped = false;
                }
                else if (frame[i] == HDLC_ESCAPE)
                {
                    escaped = true;
                }
                else
                {
                    data.Add(frame[i]);
                }
            }

            if (data.Count < 3)
                return null;

            // 去除 CRC (最后 2 字节)
            byte[] result = new byte[data.Count - 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = data[i];

            return result;
        }

        /// <summary>
        /// 计算 CRC-16
        /// </summary>
        private ushort CalculateCrc16(byte[] data)
        {
            ushort crc = 0xFFFF;

            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ 0x8408);
                    else
                        crc >>= 1;
                }
            }

            return (ushort)(crc ^ 0xFFFF);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        private async Task WriteAsync(byte[] data)
        {
            if (_port == null || !_port.IsOpen)
                throw new InvalidOperationException("端口未打开");

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    _port.Write(data, 0, data.Length);
                }
            });
        }

        /// <summary>
        /// 读取帧
        /// </summary>
        private async Task<byte[]> ReadFrameAsync(int timeout)
        {
            var buffer = new List<byte>();
            var cts = new CancellationTokenSource(timeout);

            try
            {
                bool started = false;

                while (!cts.Token.IsCancellationRequested)
                {
                    if (_port.BytesToRead > 0)
                    {
                        byte b = (byte)_port.ReadByte();

                        if (b == HDLC_FLAG)
                        {
                            if (started && buffer.Count > 0)
                            {
                                // 帧结束
                                buffer.Add(b);
                                return ParseDiagFrame(buffer.ToArray());
                            }
                            else
                            {
                                // 帧开始
                                started = true;
                                buffer.Clear();
                                buffer.Add(b);
                            }
                        }
                        else if (started)
                        {
                            buffer.Add(b);
                        }
                    }
                    else
                    {
                        await Task.Delay(10, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 超时
            }

            return null;
        }

        #endregion

        #region 日志

        private void Log(string format, params object[] args)
        {
            OnLog?.Invoke(string.Format(format, args));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Disconnect();
            _port?.Dispose();
        }

        #endregion
    }
}
