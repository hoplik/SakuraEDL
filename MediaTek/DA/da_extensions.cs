// ============================================================================
// SakuraEDL - MediaTek DA Extensions 支持框架
// MediaTek Download Agent Extensions Support Framework
// ============================================================================
// 参考: Penumbra 文档 https://shomy.is-a.dev/penumbra/Mediatek/Common/DA/DA-Extensions
// DA Extensions由bkerler开发，用于移除厂商DA的限制，恢复RPMB/寄存器访问等功能
// ============================================================================

using System;
using SakuraEDL.MediaTek.Models;

namespace SakuraEDL.MediaTek.DA
{
    /// <summary>
    /// DA Extensions 配置
    /// </summary>
    public class DaExtensionsConfig
    {
        /// <summary>
        /// 标准加载地址 (DRAM空间)
        /// </summary>
        public const uint STANDARD_LOAD_ADDR = 0x68000000;
        
        /// <summary>
        /// 低内存设备加载地址 (XFlash协议)
        /// 参考: https://github.com/bkerler/mtkclient/pull/1563
        /// </summary>
        public const uint LOW_MEM_LOAD_ADDR = 0x4FFF0000;
        
        /// <summary>
        /// DA2通常加载地址
        /// </summary>
        public const uint DA2_LOAD_ADDR = 0x40000000;
        
        /// <summary>
        /// DA1通常加载地址范围
        /// </summary>
        public const uint DA1_MEM_START = 0x00200000;
        public const uint DA1_MEM_END = 0x00300000;

        /// <summary>
        /// 是否使用低内存地址
        /// </summary>
        public bool UseLowMemoryAddress { get; set; }

        /// <summary>
        /// Extensions二进制数据
        /// </summary>
        public byte[] ExtensionsBinary { get; set; }

        /// <summary>
        /// 获取加载地址
        /// </summary>
        public uint GetLoadAddress()
        {
            return UseLowMemoryAddress ? LOW_MEM_LOAD_ADDR : STANDARD_LOAD_ADDR;
        }
    }

    /// <summary>
    /// XFlash (V5) DA Extensions 命令
    /// 命令范围: 0x0F0000 - 0x0FFFFF
    /// </summary>
    public static class XFlashExtensionCommands
    {
        public const uint CMD_RANGE_START = 0x0F0000;
        public const uint CMD_RANGE_END   = 0x0FFFFF;

        // RPMB操作
        public const uint CMD_READ_RPMB   = 0x0F0001;
        public const uint CMD_WRITE_RPMB  = 0x0F0002;

        // 寄存器访问
        public const uint CMD_READ_REG    = 0x0F0003;
        public const uint CMD_WRITE_REG   = 0x0F0004;
        public const uint CMD_READ_REG16  = 0x0F0005;
        public const uint CMD_WRITE_REG16 = 0x0F0006;

        // SEJ (Security Engine) 操作
        public const uint CMD_SEJ_DECRYPT = 0x0F0007;
        public const uint CMD_SEJ_ENCRYPT = 0x0F0008;

        // 内存操作
        public const uint CMD_READ_MEM    = 0x0F0009;
        public const uint CMD_WRITE_MEM   = 0x0F000A;

        /// <summary>
        /// 检查命令是否为Extensions命令
        /// </summary>
        public static bool IsExtensionCommand(uint command)
        {
            return command >= CMD_RANGE_START && command <= CMD_RANGE_END;
        }
    }

    /// <summary>
    /// XML (V6) DA Extensions 命令
    /// 使用XML协议的命令字符串
    /// </summary>
    public static class XmlExtensionCommands
    {
        // RPMB操作
        public const string CMD_READ_RPMB = "CMD:READ-RPMB";
        public const string CMD_WRITE_RPMB = "CMD:WRITE-RPMB";

        // 寄存器访问
        public const string CMD_READ_REG = "CMD:READ-REGISTER";
        public const string CMD_WRITE_REG = "CMD:WRITE-REGISTER";

        // SEJ操作
        public const string CMD_SEJ = "CMD:SEJ-OPERATION";

        // 内存操作
        public const string CMD_READ_MEM = "CMD:READ-MEMORY";
        public const string CMD_WRITE_MEM = "CMD:WRITE-MEMORY";
    }

    /// <summary>
    /// DA Extensions 兼容性检测
    /// </summary>
    public static class DaExtensionsCompatibility
    {
        /// <summary>
        /// 检查设备是否支持DA Extensions
        /// 
        /// 要求:
        /// 1. 必须能够加载修补过的DA（至少自定义DA2）
        /// 2. Carbonara漏洞未被修补（2024年后的设备可能已修补）
        /// 3. 设备未启用严格的DA验证
        /// </summary>
        public static bool SupportsExtensions(MtkDeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
                return false;

            // 检查是否为V5/V6 DA（Extensions只支持这两种）
            var daMode = deviceInfo.DaMode;
            if (daMode != 5 && daMode != 6)  // 5=XFlash, 6=XML
                return false;

            // TODO: 添加Carbonara修补检测
            // if (IsCarbonaraPatched(deviceInfo))
            //     return false;

            // 2024年后的设备可能不支持
            // 这里需要更详细的芯片/日期检测
            return true;
        }

