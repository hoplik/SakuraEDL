// ============================================================================
// SakuraEDL - MediaTek BROM 命令定义
// MediaTek Boot ROM (BROM) Protocol Commands
// ============================================================================
// 参考: mtkclient 项目 mtk_preloader.py
// ============================================================================

using System;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// BROM 握手序列
    /// </summary>
    public static class BromHandshake
    {
        /// <summary>握手发送字节序列 (0xA0, 0x0A, 0x50, 0x05)</summary>
        public static readonly byte[] SendSequence = { 0xA0, 0x0A, 0x50, 0x05 };
        
        /// <summary>握手期望回应序列 (0x5F, 0xF5, 0xAF, 0xFA)</summary>
        public static readonly byte[] ExpectedResponse = { 0x5F, 0xF5, 0xAF, 0xFA };
        
        /// <summary>单字节握手发送</summary>
        public const byte HANDSHAKE_SEND = 0xA0;
        
        /// <summary>单字节握手回应</summary>
        public const byte HANDSHAKE_RESPONSE = 0x5F;
    }

    /// <summary>
    /// BROM 命令字节定义 (仅保留实际使用的命令)
    /// 参考: mtkclient/Library/mtk_preloader.py Cmd 枚举
    /// </summary>
    public static class BromCommands
    {
        // ========================================
        // 内存读写命令
        // ========================================
        
        /// <summary>读取 32 位 (0xD1)</summary>
        public const byte CMD_READ32 = 0xD1;
        
        /// <summary>写入 16 位 (0xD2)</summary>
        public const byte CMD_WRITE16 = 0xD2;
        
        /// <summary>写入 32 位 (0xD4)</summary>
        public const byte CMD_WRITE32 = 0xD4;

        // ========================================
        // DA 操作命令
        // ========================================
        
        /// <summary>跳转到 DA (0xD5)</summary>
        public const byte CMD_JUMP_DA = 0xD5;
        
        /// <summary>发送 DA (0xD7)</summary>
        public const byte CMD_SEND_DA = 0xD7;
        
        /// <summary>获取目标配置 (0xD8)</summary>
        public const byte CMD_GET_TARGET_CONFIG = 0xD8;
        
        /// <summary>发送环境准备 (0xD9)</summary>
        public const byte CMD_SEND_ENV_PREPARE = 0xD9;

        // ========================================
        // 安全/认证命令
        // ========================================
        
        /// <summary>发送证书 / Exploit Payload (0xE0)</summary>
        public const byte CMD_SEND_CERT = 0xE0;
        
        /// <summary>获取 ME ID (0xE1)</summary>
        public const byte CMD_GET_ME_ID = 0xE1;
        
        /// <summary>SLA 认证 (0xE3)</summary>
        public const byte CMD_SLA = 0xE3;
        
        /// <summary>获取 SOC ID (0xE7)</summary>
        public const byte CMD_GET_SOC_ID = 0xE7;

        // ========================================
        // 版本信息命令
        // ========================================
        
        /// <summary>获取硬件软件版本 (0xFC)</summary>
        public const byte CMD_GET_HW_SW_VER = 0xFC;
        
        /// <summary>获取硬件代码 (0xFD)</summary>
        public const byte CMD_GET_HW_CODE = 0xFD;
        
        /// <summary>获取 BL 版本 (0xFE)</summary>
        public const byte CMD_GET_BL_VER = 0xFE;
        
        /// <summary>获取版本 (0xFF)</summary>
        public const byte CMD_GET_VERSION = 0xFF;
    }

    /// <summary>
    /// BROM 响应码
    /// </summary>
    public static class BromResponse
    {
        /// <summary>确认 (0x5A)</summary>
        public const byte ACK = 0x5A;
        
        /// <summary>否定确认 (0xA5)</summary>
        public const byte NACK = 0xA5;
    }

    /// <summary>
    /// BROM 状态码
    /// </summary>
    public enum BromStatus : ushort
    {
        /// <summary>成功</summary>
        Success = 0x0000,
        
        /// <summary>Auth/SLA 需要认证 (0x0010) - Preloader 模式下常见</summary>
        AuthRequired = 0x0010,
        
        /// <summary>Preloader 模式需要 Auth (0x0011)</summary>
        PreloaderAuth = 0x0011,
        
        /// <summary>SLA 需要认证</summary>
        SlaRequired = 0x1D0D,
        
        /// <summary>SLA 不需要</summary>
        SlaNotNeeded = 0x1D0C,
        
        /// <summary>DAA 安全错误</summary>
        DaaSecurityError = 0x7017,
        
        /// <summary>DAA 签名验证错误</summary>
        DaaSignatureError = 0x7015,
        
        /// <summary>不支持的命令</summary>
        UnsupportedCmd = 0x0001,
        
        /// <summary>通用错误</summary>
        Error = 0x0002,
    }

    /// <summary>
    /// 目标配置标志位
    /// </summary>
    [Flags]
    public enum TargetConfigFlags : uint
    {
        /// <summary>无标志</summary>
        None = 0x00,
        
        /// <summary>Secure Boot 已启用</summary>
        SbcEnabled = 0x01,
        
        /// <summary>SLA 已启用</summary>
        SlaEnabled = 0x02,
        
        /// <summary>DAA 已启用</summary>
        DaaEnabled = 0x04,
    }

    /// <summary>
    /// DA 模式
    /// </summary>
    public enum DaMode : int
    {
        /// <summary>传统 DA 模式</summary>
        Legacy = 3,
        
        /// <summary>XFlash DA 模式</summary>
        XFlash = 5,
        
        /// <summary>XML DA 模式 (V6)</summary>
        Xml = 6,
    }

    /// <summary>
    /// MTK 设备状态
    /// </summary>
    public enum MtkDeviceState
    {
        /// <summary>断开连接</summary>
        Disconnected,
        
        /// <summary>握手中</summary>
        Handshaking,
        
        /// <summary>已连接 (BROM 模式)</summary>
        Brom,
        
        /// <summary>Preloader 模式</summary>
        Preloader,
        
        /// <summary>DA1 已加载</summary>
        Da1Loaded,
        
        /// <summary>DA2 已加载</summary>
        Da2Loaded,
        
        /// <summary>错误状态</summary>
        Error,
    }

    /// <summary>
    /// MTK USB VID/PID
    /// </summary>
    public static class MtkUsbIds
    {
        public const int VID_MTK = 0x0E8D;
        public const int PID_PRELOADER = 0x0003;
        public const int PID_BOOTROM = 0x2000;
        
        /// <summary>检查是否为 MTK 下载模式设备</summary>
        public static bool IsDownloadMode(int vid, int pid)
        {
            return vid == VID_MTK && (pid == PID_PRELOADER || pid == PID_BOOTROM);
        }
    }

    /// <summary>
    /// BROM 错误码解释
    /// </summary>
    public static class BromErrorHelper
    {
        /// <summary>获取错误消息</summary>
        public static string GetErrorMessage(ushort status)
        {
            return status switch
            {
                0x0000 => "成功",
                0x0001 => "无效命令",
                0x0002 => "校验错误",
                0x1D0C => "不需要 SLA 认证",
                0x1D0D => "需要 SLA 认证",
                0x7015 => "DAA 签名验证失败",
                0x7017 => "DAA 安全错误 (设备启用了 DAA 保护)",
                _ => $"未知错误 (0x{status:X4})"
            };
        }

        /// <summary>检查状态是否为成功</summary>
        public static bool IsSuccess(ushort status)
        {
            // 明确的成功状态
            if (status == 0x0000 || status == 0x1D0C)
                return true;
            
            // DAA 状态 - 返回成功让上层处理重连
            if (status == 0x7017 || status == 0x7015)
                return true;
            
            // 小于 0x100 的其他值视为成功
            if (status < 0x100 && status > 0x000E)
                return true;
            
            return false;
        }

        /// <summary>检查是否需要 SLA 认证</summary>
        public static bool NeedsSla(ushort status)
        {
            return status == (ushort)BromStatus.SlaRequired;
        }
    }
}
