// ============================================================================
// LoveAlways - MediaTek DA Extensions 加载器
// MediaTek Download Agent Extensions Loader
// ============================================================================
// 从mtk-payloads项目加载和上传DA Extensions到设备
// 参考: https://github.com/shomykohai/mtk-payloads
// ============================================================================

using System;
using System.IO;
using System.Threading.Tasks;
using LoveAlways.MediaTek.Common;
using LoveAlways.MediaTek.Models;
using LoveAlways.MediaTek.Protocol;

namespace LoveAlways.MediaTek.DA
{
    /// <summary>
    /// DA Extensions 加载器
    /// 从mtk-payloads项目加载编译好的Extensions二进制
    /// </summary>
    public class DaExtensionsLoader
    {
        private readonly string _payloadBasePath;
        private readonly MtkLogger _log;

        #region 默认路径配置

        /// <summary>XFlash (V5) Extensions 文件名</summary>
        public const string V5_EXTENSION_FILE = "da_x_ext.bin";
        
        /// <summary>XML (V6) Extensions 文件名</summary>
        public const string V6_EXTENSION_FILE = "da_xml_ext.bin";
        
        /// <summary>默认Payload路径</summary>
        public const string DEFAULT_PAYLOAD_PATH = "Payloads";

        #endregion

        #region 构造函数

        public DaExtensionsLoader(string payloadBasePath = null, MtkLogger logger = null)
        {
            _payloadBasePath = payloadBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DEFAULT_PAYLOAD_PATH);
            _log = logger ?? MtkLog.Instance;
        }

        #endregion

        #region 加载Extensions二进制

        /// <summary>
        /// 根据设备信息加载对应的Extensions二进制
        /// </summary>
        public byte[] LoadExtension(ushort hwCode, MtkDeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
                throw new ArgumentNullException(nameof(deviceInfo));

            var isV6 = deviceInfo.DaMode == 6;
            return LoadExtension(hwCode, isV6);
        }

