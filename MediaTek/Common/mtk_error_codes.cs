// ============================================================================
// SakuraEDL - MTK Error Codes | 联发科错误码
// ============================================================================
// [ZH] 错误码解析 - XFlash (V5) 和 XML (V6) 协议错误码解析
// [EN] Error Code Parser - XFlash (V5) and XML (V6) protocol error parsing
// [JA] エラーコード解析 - XFlash (V5)/XML (V6) プロトコルエラー解析
// [KO] 오류 코드 파서 - XFlash (V5) 및 XML (V6) 프로토콜 오류 분석
// [RU] Парсер кодов ошибок - Разбор ошибок протоколов XFlash/XML
// [ES] Analizador de códigos de error - Análisis de errores XFlash/XML
// ============================================================================
// Reference: Penumbra project https://shomy.is-a.dev/penumbra/
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================

using System.Collections.Generic;

namespace SakuraEDL.MediaTek.Common
{
    /// <summary>
    /// 错误严重性级别
    /// </summary>
    public enum ErrorSeverity
    {
        Success = 0x00,      // 0x00000000
        Info = 0x40,         // 0x40000000
        Warning = 0x80,      // 0x80000000
        Error = 0xC0         // 0xC0000000
    }

    /// <summary>
    /// 错误域（组件）
    /// </summary>
    public enum ErrorDomain
    {
        Common = 1,          // 通用错误
        Security = 2,        // 安全相关
        Library = 3,         // 库/函数错误
        Device = 4,          // 设备/硬件错误
        Host = 5,            // Host端错误
        Brom = 6,            // BROM错误
        Da = 7,              // DA错误
        Preloader = 8        // Preloader错误
    }

    /// <summary>
    /// MTK 错误码解析与格式化
    /// 
    /// 错误码结构 (32位):
    /// - 位31-30: 严重性 (Success=00, Info=01, Warning=10, Error=11)
    /// - 位29-16: 保留
    /// - 位23-16: 错误域 (1-8)
    /// - 位15-0:  错误代码
    /// 
    /// 示例: 0xC0070004
    ///   0xC0000000 (Error) | 0x00070000 (DA Domain) | 0x0004 (Code 4)
    ///   => Error | DA | DA_HASH_MISMATCH
    /// </summary>
    public static class MtkErrorCodes
    {
        #region 错误码掩码

        private const uint SEVERITY_MASK    = 0xC0000000;
        private const uint DOMAIN_MASK      = 0x00FF0000;
        private const uint CODE_MASK        = 0x0000FFFF;

        public const uint SEVERITY_SUCCESS  = 0x00000000;
        public const uint SEVERITY_INFO     = 0x40000000;
        public const uint SEVERITY_WARNING  = 0x80000000;
        public const uint SEVERITY_ERROR    = 0xC0000000;

        private const int DOMAIN_SHIFT = 16;

        #endregion

        #region 错误码定义

