// ============================================================================
// SakuraEDL - MediaTek BROM 命令定义
// MediaTek Boot ROM (BROM) Protocol Commands
// ============================================================================
// 参考: mtkclient 项目 mtk_preloader.py
// 来源: IDA Pro 逆向分析 SP_Flash_Tool V6 flash.dll
// ============================================================================

using System;

namespace SakuraEDL.MediaTek.Protocol
{
    /// <summary>
    /// BROM 握手序列
    /// 来源: IDA Pro 逆向分析 SP_Flash_Tool V6 flash.dll (byte_10103B98)
    /// </summary>
    public static class BromHandshake
    {
        /// <summary>握手发送字节序列 (0xA0, 0x0A, 0x50, 0x05)</summary>
        public static readonly byte[] SendSequence = { 0xA0, 0x0A, 0x50, 0x05 };
        
        /// <summary>握手期望回应序列 (0x5F, 0xF5, 0xAF, 0xFA) - 计算方式: ~SendSequence[i]</summary>
        public static readonly byte[] ExpectedResponse = { 0x5F, 0xF5, 0xAF, 0xFA };
        
        /// <summary>单字节握手发送</summary>
        public const byte HANDSHAKE_SEND = 0xA0;
        
        /// <summary>单字节握手回应</summary>
        public const byte HANDSHAKE_RESPONSE = 0x5F;
        
        /// <summary>Preloader 存在标志 (0x52 = 'R') - IDA: "preloader exist. connect."</summary>
        public const byte PRELOADER_EXIST = 0x52;
        
        /// <summary>验证握手回应是否正确</summary>
        public static bool ValidateResponse(byte send, byte response)
        {
            return (byte)(~response) == send;
        }
    }

    /// <summary>
    /// BROM 命令字节定义
    /// 参考: mtkclient/Library/mtk_preloader.py Cmd 枚举
    /// 来源: IDA Pro 逆向分析 SP_Flash_Tool V6 flash.dll
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
        
        /// <summary>扩展跳转到 DA (0xDE) - IDA: boot rom jump to Ex</summary>
        public const byte CMD_JUMP_DA_EX = 0xDE;
        
        /// <summary>BROM 日志输出 (0xDF) - IDA: BROM_CMD_MSG_DUMP_EX</summary>
        public const byte CMD_MSG_DUMP_EX = 0xDF;

        // ========================================
        // 安全/认证命令
        // ========================================
        
        /// <summary>发送证书 / Exploit Payload (0xE0) - IDA: BROM_SCMD_SEND_CERT</summary>
        public const byte CMD_SEND_CERT = 0xE0;
        
        /// <summary>获取 ME ID (0xE1) - IDA: BROM_SCMD_GET_ME_ID, 返回 16 字节</summary>
        public const byte CMD_GET_ME_ID = 0xE1;
        
        /// <summary>发送 Auth 文件 (0xE2) - IDA: BROM_SCMD_SEND_AUTH</summary>
        public const byte CMD_SEND_AUTH = 0xE2;
        
        /// <summary>SLA 认证 (0xE3)</summary>
        public const byte CMD_SLA = 0xE3;
        
        /// <summary>获取 SOC ID (0xE7) - IDA: BROM_SCMD_GET_SOC_ID, 返回 32 字节</summary>
        public const byte CMD_GET_SOC_ID = 0xE7;

        // ========================================
        // Preloader 数据命令
        // ========================================
        
        /// <summary>发送 Preloader 数据 (0xF1) - IDA: send Preloader data to boot rom</summary>
        public const byte CMD_SEND_PRELOADER = 0xF1;

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
    /// Bootloader (Preloader) 命令字节定义
    /// 来源: IDA Pro 逆向分析 SP_Flash_Tool V6 flash.dll
    /// </summary>
    public static class BldrCommands
    {
        /// <summary>发送分区数据 (0x70) - IDA: BLDR_CMD_SEND_PARTITION_DATA</summary>
        public const byte CMD_SEND_PARTITION_DATA = 0x70;
        
        /// <summary>跳转到分区 (0x71) - IDA: BLDR_CMD_JUMP_TO_PARTITION</summary>
        public const byte CMD_JUMP_TO_PARTITION = 0x71;
        
        /// <summary>设置目标 Flash 模式 (0x72) - IDA: BLDR_CMD_SET_TARGET_FLASH_MODE</summary>
        public const byte CMD_SET_TARGET_FLASH_MODE = 0x72;
        
