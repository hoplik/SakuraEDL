// ============================================================================
// SakuraEDL - Preloader Dump Service | Preloader 转储服务
// ============================================================================
// [ZH] Preloader 转储服务 - 从 MTK 设备内存转储 Preloader
// [EN] Preloader Dump Service - Dump Preloader from MTK device memory
// [JA] Preloaderダンプサービス - MTKデバイスメモリからPreloaderを抽出
// [KO] Preloader 덤프 서비스 - MTK 기기 메모리에서 Preloader 추출
// [RU] Сервис дампа Preloader - Извлечение Preloader из памяти MTK
// [ES] Servicio de volcado Preloader - Extraer Preloader de memoria MTK
// ============================================================================
// Based on MTK META UTILITY V48 reverse engineering
// Copyright (c) 2025-2026 SakuraEDL | Licensed under CC BY-NC-SA 4.0
// ============================================================================
// 
// *** Preloader Dump 是必须优先执行的关键操作 ***
// 
// 功能:
// - 从设备内存转储 Preloader (优先级最高)
// - 解析 Preloader 信息 (MTK_BLOADER_INFO)
// - 提取 EMI 配置 (内存类型、厂商、型号)
// - 设备识别和修复依据
//
// 为什么 Preloader Dump 必须优先？
// 1. Preloader 包含 EMI 配置，是刷机时选择正确固件的关键
// 2. EMI 配置错误会导致设备无法启动（变砖）
// 3. Preloader 信息可用于识别设备硬件版本
// 4. 修复变砖设备时，Preloader 是重要参考
// 5. Preloader 转储后可以进行安全分析
//
// 响应码:
// - 0xC1C2C3C4: Dump ACK，开始接收 Preloader 数据
// - 0xA1A2A3A4: Bypass ACK，安全绕过成功（无 Dump）
// ============================================================================

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Database;
using SakuraEDL.MediaTek.Exploit;
using SakuraEDL.MediaTek.Protocol;

namespace SakuraEDL.MediaTek.Services
{
    /// <summary>
    /// Preloader Dump 结果
    /// </summary>
    public class PreloaderDumpResult
    {
        /// <summary>是否成功</summary>
        public bool Success { get; set; }
        
        /// <summary>错误消息</summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>Preloader 数据</summary>
        public byte[] Data { get; set; }
        
        /// <summary>Preloader 信息</summary>
        public MtkBloaderInfo Info { get; set; }
        
        /// <summary>保存路径</summary>
        public string SavedPath { get; set; }
        
        /// <summary>转储方法</summary>
        public string DumpMethod { get; set; }
    }

    /// <summary>
    /// Preloader Dump 服务
    /// </summary>
    public class PreloaderDumpService
    {
        private BromClient _bromClient;
        private readonly Action<string, Color> _log;
        private readonly Action<int, int> _onProgress;

        public PreloaderDumpService(
            BromClient bromClient,
            Action<string, Color> log = null,
            Action<int, int> onProgress = null)
        {
            _bromClient = bromClient;
            _log = log ?? ((msg, color) => { });
            _onProgress = onProgress ?? ((cur, max) => { });
        }

        /// <summary>
        /// 执行 Preloader 转储
        /// </summary>
        public async Task<PreloaderDumpResult> DumpPreloaderAsync(string outputPath = null, CancellationToken ct = default)
        {
            var result = new PreloaderDumpResult();

            try
            {
                if (_bromClient == null || !_bromClient.IsConnected)
                {
                    result.ErrorMessage = "设备未连接";
                    return result;
                }

                ushort hwCode = _bromClient.HwCode;
                string chipName = MtkChipDatabase.GetChipName(hwCode);
                
                Log($"[Preloader Dump] 芯片: {chipName} (HW: 0x{hwCode:X4})", Color.Cyan);

                // 检查设备模式
                if (!_bromClient.IsBromMode)
                {
                    Log("[Preloader Dump] 设备在 Preloader 模式，尝试读取 Preloader 信息...", Color.Yellow);
                    
                    // Preloader 模式下，可以通过某些方法获取 Preloader 信息
                    // 但完整转储通常需要 BROM 模式或 exploit
                    result.DumpMethod = "Preloader Mode (Limited)";
                }
                else
                {
                    result.DumpMethod = "BROM Exploit";
                }

                // 获取芯片配置
                var chipRecord = MtkChipDatabase.GetChip(hwCode);
                
                // 检查是否支持 exploit
                if (chipRecord == null || !chipRecord.HasExploit)
                {
                    Log("[Preloader Dump] ⚠ 此芯片可能不支持 Preloader 转储", Color.Orange);
                }

                // 方法1: 通过 BROM Exploit 转储
                if (_bromClient.IsBromMode)
                {
                    var exploitResult = await DumpViaBromExploitAsync(hwCode, ct);
                    if (exploitResult != null && exploitResult.Length > 0)
                    {
                        result.Data = exploitResult;
                        result.Success = true;
                    }
                }

                // 方法2: 通过 DA 内存读取 (如果 exploit 失败)
                if (!result.Success && _bromClient.State == MtkDeviceState.Da2Loaded)
                {
                    Log("[Preloader Dump] 尝试通过 DA 读取...", Color.Cyan);
                    var daResult = await DumpViaDaAsync(ct);
                    if (daResult != null && daResult.Length > 0)
                    {
                        result.Data = daResult;
                        result.DumpMethod = "DA Memory Read";
                        result.Success = true;
                    }
                }

                // 解析 Preloader 信息
                if (result.Success && result.Data != null)
                {
                    Log($"[Preloader Dump] ✓ 转储成功 ({result.Data.Length} 字节)", Color.Green);
                    
                    var parser = new PreloaderParser(s => Log(s, Color.Gray));
                    result.Info = parser.ParseFromData(result.Data);

                    if (result.Info != null)
                    {
                        Log($"[Preloader Dump] === Preloader 信息 ===", Color.Cyan);
                        Log($"[Preloader Dump] 平台: {result.Info.Platform}", Color.White);
                        Log($"[Preloader Dump] EMI: {result.Info.EmiName}", Color.White);
                        Log($"[Preloader Dump] 版本: {result.Info.Version}", Color.White);
                        Log($"[Preloader Dump] 编译: {result.Info.BuildTime}", Color.Gray);
                    }

                    // 保存文件
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        result.SavedPath = outputPath;
                    }
                    else
                    {
                        // 生成默认文件名
                        string fileName = $"preloader_{chipName}_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
                        result.SavedPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                            fileName);
                    }

                    if (parser.SavePreloader(result.Data, result.SavedPath, result.Info))
                    {
                        Log($"[Preloader Dump] 已保存到: {result.SavedPath}", Color.Green);
                    }
                }
                else if (!result.Success)
                {
                    result.ErrorMessage = "Preloader 转储失败";
                    Log("[Preloader Dump] ✗ 转储失败", Color.Red);
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                Log($"[Preloader Dump] 异常: {ex.Message}", Color.Red);
                return result;
            }
        }