        /// <summary>
        /// 常见错误码及其描述
        /// 来源: mtkclient项目 Library/error.py (ErrorCodes_XFlash, 已验证真实数据)
        /// </summary>
        public static readonly Dictionary<uint, string> CommonErrors = new Dictionary<uint, string>
        {
            // 成功
            { 0x00000000, "OK - 成功" },
            
            // XFlash Common 错误 (0xC001xxxx)
            { 0xC0010001, "Error - 错误" },
            { 0xC0010002, "Abort - 中止" },
            { 0xC0010003, "Unsupported command - 不支持的命令" },
            { 0xC0010004, "Unsupported ctrl code - 不支持的控制码" },
            { 0xC0010005, "Protocol error - 协议错误" },
            { 0xC0010006, "Protocol buffer overflow - 协议缓冲区溢出" },
            { 0xC0010007, "Insufficient buffer - 缓冲区不足" },
            { 0xC0010008, "USB SCAN error - USB扫描错误" },
            { 0xC0010009, "Invalid hsession - 无效会话句柄" },
            { 0xC001000A, "Invalid session - 无效会话" },
            { 0xC001000B, "Invalid stage - 无效阶段" },
            { 0xC001000C, "Not implemented - 未实现" },
            { 0xC001000D, "File not found - 文件未找到" },
            { 0xC001000E, "Open file error - 打开文件错误" },
            { 0xC001000F, "Write file error - 写入文件错误" },
            { 0xC0010010, "Read file error - 读取文件错误" },
            { 0xC0010011, "Create File error / Unsupported Version - 创建文件错误/不支持的版本" },
            
            // Security 错误 (0xC002xxxx)
            { 0xC0020001, "Rom info not found - ROM信息未找到" },
            { 0xC0020002, "Cust name not found - 客户名称未找到" },
            { 0xC0020003, "Device not supported - 设备不支持" },
            { 0xC0020004, "DL forbidden - 下载禁止" },
            { 0xC0020005, "Img too large - 镜像过大" },
            { 0xC0020006, "PL verify fail - Preloader验证失败" },
            { 0xC0020007, "Image verify fail - 镜像验证失败" },
            { 0xC0020008, "Hash operation fail - 哈希操作失败" },
            { 0xC0020009, "Hash binding check fail - 哈希绑定检查失败" },
            { 0xC002000A, "Invalid buf - 无效缓冲区" },
            { 0xC002000B, "Binding hash not available - 绑定哈希不可用" },
            { 0xC002000C, "Write data not allowed - 写入数据不允许" },
            { 0xC002000D, "Format not allowed - 格式化不允许" },
            { 0xC002000E, "SV5 public key auth failed - SV5公钥认证失败" },
            { 0xC002000F, "SV5 hash verify failed - SV5哈希验证失败" },
            { 0xC0020010, "SV5 RSA OP failed - SV5 RSA操作失败" },
            { 0xC0020011, "SV5 RSA verify failed - SV5 RSA验证失败" },
            { 0xC0020012, "SV5 GFH not found - SV5 GFH未找到" },
            { 0xC0020013, "Cert1 invalid - 证书1无效" },
            { 0xC0020014, "Cert2 invalid - 证书2无效" },
            { 0xC0020015, "Imghdr invalid - 镜像头无效" },
            { 0xC0020016, "Sig size invalid - 签名大小无效" },
            { 0xC0020017, "RSA pss op fail - RSA-PSS操作失败" },
            { 0xC0020018, "Cert auth failed - 证书认证失败" },
            { 0xC002002D, "Anti rollback violation - 防回滚违规" },
            { 0xC002002E, "SECCFG not found - SECCFG未找到" },
            { 0xC002002F, "SECCFG magic incorrect - SECCFG魔术值错误" },
            { 0xC0020030, "SECCFG invalid - SECCFG无效" },
            { 0xC0020049, "Remote Security policy disabled - 远程安全策略已禁用" },
            { 0xC002004C, "DA Anti-Rollback error - DA防回滚错误" },
            { 0xC0020053, "DA version Anti-Rollback error - DA版本防回滚错误" },
            { 0xC002005C, "Lockstate seccfg fail - 锁定状态SECCFG失败" },
            { 0xC002005D, "Lockstate custom fail - 锁定状态自定义失败" },
            { 0xC002005E, "Lockstate inconsistent - 锁定状态不一致" },
            
            // Library 错误 (0xC003xxxx)
            { 0xC0030001, "Scatter file invalid - Scatter文件无效" },
            { 0xC0030002, "DA file invalid - DA文件无效" },
            { 0xC0030003, "DA selection error - DA选择错误" },
            { 0xC0030004, "Preloader invalid - Preloader无效" },
            { 0xC0030005, "EMI hdr invalid - EMI头无效" },
            { 0xC0030006, "Storage mismatch - 存储不匹配" },
            { 0xC0030007, "Invalid parameters - 无效参数" },
            { 0xC0030008, "Invalid GPT - GPT无效" },
            { 0xC0030009, "Invalid PMT - PMT无效" },
            { 0xC003000A, "Layout changed - 布局已改变" },
            { 0xC003000B, "Invalid format param - 无效格式化参数" },
            { 0xC003000C, "Unknown storage section type - 未知存储段类型" },
            { 0xC003000D, "Unknown scatter field - 未知Scatter字段" },
            { 0xC003000E, "Partition tbl doesn't exist - 分区表不存在" },
            { 0xC003000F, "Scatter hw chip id mismatch - Scatter硬件芯片ID不匹配" },
            { 0xC0030010, "SEC cert file not found - 安全证书文件未找到" },
            { 0xC0030011, "SEC auth file not found - 安全认证文件未找到" },
            { 0xC0030012, "SEC auth file needed - 需要安全认证文件" },
            
            // Device 错误 (0xC004xxxx)
            { 0xC0040001, "Unsupported operation - 不支持的操作" },
            { 0xC0040002, "Thread error - 线程错误" },
            { 0xC0040003, "Checksum error - 校验和错误" },
            { 0xC0040004, "Unknown sparse - 未知稀疏镜像" },
            { 0xC0040005, "Unknown sparse chunk type - 未知稀疏块类型" },
            { 0xC0040006, "Partition not found - 分区未找到" },
            { 0xC0040007, "Read parttbl failed - 读取分区表失败" },
            { 0xC0040008, "Exceeded max partition number - 超过最大分区数" },
            { 0xC0040009, "Unknown storage type - 未知存储类型" },
            { 0xC004000A, "Dram Test failed - DRAM测试失败" },
            { 0xC004000B, "Exceed available range - 超出可用范围" },
            { 0xC004000C, "Write sparse image failed - 写入稀疏镜像失败" },
            { 0xC0040030, "MMC error - MMC错误" },
            { 0xC0040040, "Nand error - Nand错误" },
            { 0xC0040041, "Nand in progress - Nand操作进行中" },
            { 0xC0040042, "Nand timeout - Nand超时" },
            { 0xC0040043, "Nand bad block - Nand坏块" },
            { 0xC0040044, "Nand erase failed - Nand擦除失败" },
            { 0xC0040045, "Nand page program failed - Nand页编程失败" },
            { 0xC0040050, "EMI setting version error - EMI设置版本错误" },
            { 0xC0040060, "UFS error - UFS错误" },
            { 0xC0040100, "DA OTP not supported - DA OTP不支持" },
            { 0xC0040102, "DA OTP lock failed - DA OTP锁定失败" },
            { 0xC0040200, "EFUSE unknown error - EFUSE未知错误" },
            { 0xC0040201, "EFUSE write timeout without verify - EFUSE写入超时（未验证）" },
            { 0xC0040202, "EFUSE blown - EFUSE已熔断" },
            { 0xC0040203, "EFUSE revert bit - EFUSE回退位" },
            { 0xC0040204, "EFUSE blown partly - EFUSE部分熔断" },
            { 0xC0040206, "EFUSE value is not zero - EFUSE值非零" },
            { 0xC0040209, "EFUSE blow error - EFUSE熔断错误" },
            
            // Host 错误 (0xC005xxxx)
            { 0xC0050001, "Device ctrl exception - 设备控制异常" },
            { 0xC0050002, "Shutdown Cmd exception - 关机命令异常" },
            { 0xC0050003, "Download exception - 下载异常" },
            { 0xC0050004, "Upload exception - 上传异常" },
            { 0xC0050005, "Ext Ram exception - 外部RAM异常" },
            { 0xC0050008, "Write data exception - 写入数据异常" },
            { 0xC0050009, "Format exception - 格式化异常" },
            
            // BROM 错误 (0xC006xxxx)
            { 0xC0060001, "Brom start cmd/connect not preloader failed - BROM启动命令失败" },
            { 0xC0060002, "Brom get bbchip hw ver failed - BROM获取芯片版本失败" },
            { 0xC0060003, "Brom cmd send da failed - BROM发送DA失败" },
            { 0xC0060004, "Brom cmd jump da failed - BROM跳转DA失败" },
            { 0xC0060005, "Brom cmd failed - BROM命令失败" },
            { 0xC0060006, "Brom stage callback failed - BROM阶段回调失败" },
            
            // DA 错误 (0xC007xxxx)
            { 0xC0070001, "DA Version mismatch - DA版本不匹配" },
            { 0xC0070002, "DA not found - DA未找到" },
            { 0xC0070003, "DA section not found - DA段未找到" },
            { 0xC0070004, "DA hash mismatch - DA哈希不匹配 (Carbonara预期)" },
            { 0xC0070005, "DA exceed max num - DA超过最大数量" },

            // Progress报告 (Info级别 0x4004xxxx)
            { 0x40040004, "PROGRESS_REPORT - 操作进行中" },
            { 0x40040005, "PROGRESS_DONE - 操作完成" },
            
            // 特殊错误码
            { 0x00005A5B, "DA_IN_BLACKLIST - DA在黑名单中" },
        };