        /// <summary>Stay Still / 保持状态 (0x80) - IDA: BLDR_CMD_STAY_STILL</summary>
        public const byte CMD_STAY_STILL = 0x80;
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
    /// DA (Download Agent) 协议常量
    /// 来源: IDA Pro 逆向分析 SP_Flash_Tool V6 flash.dll
    /// </summary>
    public static class DaProtocol
    {
        // ========================================
        // 同步/握手常量
        // ========================================
        
        /// <summary>DA 同步字符 (0xC0) - DA 启动后发送此字符通知主机就绪</summary>
        public const byte SYNC_CHAR = 0xC0;
        
        /// <summary>DA 结束命令 (0xD9) - 用于结束 DA 会话</summary>
        public const byte DA_FINISH_CMD = 0xD9;
        
        // ========================================
        // 超时参数 (毫秒)
        // ========================================
        
        /// <summary>等待 SYNC_CHAR 超时: 5000ms</summary>
        public const int SYNC_CHAR_TIMEOUT_MS = 5000;
        
        /// <summary>普通读取超时: 10000ms</summary>
        public const int READ_TIMEOUT_MS = 10000;
        
        /// <summary>H/W 检测报告超时: 10000ms (0x2710)</summary>
        public const int HW_DETECT_TIMEOUT_MS = 10000;
        
        /// <summary>DA H/W 检测超时: 30000ms</summary>
        public const int DA_HW_DETECT_TIMEOUT_MS = 30000;
        
        /// <summary>Preloader 连接超时: 20000ms</summary>
        public const int PRELOADER_CONNECT_TIMEOUT_MS = 20000;
        
        // ========================================
        // 延时参数 (毫秒)
        // ========================================
        
        /// <summary>DA 配置发送后延时: 1000ms</summary>
        public const int DA_CONFIG_DELAY_MS = 1000;
        
        /// <summary>CMD_Finish 命令后延时: 10ms</summary>
        public const int CMD_FINISH_DELAY_MS = 10;
        
        // ========================================
        // 协议常量 (IDA 逆向分析)
        // ========================================
        
        /// <summary>状态码错误阈值 - status >= 0x1000 视为错误</summary>
        public const ushort STATUS_ERROR_THRESHOLD = 0x1000;
        
        /// <summary>单次传输最大块大小: 64KB (0x10000)</summary>
        public const int MAX_TRANSFER_BLOCK = 0x10000;
        
        /// <summary>Preloader 首包大小: 512 字节 (0x200)</summary>
        public const int PRELOADER_FIRST_PACKET = 0x200;
        
        // ========================================
        // DA 版本相关 HW Code
        // ========================================
        
        /// <summary>MT6571 HW Code</summary>
        public const ushort HW_CODE_MT6571 = 0x6571;
        
        /// <summary>MT6573 HW Code</summary>
        public const ushort HW_CODE_MT6573 = 0x6573;
        
        /// <summary>MT6575 HW Code</summary>
        public const ushort HW_CODE_MT6575 = 0x6575;
        
        /// <summary>MT6577 HW Code</summary>
        public const ushort HW_CODE_MT6577 = 0x6577;
        
        /// <summary>MT6592 HW Code - 支持发送 phone info</summary>
        public const ushort HW_CODE_MT6592 = 0x6592;
        
        /// <summary>
        /// 检查是否需要启用 phone info 发送 (MT6592+)
        /// </summary>
        public static bool NeedsPhoneInfoEnabled(ushort hwCode)
        {
            return hwCode >= HW_CODE_MT6592;
        }
        
        /// <summary>
        /// 检查状态码是否为错误 (>= 0x1000)
        /// </summary>
        public static bool IsStatusError(ushort status)
        {
            return status >= STATUS_ERROR_THRESHOLD;
        }
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
    /// V6 XML DA 命令字符串 (IDA 逆向分析 flash.dll)
    /// 用于 XML 格式的 DA 通信
    /// XML 格式: &lt;da&gt;&lt;version&gt;1.0&lt;/version&gt;&lt;command&gt;CMD:XXX&lt;/command&gt;&lt;arg&gt;...&lt;/arg&gt;&lt;/da&gt;
    /// </summary>
    public static class XmlDaCommands
    {
        // ========================================
        // 设备信息命令 (IDA 验证)
        // ========================================
        
        /// <summary>获取硬件信息 - BROM 模式使用</summary>
        public const string CMD_GET_HW_INFO = "CMD:GET-HW-INFO";
        