        /// <summary>
        /// 检查DA是否已修补Carbonara（影响Extensions加载）
        /// 参考: Penumbra文档 - 2024年后修补了boot_to硬编码地址
        /// </summary>
        public static bool IsCarbonaraPatched(byte[] da2Data)
        {
            if (da2Data == null || da2Data.Length < 0x1000)
                return true;  // 保守策略：无法确认时假设已修补

            // 检查是否包含硬编码的0x40000000地址（修补后的特征）
            // 修补后的DA2会强制使用0x40000000作为boot_to地址
            byte[] hardcodedAddr = { 0x00, 0x00, 0x00, 0x40 };  // 0x40000000 LE
            
            int count = 0;
            for (int i = 0; i < da2Data.Length - 4; i++)
            {
                if (da2Data[i] == hardcodedAddr[0] &&
                    da2Data[i + 1] == hardcodedAddr[1] &&
                    da2Data[i + 2] == hardcodedAddr[2] &&
                    da2Data[i + 3] == hardcodedAddr[3])
                {
                    count++;
                }
            }

            // 如果出现多次硬编码地址，可能是修补后的DA
            return count > 3;
        }

        /// <summary>
        /// 判断设备是否为低内存设备
        /// 低内存设备需要使用特殊的Extensions加载地址
        /// </summary>
        public static bool IsLowMemoryDevice(ushort hwCode)
        {
            // 通常入门级芯片（如MT6739, MT6761等）内存较小
            return hwCode switch
            {
                0x0699 => true,  // MT6739
                0x0562 => true,  // MT6761
                0x0707 => true,  // MT6762
                _ => false
            };
        }
    }

    /// <summary>
    /// DA Extensions 状态
    /// </summary>
    public enum ExtensionsStatus
    {
        /// <summary>未加载</summary>
        NotLoaded,
        
        /// <summary>加载中</summary>
        Loading,
        
        /// <summary>已加载</summary>
        Loaded,
        
        /// <summary>不支持</summary>
        NotSupported,
        
        /// <summary>加载失败</summary>
        LoadFailed
    }

    /// <summary>
    /// DA Extensions 管理器（接口定义）
    /// </summary>
    public interface IDaExtensionsManager
    {
        /// <summary>当前Extensions状态</summary>
        ExtensionsStatus Status { get; }

        /// <summary>检查是否支持Extensions</summary>
        bool IsSupported();

        /// <summary>加载Extensions到设备</summary>
        bool LoadExtensions(DaExtensionsConfig config);

        /// <summary>卸载Extensions</summary>
        void UnloadExtensions();

        /// <summary>读取RPMB</summary>
        byte[] ReadRpmb(uint address, uint length);

        /// <summary>写入RPMB</summary>
        bool WriteRpmb(uint address, byte[] data);

        /// <summary>读取寄存器</summary>
        uint ReadRegister(uint address);

        /// <summary>写入寄存器</summary>
        bool WriteRegister(uint address, uint value);

        /// <summary>SEJ解密</summary>
        byte[] SejDecrypt(byte[] data);

        /// <summary>SEJ加密</summary>
        byte[] SejEncrypt(byte[] data);
    }

    /// <summary>
    /// DA Extensions 工具类
    /// </summary>
    public static class DaExtensionsHelper
    {
        /// <summary>
        /// 获取推荐的Extensions配置
        /// </summary>
        public static DaExtensionsConfig GetRecommendedConfig(ushort hwCode, MtkDeviceInfo deviceInfo)
        {
            var config = new DaExtensionsConfig
            {
                UseLowMemoryAddress = DaExtensionsCompatibility.IsLowMemoryDevice(hwCode),
                // ExtensionsBinary 需要从外部加载
            };

            return config;
        }

        /// <summary>
        /// 验证Extensions二进制是否有效
        /// </summary>
        public static bool ValidateExtensionsBinary(byte[] binary)
        {
            if (binary == null || binary.Length < 0x1000)
                return false;

            // TODO: 添加更详细的验证逻辑
            // 例如：检查ELF头、魔术值等

            return true;
        }

        /// <summary>
        /// 获取Extensions版本信息
        /// </summary>
        public static string GetExtensionsVersion()
        {
            // TODO: 从Extensions二进制中提取版本信息
            return "1.0.0";
        }
    }

    /// <summary>
    /// DA Extensions 异常
    /// </summary>
    public class DaExtensionsException : Exception
    {
        public DaExtensionsException(string message) : base(message) { }
        public DaExtensionsException(string message, Exception innerException) : base(message, innerException) { }
    }
}