        /// <summary>
        /// 根据DA模式加载Extensions二进制
        /// </summary>
        public byte[] LoadExtension(ushort hwCode, bool isV6)
        {
            _log.Info($"加载DA Extensions (HW Code: 0x{hwCode:X4}, 模式: {(isV6 ? "V6/XML" : "V5/XFlash")})", LogCategory.Da);

            // 确定文件名
            var fileName = isV6 ? V6_EXTENSION_FILE : V5_EXTENSION_FILE;
            var folderName = isV6 ? "da_xml" : "da_x";
            
            // 尝试多个可能的路径
            var possiblePaths = new[]
            {
                Path.Combine(_payloadBasePath, folderName, fileName),  // Payloads/da_x/da_x_ext.bin
                Path.Combine(_payloadBasePath, fileName),               // Payloads/da_x_ext.bin
                Path.Combine(".", folderName, fileName),                // ./da_x/da_x_ext.bin
                Path.Combine(".", fileName)                             // ./da_x_ext.bin
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var data = File.ReadAllBytes(path);
                        if (ValidateExtensionBinary(data))
                        {
                            _log.Success($"加载成功: {path} ({data.Length} 字节)", LogCategory.Da);
                            return data;
                        }
                        else
                        {
                            _log.Warning($"二进制验证失败: {path}", LogCategory.Da);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"读取文件失败: {path}", LogCategory.Da, ex);
                    }
                }
            }

            // 未找到有效的Extensions文件
            var searchedPaths = string.Join("\n  ", possiblePaths);
            var errorMsg = $"未找到DA Extensions二进制文件\n搜索路径:\n  {searchedPaths}";
            _log.Error(errorMsg, LogCategory.Da);
            
            throw new FileNotFoundException(errorMsg);
        }

        /// <summary>
        /// 验证Extensions二进制是否有效
        /// </summary>
        private bool ValidateExtensionBinary(byte[] binary)
        {
            if (binary == null || binary.Length < 0x100)
                return false;

            // TODO: 添加更详细的验证
            // - 检查ELF魔术值
            // - 验证代码段
            // - 检查入口点
            
            return true;
        }

        #endregion

        #region 加载到设备

        /// <summary>
        /// 加载Extensions到设备
        /// </summary>
        public async Task<bool> LoadToDeviceAsync(
            IBromClient bromClient,
            ushort hwCode,
            MtkDeviceInfo deviceInfo,
            DaExtensionsConfig config = null)
        {
            if (bromClient == null)
                throw new ArgumentNullException(nameof(bromClient));

            _log.LogHeader("DA Extensions 加载流程");

            try
            {
                // 1. 检查兼容性
                _log.Info("检查设备兼容性...", LogCategory.Da);
                if (!DaExtensionsCompatibility.SupportsExtensions(deviceInfo))
                {
                    _log.Error("设备不支持DA Extensions", LogCategory.Da);
                    return false;
                }
                _log.Success("设备兼容性检查通过", LogCategory.Da);

                // 2. 加载二进制
                _log.Info("加载Extensions二进制...", LogCategory.Da);
                var binary = LoadExtension(hwCode, deviceInfo);

                // 3. 准备配置
                if (config == null)
                {
                    config = DaExtensionsHelper.GetRecommendedConfig(hwCode, deviceInfo);
                }
                config.ExtensionsBinary = binary;

                var loadAddr = config.GetLoadAddress();
                _log.Info($"加载地址: 0x{loadAddr:X8} ({(config.UseLowMemoryAddress ? "低内存" : "标准")})", LogCategory.Da);
                _log.Info($"二进制大小: {binary.Length} 字节 ({binary.Length / 1024.0:F2} KB)", LogCategory.Da);

                // 4. 上传到设备
                _log.Info("上传Extensions到设备...", LogCategory.Da);
                
                // TODO: 实际的上传逻辑需要根据BromClient的实现来完成
                // 这里提供接口示例
                /*
                await bromClient.SendBootTo(loadAddr, binary);
                */
                
                _log.Warning("Extensions上传功能待实现 (需要boot_to命令支持)", LogCategory.Da);
                _log.Info("提示: 需要先使用Carbonara漏洞修补DA1，然后通过boot_to加载Extensions", LogCategory.Exploit);

                _log.LogSeparator();
                _log.Success("Extensions配置准备完成", LogCategory.Da);
                
                return true;
            }
            catch (Exception ex)
            {
                _log.Critical("Extensions加载失败", LogCategory.Da, ex);
                return false;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查Payload文件是否存在
        /// </summary>
        public bool CheckPayloadExists(bool isV6)
        {
            var fileName = isV6 ? V6_EXTENSION_FILE : V5_EXTENSION_FILE;
            var folderName = isV6 ? "da_xml" : "da_x";
            
            var path1 = Path.Combine(_payloadBasePath, folderName, fileName);
            var path2 = Path.Combine(_payloadBasePath, fileName);
            
            return File.Exists(path1) || File.Exists(path2);
        }

        /// <summary>
        /// 获取Payload信息
        /// </summary>
        public void PrintPayloadInfo()
        {
            _log.LogHeader("DA Extensions Payload 信息");
            
            _log.Info($"Payload基础路径: {_payloadBasePath}", LogCategory.Da);
            
            // V5 Extensions
            var v5Exists = CheckPayloadExists(false);
            _log.LogDeviceInfo("V5/XFlash Extensions", v5Exists ? "✓ 已安装" : "✗ 未找到", LogCategory.Da);
            
            // V6 Extensions
            var v6Exists = CheckPayloadExists(true);
            _log.LogDeviceInfo("V6/XML Extensions", v6Exists ? "✓ 已安装" : "✗ 未找到", LogCategory.Da);
            
            if (!v5Exists && !v6Exists)
            {
                _log.LogSeparator('-', 60);
                _log.Warning("未找到任何Extensions Payload", LogCategory.Da);
                _log.Info("请从以下位置获取:", LogCategory.Da);
                _log.Info("  https://github.com/shomykohai/mtk-payloads", LogCategory.Da);
                _log.Info("", LogCategory.Da);
                _log.Info("安装方法:", LogCategory.Da);
                _log.Info($"  1. 克隆仓库: git clone https://github.com/shomykohai/mtk-payloads", LogCategory.Da);
                _log.Info($"  2. 编译: cd mtk-payloads && ./build_all.sh", LogCategory.Da);
                _log.Info($"  3. 复制到: {_payloadBasePath}", LogCategory.Da);
            }
            
            _log.LogSeparator();
        }

        /// <summary>
        /// 创建默认的Extensions配置
        /// </summary>
        public DaExtensionsConfig CreateDefaultConfig(ushort hwCode, MtkDeviceInfo deviceInfo)
        {
            var config = DaExtensionsHelper.GetRecommendedConfig(hwCode, deviceInfo);
            
            // 自动加载二进制
            try
            {
                config.ExtensionsBinary = LoadExtension(hwCode, deviceInfo);
            }
            catch (Exception ex)
            {
                _log.Warning($"无法加载Extensions二进制: {ex.Message}", LogCategory.Da);
            }
            
            return config;
        }

        #endregion
    }

    /// <summary>
    /// BROM客户端接口（用于Extensions加载）
    /// </summary>
    public interface IBromClient
    {
        /// <summary>发送boot_to命令加载代码到指定地址</summary>
        Task SendBootTo(uint address, byte[] data);
        
        /// <summary>发送DA命令</summary>
        Task SendDaCommand(uint command, byte[] data = null);
        
        /// <summary>接收DA响应</summary>
        Task<byte[]> ReceiveDaResponse(int length);
    }
}