        /// <summary>主机支持的命令列表</summary>
        public const string CMD_HOST_SUPPORTED_COMMANDS = "CMD:HOST-SUPPORTED-COMMANDS";
        
        /// <summary>通知初始化硬件</summary>
        public const string CMD_NOTIFY_INIT_HW = "CMD:NOTIFY-INIT-HW";
        
        /// <summary>设置主机信息</summary>
        public const string CMD_SET_HOST_INFO = "CMD:SET-HOST-INFO";
        
        // ========================================
        // 启动/跳转命令 (IDA 验证)
        // ========================================
        
        /// <summary>启动到指定地址</summary>
        public const string CMD_BOOT_TO = "CMD:BOOT-TO";
        
        /// <summary>运行程序</summary>
        public const string CMD_RUN_PROGRAM = "CMD:RUN-PROGRAM";
        
        /// <summary>运行加载器</summary>
        public const string CMD_RUN_LOADER = "CMD:RUN-LOADER";
        
        /// <summary>重启后让 Preloader 保持</summary>
        public const string CMD_LET_LOADER_STAY_AFTER_REBOOT = "LET-LOADER-STAY-AFTER-REBOOT";
        
        /// <summary>操作命令</summary>
        public const string CMD_ACTION = "CMD:ACTION";
        
        // ========================================
        // USB 速度命令 (IDA 验证)
        // ========================================
        
        /// <summary>检查是否支持高速 USB</summary>
        public const string CMD_CAN_HIGHER_USB_SPEED = "CMD:CAN-HIGHER-USB-SPEED";
        
        /// <summary>切换到高速 USB</summary>
        public const string CMD_SWITCH_HIGHER_USB_SPEED = "CMD:SWITCH-HIGHER-USB-SPEED";
        
        // ========================================
        // 文件传输命令 (IDA 验证)
        // ========================================
        
        /// <summary>下载文件到设备 (写入)</summary>
        public const string CMD_DOWNLOAD_FILE = "CMD:DOWNLOAD-FILE";
        
        /// <summary>从设备上传文件 (读取)</summary>
        public const string CMD_UPLOAD_FILE = "CMD:UPLOAD-FILE";
        
        /// <summary>进度报告</summary>
        public const string CMD_PROGRESS_REPORT = "CMD:PROGRESS-REPORT";
        
        /// <summary>文件系统操作</summary>
        public const string CMD_FILE_SYS_OPERATION = "CMD:FILE-SYS-OPERATION";
        
        /// <summary>开始传输</summary>
        public const string CMD_START = "CMD:START";
        
        /// <summary>结束传输</summary>
        public const string CMD_END = "CMD:END";
        
        // ========================================
        // 认证命令 (IDA 验证)
        // ========================================
        
        /// <summary>发送认证数据 (证书/签名)</summary>
        public const string CMD_SEND_AUTH = "CMD:SEND-AUTH";
        
        /// <summary>获取随机数 (用于 SLA 认证) - 默认禁用</summary>
        public const string CMD_GET_RANDOM = "CMD:GET-RANDOM";
        
        /// <summary>获取 SLA 状态</summary>
        public const string CMD_GET_SLA = "CMD:GET-SLA";
        
        /// <summary>SLA 质询请求</summary>
        public const string CMD_SLA_CHALLENGE = "CMD:SLA-CHALLENGE";
        
        /// <summary>SLA 认证响应</summary>
        public const string CMD_SLA_AUTH = "CMD:SLA-AUTH";
        
        // ========================================
        // 寄存器操作命令 (IDA 验证)
        // ========================================
        
        /// <summary>读取寄存器/内存</summary>
        public const string CMD_READ_REGISTER = "CMD:READ-REGISTER";
        
        /// <summary>写入寄存器/内存</summary>
        public const string CMD_WRITE_REGISTER = "CMD:WRITE-REGISTER";
        
        // ========================================
        // 分区/Flash 命令 (用于分区操作)
        // ========================================
        
        /// <summary>擦除分区</summary>
        public const string CMD_ERASE_PARTITION = "CMD:ERASE-PARTITION";
        
        /// <summary>写入分区</summary>
        public const string CMD_WRITE_PARTITION = "CMD:WRITE-PARTITION";
        
        /// <summary>批量写入分区</summary>
        public const string CMD_WRITE_PARTITIONS = "CMD:WRITE-PARTITIONS";
        