        /// <summary>
        /// 通过 BROM Exploit 转储 (优先使用 Dump Payload)
        /// </summary>
        private async Task<byte[]> DumpViaBromExploitAsync(ushort hwCode, CancellationToken ct)
        {
            try
            {
                Log("[Preloader Dump] *** 执行 BROM Exploit (优先 Dump) ***", Color.Yellow);

                // 优先获取 Dump Payload
                byte[] payload;
                if (ExploitPayloadManager.SupportsDump(hwCode))
                {
                    payload = ExploitPayloadManager.GetDumpPayload(hwCode);
                    Log($"[Preloader Dump] 使用专用 Dump Payload (HW: 0x{hwCode:X4})", Color.Cyan);
                    Log("[Preloader Dump] 期望响应: 0xC1C2C3C4 (Dump ACK)", Color.Gray);
                }
                else
                {
                    payload = ExploitPayloadManager.GetPayload(hwCode);
                    Log("[Preloader Dump] 无专用 Dump Payload，使用通用 Payload", Color.Orange);
                    Log("[Preloader Dump] 可能返回 0xA1A2A3A4 (Bypass) 或 0xC1C2C3C4 (Dump)", Color.Gray);
                }

                if (payload == null || payload.Length == 0)
                {
                    Log("[Preloader Dump] 未找到 Exploit Payload", Color.Orange);
                    return null;
                }

                Log($"[Preloader Dump] Payload 大小: {payload.Length} 字节", Color.Gray);

                // 创建 exploit 框架
                var exploit = new BromExploitFramework(
                    _bromClient.GetPort(),
                    s => Log(s, Color.Yellow),
                    s => Log(s, Color.Gray),
                    _bromClient.GetPortLock()
                );

                // 执行 exploit (dumpPreloader = true 确保优先 Dump)
                var result = await exploit.ExecuteExploitAsync(hwCode, payload, dumpPreloader: true, ct);
                
                if (result.Success)
                {
                    if (result.PreloaderData != null && result.PreloaderData.Length > 0)
                    {
                        Log($"[Preloader Dump] ✓ Dump 成功! 数据大小: {result.PreloaderData.Length} 字节", Color.Green);
                        return result.PreloaderData;
                    }
                    else if (result.ExploitType == "Bypass")
                    {
                        Log("[Preloader Dump] ⚠ 收到 Bypass ACK，无 Dump 数据", Color.Orange);
                        Log("[Preloader Dump] 提示: 此芯片可能需要专用 Dump Payload", Color.Gray);
                    }
                }
                else
                {
                    Log($"[Preloader Dump] Exploit 失败: {result.ErrorMessage}", Color.Red);
                }

                return null;
            }
            catch (Exception ex)
            {
                Log($"[Preloader Dump] Exploit 异常: {ex.Message}", Color.Red);
                return null;
            }
        }

        /// <summary>
        /// 通过 DA 内存读取转储
        /// </summary>
        private async Task<byte[]> DumpViaDaAsync(CancellationToken ct)
        {
            // 这需要 DA 已加载，通过 read_register 或 read_flash 命令读取
            // 具体实现依赖于 XmlDaClient 的 ReadRegisterAsync 或 ReadFlashAsync
            
            // 暂时返回 null，需要更复杂的实现
            await Task.CompletedTask;
            return null;
        }

        /// <summary>
        /// 从文件解析 Preloader
        /// </summary>
        public MtkBloaderInfo ParsePreloaderFile(string filePath)
        {
            var parser = new PreloaderParser(s => Log(s, Color.Gray));
            return parser.ParseFromFile(filePath);
        }

        /// <summary>
        /// 检查 Preloader 是否启用安全保护
        /// </summary>
        public bool IsSecurePreloader(byte[] data)
        {
            var parser = new PreloaderParser();
            return parser.IsSecurePreloader(data);
        }

        /// <summary>
        /// 提取 EMI 名称
        /// </summary>
        public string ExtractEmiName(byte[] data)
        {
            var parser = new PreloaderParser();
            var info = parser.ParseFromData(data);
            return info?.EmiName;
        }

        private void Log(string message, Color color)
        {
            _log?.Invoke(message, color);
        }
    }
}