        #endregion

        #region 解析与格式化

        /// <summary>
        /// 解析错误码
        /// </summary>
        public static (ErrorSeverity severity, ErrorDomain domain, ushort code) ParseErrorCode(uint errorCode)
        {
            var severityBits = (errorCode & SEVERITY_MASK) >> 30;
            var severity = severityBits switch
            {
                0 => ErrorSeverity.Success,
                1 => ErrorSeverity.Info,
                2 => ErrorSeverity.Warning,
                3 => ErrorSeverity.Error,
                _ => ErrorSeverity.Error
            };

            var domainCode = (errorCode & DOMAIN_MASK) >> DOMAIN_SHIFT;
            var domain = (ErrorDomain)(domainCode);

            var code = (ushort)(errorCode & CODE_MASK);

            return (severity, domain, code);
        }

        /// <summary>
        /// 格式化错误码为可读字符串
        /// </summary>
        public static string FormatError(uint errorCode)
        {
            // 检查是否为已知错误
            if (CommonErrors.TryGetValue(errorCode, out var knownError))
            {
                return $"0x{errorCode:X8}: {knownError}";
            }

            // 解析未知错误
            var (severity, domain, code) = ParseErrorCode(errorCode);
            
            var severityStr = severity switch
            {
                ErrorSeverity.Success => "Success",
                ErrorSeverity.Info => "Info",
                ErrorSeverity.Warning => "Warning",
                ErrorSeverity.Error => "Error",
                _ => "Unknown"
            };

            var domainStr = domain switch
            {
                ErrorDomain.Common => "Common",
                ErrorDomain.Security => "Security",
                ErrorDomain.Library => "Library",
                ErrorDomain.Device => "Device",
                ErrorDomain.Host => "Host",
                ErrorDomain.Brom => "BROM",
                ErrorDomain.Da => "DA",
                ErrorDomain.Preloader => "Preloader",
                _ => $"Domain({(int)domain})"
            };

            return $"0x{errorCode:X8}: {severityStr} | {domainStr} | Code 0x{code:X4}";
        }