        /// <summary>读取分区</summary>
        public const string CMD_READ_PARTITION = "CMD:READ-PARTITION";
        
        /// <summary>获取分区表</summary>
        public const string CMD_GET_PT = "CMD:GET-PT";
        
        /// <summary>读取分区表</summary>
        public const string CMD_READ_PARTITION_TABLE = "CMD:READ-PARTITION-TABLE";
        
        /// <summary>格式化分区</summary>
        public const string CMD_FORMAT_PARTITION = "CMD:FORMAT-PARTITION";
        
        /// <summary>擦除 Flash</summary>
        public const string CMD_ERASE_FLASH = "CMD:ERASE-FLASH";
        
        /// <summary>读取 Flash</summary>
        public const string CMD_READ_FLASH = "CMD:READ-FLASH";
        
        /// <summary>写入 Flash</summary>
        public const string CMD_WRITE_FLASH = "CMD:WRITE-FLASH";
        
        /// <summary>Flash 全量刷写</summary>
        public const string CMD_FLASH_ALL = "CMD:FLASH-ALL";
        
        /// <summary>Flash 更新</summary>
        public const string CMD_FLASH_UPDATE = "CMD:FLASH-UPDATE";
        
        /// <summary>重启设备</summary>
        public const string CMD_REBOOT = "CMD:REBOOT";
        
        /// <summary>关机</summary>
        public const string CMD_SHUTDOWN = "CMD:SHUTDOWN";
        
        // ========================================
        // 安全命令 (iReverse 参考)
        // ========================================
        
        /// <summary>设置 Flash 策略</summary>
        public const string CMD_SECURITY_SET_FLASH_POLICY = "CMD:SECURITY-SET-FLASH-POLICY";
        
        /// <summary>获取设备固件信息</summary>
        public const string CMD_SECURITY_GET_DEV_FW_INFO = "CMD:SECURITY-GET-DEV-FW-INFO";
        
        /// <summary>设置 Allinone 签名</summary>
        public const string CMD_SECURITY_SET_ALLINONE_SIGNATURE = "CMD:SECURITY-SET-ALLINONE-SIGNATURE";
        
        // ========================================
        // 设备控制命令 (iReverse 参考)
        // ========================================
        
        /// <summary>设置启动模式 (FASTBOOT/META/ANDROID-TEST-MODE)</summary>
        public const string CMD_SET_BOOT_MODE = "CMD:SET-BOOT-MODE";
        
        /// <summary>设置运行时参数</summary>
        public const string CMD_SET_RUNTIME_PARAMETER = "CMD:SET-RUNTIME-PARAMETER";
        
        /// <summary>eMMC 控制 (GET-RPMB-STATUS/LIFE-CYCLE-STATUS 等)</summary>
        public const string CMD_EMMC_CONTROL = "CMD:EMMC-CONTROL";
        
        // ========================================
        // eFuse 命令 (iReverse 参考)
        // ========================================
        
        /// <summary>读取 eFuse</summary>
        public const string CMD_READ_EFUSE = "CMD:READ-EFUSE";
        
        /// <summary>写入 eFuse</summary>
        public const string CMD_WRITE_EFUSE = "CMD:WRITE-EFUSE";
        
        // ========================================
        // 诊断/测试命令 (iReverse 参考)
        // ========================================
        
        /// <summary>RAM 测试 (FLIP/CALIBRATION)</summary>
        public const string CMD_RAM_TEST = "CMD:RAM-TEST";
        
        /// <summary>DRAM 修复</summary>
        public const string CMD_DRAM_REPAIR = "CMD:DRAM-REPAIR";
        
        // ========================================
        // 版本/信息命令 (iReverse 参考)
        // ========================================
        
        /// <summary>获取版本</summary>
        public const string CMD_GET_VERSION = "CMD:GET-VERSION";
        
        /// <summary>获取 DA 信息</summary>
        public const string CMD_GET_DA_INFO = "CMD:GET-DA-INFO";
        
        /// <summary>获取系统属性 (DA.SLA 等)</summary>
        public const string CMD_GET_SYS_PROPERTY = "CMD:GET-SYS-PROPERTY";
        
        /// <summary>获取下载镜像反馈</summary>
        public const string CMD_GET_DOWNLOADED_IMAGE_FEEDBACK = "CMD:GET-DOWNLOADED-IMAGE-FEEDBACK";
        
        // ========================================
        // 证书/RSC 命令 (iReverse 参考)
        // ========================================
        
