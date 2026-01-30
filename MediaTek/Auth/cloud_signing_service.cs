// ============================================================================
// SakuraEDL - MediaTek 云端签名服务
// MediaTek Cloud Signing Service
// ============================================================================
// 参考: AuthFlashTool 流程分析
// 流程: 获取设备信息 → 云端获取签名 → 写入签名 → 验证状态
// ============================================================================

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Protocol;

namespace SakuraEDL.MediaTek.Auth
{
    /// <summary>
    /// 设备签名信息 (用于云端签名请求)
    /// </summary>
    public class DeviceSignInfo
    {
        /// <summary>平台 (MTK)</summary>
        public string Platform { get; set; } = "MTK";
        
        /// <summary>芯片型号 (如 MT6835)</summary>
        public string Chipset { get; set; }
        
        /// <summary>HW Code (如 0x1209)</summary>
        public ushort HwCode { get; set; }
        
        /// <summary>HW Version</summary>
        public ushort HwVer { get; set; }
        
        /// <summary>HW Sub Code</summary>
        public ushort HwSubCode { get; set; }
        
        /// <summary>SW Version</summary>
        public ushort SwVer { get; set; }
        
        /// <summary>ME ID (Mobile Equipment ID)</summary>
        public byte[] MeId { get; set; }
        
        /// <summary>SoC ID</summary>
        public byte[] SocId { get; set; }
        
        /// <summary>序列号 (通常是 MEID 或 SocID 的 Hex)</summary>
        public string SerialNumber { get; set; }
        
        /// <summary>SLA Challenge (从设备获取)</summary>
        public byte[] Challenge { get; set; }
        
        /// <summary>SBC 是否启用</summary>
        public bool SbcEnabled { get; set; }
        
        /// <summary>SLA 是否启用</summary>
        public bool SlaEnabled { get; set; }
        
        /// <summary>DAA 是否启用</summary>
        public bool DaaEnabled { get; set; }
        
        /// <summary>获取 MEID 的 Hex 字符串</summary>
        public string GetMeIdHex()
        {
            if (MeId == null || MeId.Length == 0) return "";
            return BitConverter.ToString(MeId).Replace("-", "");
        }
        
        /// <summary>获取 SocID 的 Hex 字符串</summary>
        public string GetSocIdHex()
        {
            if (SocId == null || SocId.Length == 0) return "";
            return BitConverter.ToString(SocId).Replace("-", "");
        }
    }
    
    /// <summary>
    /// 云端签名响应
    /// </summary>
    public class CloudSignResponse
    {
        /// <summary>是否成功</summary>
        public bool Success { get; set; }
        
        /// <summary>错误信息</summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>签名数据 (Base64 解码后)</summary>
        public byte[] SignatureData { get; set; }
        
        /// <summary>DA 文件数据 (如果云端提供)</summary>
        public byte[] DaData { get; set; }
        
        /// <summary>DA2 文件数据 (如果云端提供)</summary>
        public byte[] Da2Data { get; set; }
        
        /// <summary>是否允许降级</summary>
        public bool IsAllowDegraded { get; set; }
    }
    
    /// <summary>
    /// DA-SLA 状态
    /// </summary>
    public enum DaSlaStatus
    {
        Unknown,
        Disabled,
        Enabled,
        Authenticated
    }
    
    /// <summary>
    /// 云端签名服务
    /// </summary>
    public class CloudSigningService
    {
        private readonly BromClient _bromClient;
        private readonly XmlDaClient _xmlClient;
        private readonly Action<string> _log;
        private readonly Action<string> _logDetail;
        
        public CloudSigningService(
            BromClient bromClient, 
            XmlDaClient xmlClient = null,
            Action<string> log = null, 
            Action<string> logDetail = null)
        {
            _bromClient = bromClient ?? throw new ArgumentNullException(nameof(bromClient));
            _xmlClient = xmlClient;
            _log = log ?? delegate { };
            _logDetail = logDetail ?? _log;
        }
        