        /// <summary>
        /// 检查是否为错误（Error级别）
        /// </summary>
        public static bool IsError(uint errorCode)
        {
            return (errorCode & SEVERITY_MASK) == SEVERITY_ERROR;
        }

        /// <summary>
        /// 检查是否为成功
        /// </summary>
        public static bool IsSuccess(uint errorCode)
        {
            return errorCode == 0x00000000;
        }

        /// <summary>
        /// 检查是否为进度报告
        /// </summary>
        public static bool IsProgressReport(uint errorCode)
        {
            return errorCode == 0x40040004 || errorCode == 0x40040005;
        }

        /// <summary>
        /// 检查是否为DA哈希不匹配错误
        /// </summary>
        public static bool IsDaHashMismatch(uint errorCode)
        {
            return errorCode == 0xC0070004;
        }

        /// <summary>
        /// 构造错误码
        /// </summary>
        public static uint MakeErrorCode(ErrorSeverity severity, ErrorDomain domain, ushort code)
        {
            uint severityBits = severity switch
            {
                ErrorSeverity.Success => 0,
                ErrorSeverity.Info => 1,
                ErrorSeverity.Warning => 2,
                ErrorSeverity.Error => 3,
                _ => 3
            };

            return (severityBits << 30) | ((uint)domain << DOMAIN_SHIFT) | code;
        }

        #endregion

        #region 详细描述

        /// <summary>
        /// 获取错误的详细描述（含建议）
        /// </summary>
        public static string GetDetailedDescription(uint errorCode)
        {
            return errorCode switch
            {
                0xC0070004 => @"DA_HASH_MISMATCH (0xC0070004)
    原因: DA签名/哈希验证失败
    可能情况:
    1. DA文件已修改但未签名
    2. 设备启用了DAA (Download Agent Authorization)
    3. 使用了错误的DA文件版本
    4. Carbonara漏洞利用第一次尝试（预期行为）
    建议:
    - 确认设备是否支持未签名DA
    - 检查是否需要使用Kamakiri/Carbonara漏洞
    - 验证DA文件完整性",

                0xC0020003 => @"SECURITY_SLA_REQUIRED (0xC0020003)
    原因: 设备需要SLA (Secure Level Authentication) 认证
    可能情况:
    1. Preloader或BROM需要RSA签名认证
    2. 设备已启用安全启动
    建议:
    - 提供正确的SLA密钥进行认证
    - 检查是否有可用的认证证书",

                0xC0020004 => @"SECURITY_DAA_REQUIRED (0xC0020004)
    原因: 设备需要DAA (Download Agent Authorization) 认证
    可能情况:
    1. DA1需要签名验证才能加载
    2. 设备安全启动已启用
    建议:
    - 使用Kamakiri漏洞临时禁用DAA
    - 使用厂商签名的DA文件",

                0xC0060003 => @"BROM_HANDSHAKE_FAIL (0xC0060003)
    原因: BROM握手失败
    可能情况:
    1. 设备未进入BROM模式
    2. USB连接不稳定
    3. 驱动程序问题
    建议:
    - 确认设备处于BROM模式（断电后短接测试点）
    - 检查USB连接和驱动
    - 尝试更换USB端口",

                _ => FormatError(errorCode)
            };
        }

        #endregion
    }
}