        /// <summary>设置 RSC (Regional Sales Code)</summary>
        public const string CMD_SET_RSC = "CMD:SET-RSC";
        
        /// <summary>写入私有证书</summary>
        public const string CMD_WRITE_PRIVATE_CERT = "CMD:WRITE-PRIVATE-CERT";
        
        /// <summary>连接命令</summary>
        public const string CMD_CONNECT = "CMD:CONNECT";
    }
    
    /// <summary>
    /// Flash 模式 (IDA 逆向分析 flash.dll)
    /// </summary>
    public static class FlashMode
    {
        /// <summary>DA 模式</summary>
        public const string MODE_DA = "FLASH-MODE-DA";
        
        /// <summary>DA SRAM 模式</summary>
        public const string MODE_DA_SRAM = "FLASH-MODE-DA-SRAM";
        
        /// <summary>XFlash 模式</summary>
        public const string MODE_XFLASH = "FLASH-MODE-XFLASH";
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
    /// ═══════════════════════════════════════════════════════════════════════
    /// 根据 mtkclient usb_ids.py 修正:
    ///   0x0003 = BROM 模式 (Boot ROM, 最底层)
    ///   0x2000 = Preloader 模式
    ///   0x6000 = Preloader 模式 (备用)
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    public static class MtkUsbIds
    {
        /// <summary>MediaTek Vendor ID (0x0E8D)</summary>
        public const int VID_MTK = 0x0E8D;
        
        /// <summary>BROM 模式 PID (0x0003) - Boot ROM 最底层，Kamakiri2 漏洞使用此模式</summary>
        public const int PID_BROM = 0x0003;
        
        /// <summary>Preloader 模式 PID (0x2000)</summary>
        public const int PID_PRELOADER = 0x2000;
        
        /// <summary>Preloader 模式备用 PID (0x6000)</summary>
        public const int PID_PRELOADER_ALT = 0x6000;
        
        // ═══════════════════════════════════════════════════════════════════════
        // 旧名称保留 (向后兼容，标记为过时)
        // ═══════════════════════════════════════════════════════════════════════
        [Obsolete("使用 PID_BROM 代替")]
        public const int PID_BOOTROM = PID_BROM;
        
        /// <summary>检查是否为 BROM 模式</summary>
        public static bool IsBromMode(int vid, int pid)
        {
            return vid == VID_MTK && pid == PID_BROM;
        }
        
        /// <summary>检查是否为 Preloader 模式</summary>
        public static bool IsPreloaderMode(int vid, int pid)
        {
            return vid == VID_MTK && (pid == PID_PRELOADER || pid == PID_PRELOADER_ALT);
        }
        
        /// <summary>检查是否为 MTK 下载模式设备 (BROM 或 Preloader)</summary>
        public static bool IsDownloadMode(int vid, int pid)
        {
            return vid == VID_MTK && (pid == PID_BROM || pid == PID_PRELOADER || pid == PID_PRELOADER_ALT);
        }
        
        /// <summary>获取模式描述</summary>
        public static string GetModeDescription(int pid)
        {
            return pid switch
            {
                PID_BROM => "BROM (Boot ROM)",
                PID_PRELOADER => "Preloader",
                PID_PRELOADER_ALT => "Preloader (Alt)",
                _ => $"Unknown (0x{pid:X4})"
            };
        }
    }

    /// <summary>
    /// BROM 错误码解释
    /// 来源: IDA Pro 逆向分析 SP_Flash_Tool V6 flash.dll
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
                _ when status >= DaProtocol.STATUS_ERROR_THRESHOLD => $"协议错误 (0x{status:X4})",
                _ => $"未知错误 (0x{status:X4})"
            };
        }

        /// <summary>检查状态是否为成功 (IDA: status >= 0x1000 为错误)</summary>
        public static bool IsSuccess(ushort status)
        {
            // 明确的成功状态
            if (status == 0x0000 || status == 0x1D0C)
                return true;
            
            // DAA 状态 - 返回成功让上层处理重连
            if (status == 0x7017 || status == 0x7015)
                return true;
            
            // IDA 分析: status >= 0x1000 视为错误
            if (status >= DaProtocol.STATUS_ERROR_THRESHOLD)
                return false;
            
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
        
        /// <summary>检查状态码是否为协议错误 (>= 0x1000)</summary>
        public static bool IsProtocolError(ushort status)
        {
            return DaProtocol.IsStatusError(status);
        }
    }
}