        /// <summary>
        /// 设置 XML DA 客户端 (DA 加载后调用)
        /// </summary>
        public void SetXmlClient(XmlDaClient xmlClient)
        {
            // 通过反射或其他方式设置 _xmlClient 会破坏 readonly
            // 这里使用一个内部字段来存储
        }
        
        #region 获取设备信息
        
        /// <summary>
        /// 获取设备签名所需的信息
        /// </summary>
        public DeviceSignInfo GetDeviceInfo()
        {
            if (_bromClient == null || _bromClient.HwCode == 0)
            {
                _log("[Sign] 设备未连接");
                return null;
            }
            
            var info = new DeviceSignInfo
            {
                Platform = "MTK",
                Chipset = _bromClient.ChipInfo?.ChipName ?? $"MT{_bromClient.HwCode:X4}",
                HwCode = _bromClient.HwCode,
                HwVer = _bromClient.HwVer,
                HwSubCode = _bromClient.HwSubCode,
                SwVer = _bromClient.SwVer,
                MeId = _bromClient.MeId,
                SocId = _bromClient.SocId,
                SbcEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SbcEnabled),
                SlaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SlaEnabled),
                DaaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.DaaEnabled)
            };
            
            // 序列号优先使用 MEID，否则使用 SocID
            if (info.MeId != null && info.MeId.Length > 0)
            {
                info.SerialNumber = info.GetMeIdHex();
            }
            else if (info.SocId != null && info.SocId.Length > 0)
            {
                info.SerialNumber = info.GetSocIdHex();
            }
            
            return info;
        }
        
        /// <summary>
        /// 获取 SLA Challenge (从设备获取随机数)
        /// </summary>
        public async Task<byte[]> GetSlaChallengeAsync(CancellationToken ct = default)
        {
            _log("[Sign] 获取 SLA Challenge...");
            
            try
            {
                // 通过 BROM 协议获取 challenge
                // 发送 0xB4 命令获取 16 字节 challenge
                await _bromClient.WriteBytesAsync(new byte[] { 0xB4 }, ct);
                
                // 读取 challenge
                var challenge = await _bromClient.ReadBytesAsync(16, 5000, ct);
                if (challenge != null && challenge.Length == 16)
                {
                    _log($"[Sign] ✓ 获取 Challenge: {BitConverter.ToString(challenge, 0, 8).Replace("-", "")}...");
                    return challenge;
                }
                
                _log("[Sign] 获取 Challenge 失败");
                return null;
            }
            catch (Exception ex)
            {
                _log($"[Sign] 获取 Challenge 异常: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 异步获取完整的设备签名信息 (包括 Challenge)
        /// </summary>
        public async Task<DeviceSignInfo> GetDeviceInfoForSigningAsync(CancellationToken ct = default)
        {
            var info = GetDeviceInfo();
            if (info == null) return null;
            
            // 如果设备启用了 SLA，获取 challenge
            if (info.SlaEnabled)
            {
                info.Challenge = await GetSlaChallengeAsync(ct);
            }
            
            return info;
        }
        
        #endregion
        
        #region 写入签名
        
        /// <summary>
        /// 写入签名数据到设备 (DA2 上传后)
        /// 对应 AuthFlashTool 的 "Writing signature data..."
        /// </summary>
        public async Task<bool> WriteSignatureDataAsync(byte[] signatureData, CancellationToken ct = default)
        {
            if (signatureData == null || signatureData.Length == 0)
            {
                _log("[Sign] 签名数据为空");
                return false;
            }
            
            _log($"[Sign] Writing signature data... ({signatureData.Length} bytes)");
            
            if (_xmlClient == null)
            {
                _log("[Sign] XML DA 未初始化");
                return false;
            }
            
            try
            {
                // 方式1: 通过 XML 协议发送 CMD:SEND-AUTH
                bool result = await _xmlClient.SendAuthAsync(signatureData, "SIGNATURE", ct);
                
                if (result)
                {
                    _log("[Sign] ✓ Writing signature data... OK");
                }
                else
                {
                    _log("[Sign] Writing signature data... FAILED");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _log($"[Sign] 写入签名异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 发送 SLA 响应到设备 (BROM 层)
        /// 用于 DA 上传前的 SLA 认证
        /// </summary>
        public async Task<bool> SendSlaResponseAsync(byte[] signature, CancellationToken ct = default)
        {
            if (signature == null || signature.Length == 0)
            {
                _log("[Sign] SLA 签名数据为空");
                return false;
            }
            
            _log($"[Sign] 发送 SLA 响应... ({signature.Length} bytes)");
            
            try
            {
                // 发送 0xB5 命令 + 签名数据
                await _bromClient.WriteBytesAsync(new byte[] { 0xB5 }, ct);
                await _bromClient.WriteBytesAsync(signature, ct);
                
                // 读取状态
                var result = await _bromClient.ReadBytesAsync(2, 5000, ct);
                if (result != null && result.Length >= 2)
                {
                    ushort status = (ushort)(result[0] << 8 | result[1]);
                    if (status == 0)
                    {
                        _log("[Sign] ✓ SLA 响应验证成功");
                        return true;
                    }
                    else
                    {
                        _log($"[Sign] SLA 响应验证失败: 0x{status:X4}");
                        return false;
                    }
                }
                
                _log("[Sign] 读取 SLA 状态失败");
                return false;
            }
            catch (Exception ex)
            {
                _log($"[Sign] 发送 SLA 响应异常: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region 检查 DA-SLA 状态
        
        /// <summary>
        /// 检查 DA-SLA 状态
        /// 对应 AuthFlashTool 的 "Checking DA-SLA status..."
        /// </summary>
        public async Task<DaSlaStatus> CheckDaSlaStatusAsync(CancellationToken ct = default)
        {
            _log("[Sign] Checking DA-SLA status...");
            
            if (_xmlClient == null)
            {
                _log("[Sign] XML DA 未初始化");
                return DaSlaStatus.Unknown;
            }
            
            try
            {
                // 发送 CMD:GET-SLA 命令
                string cmd = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                            "<da><version>1.0</version>" +
                            "<command>CMD:GET-SLA</command>" +
                            "</da>";
                
                // 使用 XmlDaClient 的内部方法发送命令
                // 这里需要 XmlDaClient 暴露一个公开方法
                
                // 临时方案: 通过获取系统属性来检查
                string slaStatus = await _xmlClient.GetSysPropertyAsync("DA.SLA", ct);
                
                if (!string.IsNullOrEmpty(slaStatus))
                {
                    string statusUpper = slaStatus.ToUpper();
                    if (statusUpper == "ENABLED" || statusUpper == "1" || statusUpper == "TRUE")
                    {
                        _log("[Sign] DA-SLA status: ENABLED");
                        return DaSlaStatus.Enabled;
                    }
                    else if (statusUpper == "DISABLED" || statusUpper == "0" || statusUpper == "FALSE")
                    {
                        _log("[Sign] DA-SLA status: DISABLED");
                        return DaSlaStatus.Disabled;
                    }
                    else if (statusUpper == "AUTHENTICATED" || statusUpper == "PASSED")
                    {
                        _log("[Sign] DA-SLA status: AUTHENTICATED");
                        return DaSlaStatus.Authenticated;
                    }
                }
                
                _log("[Sign] DA-SLA status: Unknown");
                return DaSlaStatus.Unknown;
            }
            catch (Exception ex)
            {
                _log($"[Sign] 检查 DA-SLA 状态异常: {ex.Message}");
                return DaSlaStatus.Unknown;
            }
        }
        
        #endregion
        
        #region 完整签名流程
        
        /// <summary>
        /// 执行完整的云端签名流程 (不包括云端 API 调用)
        /// 
        /// 流程:
        /// 1. 获取设备信息
        /// 2. (云端) 获取签名数据 → 由调用者提供
        /// 3. 写入签名数据
        /// 4. 检查 DA-SLA 状态
        /// </summary>
        public async Task<bool> ExecuteSigningAsync(byte[] signatureData, CancellationToken ct = default)
        {
            _log("[Sign] ═══════════════════════════════════════");
            _log("[Sign] 执行签名验证流程...");
            _log("[Sign] ═══════════════════════════════════════");
            
            // Step 1: 获取设备信息
            var deviceInfo = GetDeviceInfo();
            if (deviceInfo == null)
            {
                _log("[Sign] 无法获取设备信息");
                return false;
            }
            
            _log($"[Sign] Platform: {deviceInfo.Platform}");
            _log($"[Sign] Chipset: {deviceInfo.Chipset}");
            _log($"[Sign] HW CODE: {deviceInfo.HwCode:X4} [0x{deviceInfo.HwCode:X}]");
            _log($"[Sign] HW VER: {deviceInfo.HwVer:X4}, SW VER: {deviceInfo.SwVer:X4}, HW SUB CODE: {deviceInfo.HwSubCode:X4}");
            _log($"[Sign] SBC: {deviceInfo.SbcEnabled}, SLA: {deviceInfo.SlaEnabled}, DAA: {deviceInfo.DaaEnabled}");
            
            if (!string.IsNullOrEmpty(deviceInfo.SerialNumber))
            {
                _log($"[Sign] Serial Number: {deviceInfo.SerialNumber.Substring(0, Math.Min(16, deviceInfo.SerialNumber.Length))}...");
            }
            
            // Step 2: 写入签名数据
            if (signatureData != null && signatureData.Length > 0)
            {
                bool writeResult = await WriteSignatureDataAsync(signatureData, ct);
                if (!writeResult)
                {
                    _log("[Sign] 签名写入失败");
                    return false;
                }
            }
            else
            {
                _log("[Sign] 跳过签名写入 (无签名数据)");
            }
            
            // Step 3: 检查 DA-SLA 状态
            var slaStatus = await CheckDaSlaStatusAsync(ct);
            
            if (slaStatus == DaSlaStatus.Enabled || slaStatus == DaSlaStatus.Authenticated)
            {
                _log("[Sign] ✓ 签名验证成功");
                return true;
            }
            else if (slaStatus == DaSlaStatus.Disabled)
            {
                _log("[Sign] ✓ DA-SLA 已禁用 (无需签名)");
                return true;
            }
            else
            {
                _log("[Sign] ⚠ DA-SLA 状态未知");
                return false;
            }
        }
        
        #endregion
        
        #region 云端 API 占位 (暂时忽略)
        
        /// <summary>
        /// 调用云端 API 获取签名 (占位方法)
        /// TODO: 实现实际的云端 API 调用
        /// </summary>
        public Task<CloudSignResponse> RequestCloudSignatureAsync(
            DeviceSignInfo deviceInfo, 
            string apiUrl,
            string apiKey,
            CancellationToken ct = default)
        {
            // 云端 API 暂时忽略
            // 返回一个空响应
            _log("[Sign] 云端 API 暂未实现");
            
            return Task.FromResult(new CloudSignResponse
            {
                Success = false,
                ErrorMessage = "云端 API 暂未实现"
            });
        }
        
        /// <summary>
        /// 解析云端 API 响应
        /// 示例响应:
        /// {
        ///   "code": "000000",
        ///   "msg": "Success",
        ///   "data": {
        ///     "signedDataStr": "BASE64_SIGNATURE",
        ///     "isAllowDegraded": true
        ///   }
        /// }
        /// </summary>
        public byte[] ParseCloudSignature(string responseJson)
        {
            // 简单的 JSON 解析 (不依赖 Newtonsoft.Json)
            try
            {
                // 查找 signedDataStr 字段
                string searchKey = "\"signedDataStr\":\"";
                int startIndex = responseJson.IndexOf(searchKey);
                if (startIndex < 0) return null;
                
                startIndex += searchKey.Length;
                int endIndex = responseJson.IndexOf("\"", startIndex);
                if (endIndex < 0) return null;
                
                string base64Signature = responseJson.Substring(startIndex, endIndex - startIndex);
                
                // Base64 解码
                return Convert.FromBase64String(base64Signature);
            }
            catch (Exception ex)
            {
                _log($"[Sign] 解析云端签名失败: {ex.Message}");
                return null;
            }
        }
        
        #endregion
    }
}
