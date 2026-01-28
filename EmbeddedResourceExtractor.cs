using System;
using System.IO;
using System.Reflection;

namespace SakuraEDL
{
    /// <summary>
    /// 嵌入式资源提取器 - 将嵌入的 ADB/Fastboot 工具提取到运行目录
    /// </summary>
    public static class EmbeddedResourceExtractor
    {
        private static bool _extracted = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 需要提取的资源文件列表
        /// </summary>
        private static readonly string[] EmbeddedFiles = new string[]
        {
            "adb.exe",
            "fastboot.exe",
            "AdbWinApi.dll",
            "AdbWinUsbApi.dll"
        };

        /// <summary>
        /// 提取所有嵌入的工具文件到程序目录
        /// </summary>
        public static void ExtractAll()
        {
            if (_extracted) return;

            lock (_lock)
            {
                if (_extracted) return;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var assembly = Assembly.GetExecutingAssembly();

                foreach (var fileName in EmbeddedFiles)
                {
                    try
                    {
                        string targetPath = Path.Combine(baseDir, fileName);
                        
                        // 如果文件已存在且不是旧版本，跳过
                        if (File.Exists(targetPath))
                        {
                            // 检查文件是否被锁定或正在使用
                            try
                            {
                                using (var fs = File.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.None))
                                {
                                    // 文件可访问，检查是否需要更新
                                }
                            }
                            catch
                            {
                                // 文件被锁定，跳过
                                continue;
                            }
                        }

                        // 尝试从嵌入式资源提取
                        ExtractResource(assembly, fileName, targetPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"提取 {fileName} 失败: {ex.Message}");
                    }
                }

                _extracted = true;
            }
        }

        /// <summary>
        /// 从嵌入式资源提取单个文件
        /// </summary>
        private static void ExtractResource(Assembly assembly, string fileName, string targetPath)
        {
            // 资源名称格式: {命名空间}.Resources.{文件名}
            string resourceName = $"SakuraEDL.Resources.{fileName.Replace("-", "_")}";
            
            // 尝试不同的资源名称格式
            string[] possibleNames = new string[]
            {
                resourceName,
                $"SakuraEDL.{fileName}",
                $"SakuraEDL.Tools.{fileName}",
                fileName
            };

            Stream resourceStream = null;
            foreach (var name in possibleNames)
            {
                resourceStream = assembly.GetManifestResourceStream(name);
                if (resourceStream != null) break;
            }

            // 如果找不到嵌入资源，尝试从源目录复制
            if (resourceStream == null)
            {
                // 列出所有可用资源以便调试
                var allResources = assembly.GetManifestResourceNames();
                System.Diagnostics.Debug.WriteLine($"可用资源: {string.Join(", ", allResources)}");
                
                // 如果文件已存在于当前目录，无需提取
                if (File.Exists(targetPath))
                    return;
                    
                return;
            }

            try
            {
                using (resourceStream)
                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
                
                System.Diagnostics.Debug.WriteLine($"已提取: {fileName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入 {fileName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取工具文件路径（确保已提取）
        /// </summary>
        public static string GetToolPath(string toolName)
        {
            ExtractAll();
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, toolName);
        }

        /// <summary>
        /// 检查工具是否可用
        /// </summary>
        public static bool IsToolAvailable(string toolName)
        {
            string path = GetToolPath(toolName);
            return File.Exists(path);
        }
    }
}
