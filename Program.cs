using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoveAlways.Spreadtrum.Resources;

namespace LoveAlways
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 检查命令行参数
            if (args.Length > 0)
            {
                if (ProcessCommandLine(args))
                    return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 显示启动动画（Splash）窗体，关闭后再启动主窗体
            using (var splash = new SplashForm())
            {
                splash.ShowDialog();
            }

            Application.Run(new Form1());
        }

        /// <summary>
        /// 处理命令行参数
        /// </summary>
        private static bool ProcessCommandLine(string[] args)
        {
            if (args[0] == "--build-pak" && args.Length >= 3)
            {
                // 构建统一资源包 (SPAK v2)
                string sourceDir = args[1];
                string outputPath = args[2];
                bool compress = args.Length < 4 || args[3] != "--no-compress";

                Console.WriteLine("=== 构建 SPD 资源包 (SPAK v2) ===");
                Console.WriteLine("源目录: " + sourceDir);
                Console.WriteLine("输出文件: " + outputPath);
                Console.WriteLine("压缩: " + (compress ? "是" : "否"));
                Console.WriteLine();

                try
                {
                    SprdPakManager.BuildPak(sourceDir, outputPath, compress);
                    Console.WriteLine("构建完成!");

                    if (File.Exists(outputPath))
                    {
                        var info = new FileInfo(outputPath);
                        Console.WriteLine("文件大小: " + FormatSize(info.Length));
                    }

                    // 加载并显示统计
                    if (SprdPakManager.LoadPak(outputPath))
                    {
                        Console.WriteLine("条目数量: " + SprdPakManager.EntryCount);
                        Console.WriteLine("芯片列表: " + string.Join(", ", SprdPakManager.GetChipNames()));
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("构建失败: " + ex.Message);
                    return true;
                }
            }
            else if (args[0] == "--build-fdl-pak" && args.Length >= 3)
            {
                // 构建 FDL 资源包 (旧格式，向后兼容)
                string sourceDir = args[1];
                string outputPath = args[2];
                bool compress = args.Length < 4 || args[3] != "--no-compress";

                Console.WriteLine("=== 构建 FDL 资源包 (FPAK) ===");
                Console.WriteLine("源目录: " + sourceDir);
                Console.WriteLine("输出文件: " + outputPath);
                Console.WriteLine();

                try
                {
                    FdlPakManager.BuildPak(sourceDir, outputPath, compress);
                    Console.WriteLine("构建完成!");

                    if (File.Exists(outputPath))
                    {
                        var info = new FileInfo(outputPath);
                        Console.WriteLine("文件大小: " + FormatSize(info.Length));
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("构建失败: " + ex.Message);
                    return true;
                }
            }
            else if (args[0] == "--extract-pak" && args.Length >= 3)
            {
                // 解包资源包
                string pakPath = args[1];
                string outputDir = args[2];

                Console.WriteLine("=== 解包资源包 ===");
                Console.WriteLine("资源包: " + pakPath);
                Console.WriteLine("输出目录: " + outputDir);
                Console.WriteLine();

                try
                {
                    // 根据文件头判断格式
                    using (var fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read))
                    {
                        var magic = new byte[4];
                        fs.Read(magic, 0, 4);
                        var magicStr = System.Text.Encoding.ASCII.GetString(magic);

                        if (magicStr == "SPAK")
                        {
                            Console.WriteLine("格式: SPAK v2");
                            SprdPakManager.ExtractPak(pakPath, outputDir);
                        }
                        else if (BitConverter.ToUInt32(magic, 0) == 0x4B415046) // "FPAK"
                        {
                            Console.WriteLine("格式: FPAK (FDL)");
                            FdlPakManager.ExtractPak(pakPath, outputDir);
                        }
                        else
                        {
                            Console.WriteLine("错误: 未知的资源包格式");
                            return true;
                        }
                    }

                    Console.WriteLine("解包完成!");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("解包失败: " + ex.Message);
                    return true;
                }
            }
            else if (args[0] == "--export-index")
            {
                // 导出 FDL 索引
                string outputPath = args.Length >= 2 ? args[1] : "fdl_index.json";
                string format = args.Length >= 3 ? args[2] : "json";

                Console.WriteLine("=== 导出 FDL 索引 ===");
                Console.WriteLine("输出文件: " + outputPath);
                Console.WriteLine();

                try
                {
                    FdlIndex.InitializeFromDatabase();

                    if (format.ToLower() == "csv")
                    {
                        FdlIndex.ExportCsv(outputPath);
                    }
                    else
                    {
                        FdlIndex.ExportIndex(outputPath);
                    }

                    // 显示统计
                    var stats = FdlIndex.GetStatistics();
                    Console.WriteLine(stats.ToString());

                    Console.WriteLine("导出完成!");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("导出失败: " + ex.Message);
                    return true;
                }
            }
            else if (args[0] == "--list-devices")
            {
                // 列出设备
                string filter = args.Length >= 2 ? args[1] : null;

                Console.WriteLine("=== FDL 设备列表 ===");
                Console.WriteLine();

                FdlIndex.InitializeFromDatabase();

                FdlIndex.FdlIndexEntry[] entries;
                if (!string.IsNullOrEmpty(filter))
                {
                    entries = FdlIndex.Search(filter);
                    Console.WriteLine($"搜索: {filter}");
                }
                else
                {
                    entries = FdlIndex.GetAllEntries();
                }

                Console.WriteLine($"共 {entries.Length} 个设备");
                Console.WriteLine();
                Console.WriteLine("{0,-12} {1,-20} {2,-12} {3,-12} {4,-12}", 
                    "芯片", "型号", "品牌", "FDL1地址", "FDL2地址");
                Console.WriteLine(new string('-', 70));

                foreach (var entry in entries.Take(100))
                {
                    Console.WriteLine("{0,-12} {1,-20} {2,-12} 0x{3:X8} 0x{4:X8}",
                        entry.ChipName,
                        entry.DeviceModel.Length > 18 ? entry.DeviceModel.Substring(0, 18) + ".." : entry.DeviceModel,
                        entry.Brand,
                        entry.Fdl1Address,
                        entry.Fdl2Address);
                }

                if (entries.Length > 100)
                {
                    Console.WriteLine($"... 还有 {entries.Length - 100} 个设备");
                }

                return true;
            }
            else if (args[0] == "--help" || args[0] == "-h")
            {
                ShowHelp();
                return true;
            }

            return false;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("LoveAlways - 展讯/高通多功能刷机工具");
            Console.WriteLine();
            Console.WriteLine("用法:");
            Console.WriteLine("  MultiFlash.exe                        启动图形界面");
            Console.WriteLine();
            Console.WriteLine("资源包命令:");
            Console.WriteLine("  --build-pak <源目录> <输出文件> [--no-compress]");
            Console.WriteLine("      构建统一资源包 (SPAK v2)");
            Console.WriteLine();
            Console.WriteLine("  --build-fdl-pak <源目录> <输出文件> [--no-compress]");
            Console.WriteLine("      构建 FDL 资源包 (FPAK)");
            Console.WriteLine();
            Console.WriteLine("  --extract-pak <资源包> <输出目录>");
            Console.WriteLine("      解包资源包");
            Console.WriteLine();
            Console.WriteLine("索引命令:");
            Console.WriteLine("  --export-index [输出文件] [json|csv]");
            Console.WriteLine("      导出 FDL 设备索引");
            Console.WriteLine();
            Console.WriteLine("  --list-devices [搜索词]");
            Console.WriteLine("      列出/搜索支持的设备");
            Console.WriteLine();
            Console.WriteLine("  --help");
            Console.WriteLine("      显示帮助");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  MultiFlash.exe --build-pak SprdResources\\sprd_fdls SprdResources\\sprd.pak");
            Console.WriteLine("  MultiFlash.exe --export-index fdl_index.json");
            Console.WriteLine("  MultiFlash.exe --list-devices Samsung");
            Console.WriteLine("  MultiFlash.exe --list-devices SC8541E");
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return string.Format("{0:0.##} {1}", size, sizes[order]);
        }
    }
}
