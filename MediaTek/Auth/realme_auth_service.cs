// ============================================================================
// SakuraEDL - Realme/OPPO/OnePlus 云端签名认证服务
// Realme Cloud Authentication Service
// ============================================================================
// 支持 Realme、OPPO、OnePlus 设备的云端签名认证
// API 响应格式: {"code":"000000","msg":"Success","data":{"signedDataStr":"BASE64","isAllowDegraded":true}}
// ============================================================================

using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SakuraEDL.MediaTek.Common;
using SakuraEDL.MediaTek.Protocol;
using static SakuraEDL.MediaTek.Protocol.TargetConfigFlags;

namespace SakuraEDL.MediaTek.Auth
{
    /// <summary>
    /// 签名服务类型
    /// </summary>
    public enum SignServerType
    {
        /// <summary>Realme 签名服务</summary>
        Realme,
        /// <summary>OPPO 签名服务</summary>
        Oppo,
        /// <summary>OnePlus 签名服务</summary>
        OnePlus,
        /// <summary>自定义签名服务</summary>
        Custom
    }
    
    /// <summary>
    /// Realme 签名请求
    /// </summary>
    public class RealmSignRequest
    {
        /// <summary>平台 (MTK)</summary>
        public string Platform { get; set; } = "MTK";
        
        /// <summary>芯片型号 (如 MT6768)</summary>
        public string Chipset { get; set; }
        
        /// <summary>序列号 (MEID 或 SocID)</summary>
        public string SerialNumber { get; set; }
        
        /// <summary>HW Code (16进制字符串)</summary>
        public string HwCode { get; set; }
        
        /// <summary>HW Version (16进制字符串)</summary>
        public string HwVer { get; set; }
        
        /// <summary>HW Sub Code (16进制字符串)</summary>
        public string HwSubCode { get; set; }
        
        /// <summary>Challenge 数据 (16进制字符串)</summary>
        public string Challenge { get; set; }
        
        /// <summary>ME ID (16进制字符串)</summary>
        public string MeId { get; set; }
        
        /// <summary>SoC ID (16进制字符串)</summary>
        public string SocId { get; set; }
        
        /// <summary>设备信息 Blob (Base64)</summary>
        public string DeviceBlob { get; set; }
        
        /// <summary>Auth 数据 (16进制字符串)</summary>
        public string AuthData { get; set; }
        
        /// <summary>账号/Token</summary>
        public string Token { get; set; }
        
        // === 官方 API 扩展字段 ===
        
        /// <summary>芯片序列号 (chip_sn)</summary>
        public string ChipSn { get; set; }
        
        /// <summary>磁盘 ID (disk_id)</summary>
        public string DiskId { get; set; }
        
        /// <summary>随机数 (random_num) - 来自 Challenge</summary>
        public string RandomNum { get; set; }
        
        /// <summary>项目编号 (project_no)</summary>
        public string ProjectNo { get; set; }
        
        /// <summary>软件名称签名 (sw_name_sign)</summary>
        public string SwNameSign { get; set; }
        
        /// <summary>MAC 地址</summary>
        public string MacAddress { get; set; }
        
        /// <summary>读写模式 (W=写入)</summary>
        public string ReadWriteMode { get; set; } = "W";
        
        /// <summary>META 版本</summary>
        public string MetaVer { get; set; } = "0";
        
        /// <summary>版本</summary>
        public string Version { get; set; } = "0";
        
        /// <summary>锁定版本</summary>
        public string LockVer { get; set; } = "1";
        
        /// <summary>登录类型</summary>
        public string LoginType { get; set; } = "1";
        
        /// <summary>转换为 JSON 字符串 (标准格式)</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"platform\":\"{Platform}\",");
            sb.Append($"\"chipset\":\"{Chipset}\",");
            sb.Append($"\"serial_number\":\"{SerialNumber}\",");
            sb.Append($"\"hw_code\":\"{HwCode}\",");
            sb.Append($"\"hw_ver\":\"{HwVer}\",");
            sb.Append($"\"hw_sub_code\":\"{HwSubCode}\"");
            if (!string.IsNullOrEmpty(MeId))
            {
                sb.Append($",\"meid\":\"{MeId}\"");
            }
            if (!string.IsNullOrEmpty(SocId))
            {
                sb.Append($",\"socid\":\"{SocId}\"");
            }
            if (!string.IsNullOrEmpty(Challenge))
            {
                sb.Append($",\"challenge\":\"{Challenge}\"");
            }
            if (!string.IsNullOrEmpty(AuthData))
            {
                sb.Append($",\"auth_data\":\"{AuthData}\"");
            }
            if (!string.IsNullOrEmpty(DeviceBlob))
            {
                sb.Append($",\"device_blob\":\"{DeviceBlob}\"");
            }
            if (!string.IsNullOrEmpty(Token))
            {
                sb.Append($",\"token\":\"{Token}\"");
            }
            sb.Append("}");
            return sb.ToString();
        }
        
        /// <summary>转换为官方 API 格式 (OPPO/Realme/OnePlus)</summary>
        public string ToOfficialJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"chip_sn\":\"{ChipSn ?? MeId ?? SerialNumber}\",");
            sb.Append($"\"disk_id\":\"{DiskId ?? SocId}\",");
            sb.Append($"\"ext_ip\":\"0.0.0.0\",");
            sb.Append($"\"mac\":\"{MacAddress ?? "00-00-00-00-00-00"}\",");
            sb.Append($"\"main_platform\":\"{Platform}\",");
            sb.Append($"\"meta_ver\":\"{MetaVer}\",");
            sb.Append($"\"new_project_no\":\"{ProjectNo}\",");
            sb.Append($"\"new_sw_name_sign\":\"{SwNameSign}\",");
            sb.Append($"\"old_project_no\":\"{ProjectNo}\",");
            sb.Append($"\"old_sw_name_sign\":\"{SwNameSign}\",");
            sb.Append($"\"random_num\":\"{RandomNum ?? Challenge}\",");
            sb.Append($"\"read_write_mode\":\"{ReadWriteMode}\",");
            sb.Append($"\"sub_platform\":\"{Chipset}\",");
            sb.Append($"\"token\":\"{Token}\",");
            sb.Append($"\"version\":\"{Version}\",");
            sb.Append($"\"lock_ver\":\"{LockVer}\",");
            sb.Append($"\"login_type\":\"{LoginType}\"");
            sb.Append("}");
            return sb.ToString();
        }
        
        /// <summary>转换为 GSMFuture API 格式</summary>
        public string ToGsmFutureJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"chip_sn\":\"{ChipSn ?? MeId ?? SerialNumber}\",");
            sb.Append($"\"disk_id\":\"{DiskId ?? SocId}\",");
            sb.Append($"\"main_platform\":\"{Platform}\",");
            sb.Append($"\"sub_platform\":\"{Chipset}\",");
            sb.Append($"\"random_num\":\"{RandomNum ?? Challenge}\"");
            if (!string.IsNullOrEmpty(ProjectNo))
            {
                sb.Append($",\"new_project_no\":\"{ProjectNo}\"");
                sb.Append($",\"old_project_no\":\"{ProjectNo}\"");
            }
            if (!string.IsNullOrEmpty(SwNameSign))
            {
                sb.Append($",\"new_sw_name_sign\":\"{SwNameSign}\"");
                sb.Append($",\"old_sw_name_sign\":\"{SwNameSign}\"");
            }
            if (!string.IsNullOrEmpty(Token))
            {
                sb.Append($",\"token\":\"{Token}\"");
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Realme 签名响应
    /// </summary>
    public class RealmSignResponse
    {
        /// <summary>状态码 (000000 = 成功)</summary>
        public string Code { get; set; }
        
        /// <summary>消息</summary>
        public string Message { get; set; }
        
        /// <summary>是否成功</summary>
        public bool Success => Code == "000000";
        
        /// <summary>签名数据 (Base64 编码)</summary>
        public string SignedDataStr { get; set; }
        
        /// <summary>签名数据 (解码后的字节数组)</summary>
        public byte[] SignatureData { get; set; }
        
        /// <summary>是否允许降级</summary>
        public bool IsAllowDegraded { get; set; }
        
        /// <summary>错误信息</summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>剩余额度</summary>
        public int Credit { get; set; }
        
        /// <summary>账号</summary>
        public string Account { get; set; }
    }
    
    /// <summary>
    /// Realme/OPPO/OnePlus 云端签名认证服务
    /// </summary>
    public class RealmeAuthService
    {
        private readonly BromClient _bromClient;
        private XmlDaClient _xmlClient;
        private readonly Action<string> _log;
        private readonly Action<string> _logDetail;
        
        // API 配置
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
        public string Account { get; set; }
        public SignServerType ServerType { get; set; } = SignServerType.Realme;
        
        // 超时配置
        public int HttpTimeoutMs { get; set; } = 30000;
        
        // 签名结果
        public RealmSignResponse LastResponse { get; private set; }
        
        public RealmeAuthService(
            BromClient bromClient,
            Action<string> log = null,
            Action<string> logDetail = null)
        {
            _bromClient = bromClient ?? throw new ArgumentNullException(nameof(bromClient));
            _log = log ?? delegate { };
            _logDetail = logDetail ?? _log;
        }
        
        /// <summary>
        /// 设置 XML DA 客户端
        /// </summary>
        public void SetXmlClient(XmlDaClient xmlClient)
        {
            _xmlClient = xmlClient;
        }
        
        #region 设备信息
        
        /// <summary>
        /// 获取完整的设备信息 Blob (用于云端签名)
        /// 
        /// Blob 格式:
        /// [0-1]   HW Code (2 bytes, Big Endian)
        /// [2-3]   HW Version (2 bytes, Big Endian)
        /// [4-5]   HW Sub Code (2 bytes, Big Endian)
        /// [6-7]   SW Version (2 bytes, Big Endian)
        /// [8-11]  Target Config (4 bytes, Big Endian)
        /// [12-27] ME ID (16 bytes)
        /// [28-59] SoC ID (32 bytes)
        /// 
        /// 总长度: 60 字节
        /// </summary>
        public byte[] GetDeviceInfoBlob()
        {
            if (_bromClient == null || _bromClient.HwCode == 0)
            {
                _log("[Realme] 设备未连接");
                return null;
            }
            
            try
            {
                // 创建 60 字节的 blob
                byte[] blob = new byte[60];
                int offset = 0;
                
                // HW Code (2 bytes, Big Endian)
                blob[offset++] = (byte)(_bromClient.HwCode >> 8);
                blob[offset++] = (byte)(_bromClient.HwCode & 0xFF);
                
                // HW Version (2 bytes, Big Endian)
                blob[offset++] = (byte)(_bromClient.HwVer >> 8);
                blob[offset++] = (byte)(_bromClient.HwVer & 0xFF);
                
                // HW Sub Code (2 bytes, Big Endian)
                blob[offset++] = (byte)(_bromClient.HwSubCode >> 8);
                blob[offset++] = (byte)(_bromClient.HwSubCode & 0xFF);
                
                // SW Version (2 bytes, Big Endian)
                blob[offset++] = (byte)(_bromClient.SwVer >> 8);
                blob[offset++] = (byte)(_bromClient.SwVer & 0xFF);
                
                // Target Config (4 bytes, Big Endian)
                uint config = (uint)_bromClient.TargetConfig;
                blob[offset++] = (byte)(config >> 24);
                blob[offset++] = (byte)(config >> 16);
                blob[offset++] = (byte)(config >> 8);
                blob[offset++] = (byte)(config & 0xFF);
                
                // ME ID (16 bytes)
                if (_bromClient.MeId != null && _bromClient.MeId.Length > 0)
                {
                    int copyLen = Math.Min(16, _bromClient.MeId.Length);
                    Array.Copy(_bromClient.MeId, 0, blob, offset, copyLen);
                }
                offset += 16;
                
                // SoC ID (32 bytes)
                if (_bromClient.SocId != null && _bromClient.SocId.Length > 0)
                {
                    int copyLen = Math.Min(32, _bromClient.SocId.Length);
                    Array.Copy(_bromClient.SocId, 0, blob, offset, copyLen);
                }
                
                _log($"[Realme] 设备信息 Blob: {blob.Length} 字节");
                _logDetail($"[Realme] HW Code: 0x{_bromClient.HwCode:X4}");
                _logDetail($"[Realme] ME ID: {(_bromClient.MeId != null ? BitConverter.ToString(_bromClient.MeId).Replace("-", "") : "N/A")}");
                _logDetail($"[Realme] SoC ID: {(_bromClient.SocId != null ? BitConverter.ToString(_bromClient.SocId).Replace("-", "").Substring(0, Math.Min(16, _bromClient.SocId.Length * 2)) + "..." : "N/A")}");
                
                return blob;
            }
            catch (Exception ex)
            {
                _log($"[Realme] 获取设备信息 Blob 失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取设备信息 Blob 的 Hex 字符串 (用于 API 请求)
        /// </summary>
        public string GetDeviceInfoBlobHex()
        {
            byte[] blob = GetDeviceInfoBlob();
            if (blob == null) return null;
            return BitConverter.ToString(blob).Replace("-", "");
        }
        
        /// <summary>
        /// 获取设备信息 Blob 的 Base64 字符串 (用于 API 请求)
        /// </summary>
        public string GetDeviceInfoBlobBase64()
        {
            byte[] blob = GetDeviceInfoBlob();
            if (blob == null) return null;
            return Convert.ToBase64String(blob);
        }
        
        /// <summary>
        /// 从设备获取签名请求信息
        /// </summary>
        public RealmSignRequest GetSignRequest()
        {
            if (_bromClient == null || _bromClient.HwCode == 0)
            {
                _log("[Realme] 设备未连接");
                return null;
            }
            
            var request = new RealmSignRequest
            {
                Platform = "MTK",
                Chipset = _bromClient.ChipInfo?.ChipName ?? $"MT{_bromClient.HwCode:X4}",
                HwCode = $"0x{_bromClient.HwCode:X4}",
                HwVer = $"0x{_bromClient.HwVer:X4}",
                HwSubCode = $"0x{_bromClient.HwSubCode:X4}"
            };
            
            // ME ID → chip_sn
            if (_bromClient.MeId != null && _bromClient.MeId.Length > 0)
            {
                request.MeId = BitConverter.ToString(_bromClient.MeId).Replace("-", "");
                request.SerialNumber = request.MeId;
                // chip_sn 是 ME ID 的文本表示 (可能需要转换)
                request.ChipSn = ConvertToChipSn(_bromClient.MeId);
            }
            
            // SoC ID → disk_id
            if (_bromClient.SocId != null && _bromClient.SocId.Length > 0)
            {
                request.SocId = BitConverter.ToString(_bromClient.SocId).Replace("-", "");
                request.DiskId = request.SocId;
                if (string.IsNullOrEmpty(request.SerialNumber))
                {
                    request.SerialNumber = request.SocId;
                }
            }
            
            // 设备 Blob (Base64)
            request.DeviceBlob = GetDeviceInfoBlobBase64();
            
            // Token/账号
            request.Token = Account;
            
            // 获取 MAC 地址
            request.MacAddress = GetLocalMacAddress();
            
            return request;
        }
        
        /// <summary>
        /// 转换 ME ID 为 chip_sn 格式 (16字节转文本)
        /// </summary>
        private string ConvertToChipSn(byte[] meId)
        {
            if (meId == null || meId.Length == 0)
                return "";
            
            // 尝试将字节转换为可读字符串
            // 如果是可打印字符，直接转换
            bool isPrintable = true;
            foreach (byte b in meId)
            {
                if (b < 0x20 || b > 0x7E)
                {
                    isPrintable = false;
                    break;
                }
            }
            
            if (isPrintable)
            {
                return Encoding.ASCII.GetString(meId).Trim('\0');
            }
            
            // 否则返回 Hex 字符串的前16位
            string hex = BitConverter.ToString(meId).Replace("-", "");
            return hex.Length > 16 ? hex.Substring(0, 16) : hex;
        }
        
        /// <summary>
        /// 获取本机 MAC 地址
        /// </summary>
        private string GetLocalMacAddress()
        {
            try
            {
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        var mac = nic.GetPhysicalAddress();
                        var bytes = mac.GetAddressBytes();
                        if (bytes.Length == 6)
                        {
                            return BitConverter.ToString(bytes);
                        }
                    }
                }
            }
            catch { }
            
            return "00-00-00-00-00-00";
        }
        
        /// <summary>
        /// 获取 SLA Challenge
        /// </summary>
        public async Task<byte[]> GetChallengeAsync(CancellationToken ct = default)
        {
            _log("[Realme] 获取 SLA Challenge...");
            
            try
            {
                // 发送 0xB4 命令获取 challenge
                await _bromClient.WriteBytesAsync(new byte[] { 0xB4 }, ct);
                
                // 读取 16 字节 challenge
                var challenge = await _bromClient.ReadBytesAsync(16, 5000, ct);
                if (challenge != null && challenge.Length == 16)
                {
                    _log($"[Realme] ✓ Challenge: {BitConverter.ToString(challenge).Replace("-", "").Substring(0, 16)}...");
                    return challenge;
                }
                
                _log("[Realme] 获取 Challenge 失败");
                return null;
            }
            catch (Exception ex)
            {
                _log($"[Realme] Challenge 异常: {ex.Message}");
                return null;
            }
        }
        
        #endregion
        
        #region 云端签名 API
        
        /// <summary>
        /// 调用云端 API 获取签名
        /// </summary>
        public async Task<RealmSignResponse> RequestSignatureAsync(
            RealmSignRequest request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(ApiUrl))
            {
                return new RealmSignResponse
                {
                    Code = "ERROR",
                    ErrorMessage = "API URL 未配置"
                };
            }
            
            _log("[Realme] ═══════════════════════════════════════");
            _log("[Realme] 🔔 REALME OPLUS SIGN INFO 🔔");
            _log("[Realme] ═══════════════════════════════════════");
            
            try
            {
                // 构建请求 - 根据 API 类型选择格式
                string jsonBody;
                if (ApiUrl.Contains("gsmfuture.in"))
                {
                    jsonBody = request.ToGsmFutureJson();
                    _log("[Realme] 使用 GSMFuture API 格式");
                }
                else if (ApiUrl.Contains("oplus") || ApiUrl.Contains("realme") || ApiUrl.Contains("oppo"))
                {
                    jsonBody = request.ToOfficialJson();
                    _log("[Realme] 使用官方 API 格式");
                }
                else
                {
                    jsonBody = request.ToJson();
                }
                _logDetail($"[Realme] 请求: {jsonBody}");
                
                // 发送 HTTP 请求
                var httpRequest = (HttpWebRequest)WebRequest.Create(ApiUrl);
                httpRequest.Method = "POST";
                httpRequest.ContentType = "application/json";
                httpRequest.Timeout = HttpTimeoutMs;
                
                // 添加认证头
                if (!string.IsNullOrEmpty(ApiKey))
                {
                    httpRequest.Headers.Add("Authorization", $"Bearer {ApiKey}");
                }
                if (!string.IsNullOrEmpty(Account))
                {
                    httpRequest.Headers.Add("X-Account", Account);
                }
                
                // 写入请求体
                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                httpRequest.ContentLength = bodyBytes.Length;
                
                using (var requestStream = await httpRequest.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(bodyBytes, 0, bodyBytes.Length, ct);
                }
                
                // 读取响应
                using (var response = (HttpWebResponse)await httpRequest.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string responseJson = await reader.ReadToEndAsync();
                    _logDetail($"[Realme] 响应: {responseJson}");
                    
                    // 解析响应
                    var result = ParseResponse(responseJson);
                    LastResponse = result;
                    
                    // 输出结果
                    LogSignResult(result, request);
                    
                    return result;
                }
            }
            catch (WebException webEx)
            {
                string errorMsg = webEx.Message;
                if (webEx.Response != null)
                {
                    using (var reader = new StreamReader(webEx.Response.GetResponseStream()))
                    {
                        errorMsg = reader.ReadToEnd();
                    }
                }
                
                _log($"[Realme] ❌ API 请求失败: {errorMsg}");
                
                return new RealmSignResponse
                {
                    Code = "HTTP_ERROR",
                    ErrorMessage = errorMsg
                };
            }
            catch (Exception ex)
            {
                _log($"[Realme] ❌ 签名异常: {ex.Message}");
                
                return new RealmSignResponse
                {
                    Code = "ERROR",
                    ErrorMessage = ex.Message
                };
            }
        }
        
        /// <summary>
        /// 解析 API 响应
        /// </summary>
        private RealmSignResponse ParseResponse(string json)
        {
            var response = new RealmSignResponse();
            
            try
            {
                // 解析 code
                response.Code = ExtractJsonValue(json, "code");
                
                // 解析 msg
                response.Message = ExtractJsonValue(json, "msg");
                
                // 解析 signedDataStr
                response.SignedDataStr = ExtractJsonValue(json, "signedDataStr");
                
                // 解析 isAllowDegraded
                string degraded = ExtractJsonValue(json, "isAllowDegraded");
                response.IsAllowDegraded = degraded?.ToLower() == "true";
                
                // Base64 解码签名
                if (!string.IsNullOrEmpty(response.SignedDataStr))
                {
                    try
                    {
                        response.SignatureData = Convert.FromBase64String(response.SignedDataStr);
                    }
                    catch
                    {
                        _log("[Realme] ⚠ Base64 解码失败");
                    }
                }
                
                // 解析 credit (如果有)
                string credit = ExtractJsonValue(json, "credit");
                if (int.TryParse(credit, out int creditValue))
                {
                    response.Credit = creditValue;
                }
            }
            catch (Exception ex)
            {
                response.Code = "PARSE_ERROR";
                response.ErrorMessage = ex.Message;
            }
            
            return response;
        }
        
        /// <summary>
        /// 从 JSON 中提取值 (简单实现，不依赖第三方库)
        /// </summary>
        private string ExtractJsonValue(string json, string key)
        {
            // 尝试 "key":"value" 格式
            string pattern1 = $"\"{key}\":\"";
            int idx = json.IndexOf(pattern1);
            if (idx >= 0)
            {
                int start = idx + pattern1.Length;
                int end = json.IndexOf("\"", start);
                if (end > start)
                {
                    return json.Substring(start, end - start);
                }
            }
            
            // 尝试 "key":value 格式 (数字或布尔)
            string pattern2 = $"\"{key}\":";
            idx = json.IndexOf(pattern2);
            if (idx >= 0)
            {
                int start = idx + pattern2.Length;
                // 跳过空格
                while (start < json.Length && json[start] == ' ') start++;
                
                int end = start;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ' ')
                {
                    end++;
                }
                
                if (end > start)
                {
                    return json.Substring(start, end - start).Trim();
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 输出签名结果日志
        /// </summary>
        private void LogSignResult(RealmSignResponse result, RealmSignRequest request)
        {
            if (result.Success)
            {
                _log($"[Realme] ✅ 状态: 成功");
            }
            else
            {
                _log($"[Realme] ❌ 状态: 失败 ({result.Code})");
            }
            
            _log($"[Realme] 📱 平台: {request.Platform}");
            _log($"[Realme] 🔧 芯片: {request.Chipset}");
            
            if (!string.IsNullOrEmpty(request.SerialNumber))
            {
                string sn = request.SerialNumber;
                if (sn.Length > 8)
                {
                    sn = sn.Substring(0, 8) + "...";
                }
                _log($"[Realme] 🔢 序列号: {sn}");
            }
            
            if (!string.IsNullOrEmpty(Account))
            {
                _log($"[Realme] 👤 账号: {Account}");
            }
            
            _log($"[Realme] 🖥️ 服务器: {ServerType}");
            
            if (result.Credit > 0)
            {
                _log($"[Realme] 💰 额度: {result.Credit}");
            }
            
            _log($"[Realme] 📅 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            if (result.SignatureData != null)
            {
                _log($"[Realme] 📨 签名: {result.SignatureData.Length} 字节");
            }
            
            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                _log($"[Realme] ⚠ 错误: {result.ErrorMessage}");
            }
        }
        
        #endregion
        
        #region 写入签名
        
        /// <summary>
        /// 写入签名数据到设备
        /// </summary>
        public async Task<bool> WriteSignatureAsync(byte[] signatureData, CancellationToken ct = default)
        {
            if (signatureData == null || signatureData.Length == 0)
            {
                _log("[Realme] 签名数据为空");
                return false;
            }
            
            _log($"[Realme] 写入签名数据... ({signatureData.Length} 字节)");
            
            if (_xmlClient == null)
            {
                _log("[Realme] XML DA 未初始化");
                return false;
            }
            
            try
            {
                // 通过 XML DA 发送签名
                bool result = await _xmlClient.WriteSignatureDataAsync(signatureData, ct);
                
                if (result)
                {
                    _log("[Realme] ✓ 写入签名数据... 完成");
                }
                else
                {
                    _log("[Realme] 写入签名数据... 失败");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _log($"[Realme] 写入签名异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 发送 SLA 响应 (BROM 层认证)
        /// </summary>
        public async Task<bool> SendSlaResponseAsync(byte[] signature, CancellationToken ct = default)
        {
            if (signature == null || signature.Length == 0)
            {
                _log("[Realme] SLA 签名为空");
                return false;
            }
            
            _log($"[Realme] 发送 SLA 响应... ({signature.Length} bytes)");
            
            try
            {
                // 发送 0xB5 命令 + 签名
                await _bromClient.WriteBytesAsync(new byte[] { 0xB5 }, ct);
                await _bromClient.WriteBytesAsync(signature, ct);
                
                // 读取状态
                var result = await _bromClient.ReadBytesAsync(2, 5000, ct);
                if (result != null && result.Length >= 2)
                {
                    ushort status = (ushort)(result[0] << 8 | result[1]);
                    if (status == 0)
                    {
                        _log("[Realme] ✓ SLA 验证成功");
                        return true;
                    }
                    
                    _log($"[Realme] SLA 验证失败: 0x{status:X4}");
                    return false;
                }
                
                _log("[Realme] 读取 SLA 状态失败");
                return false;
            }
            catch (Exception ex)
            {
                _log($"[Realme] SLA 响应异常: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region 检查状态
        
        /// <summary>
        /// 检查 DA-SLA 状态
        /// </summary>
        public async Task<string> CheckDaSlaStatusAsync(CancellationToken ct = default)
        {
            _log("[Realme] 检查 DA-SLA 状态...");
            
            if (_xmlClient == null)
            {
                return "NOT_CONNECTED";
            }
            
            try
            {
                string status = await _xmlClient.CheckDaSlaStatusAsync(ct);
                _log($"[Realme] DA-SLA 状态: {status}");
                return status;
            }
            catch (Exception ex)
            {
                _log($"[Realme] 检查状态异常: {ex.Message}");
                return "ERROR";
            }
        }
        
        #endregion
        
        #region 完整签名流程
        
        /// <summary>
        /// Realme/OPPO/Xiaomi 完整签名流程 (推荐)
        /// 
        /// 流程 (与 Xiaomi 相同):
        /// 1. Send DA (DA1 + DA2)
        /// 2. Send sign file (发送签名文件)
        /// 3. Read auth data (读取 auth 数据)
        /// 4. Write signdata (写入签名数据)
        /// 
        /// </summary>
        /// <param name="signFile">签名文件数据</param>
        /// <param name="signData">签名数据 (从云端获取)</param>
        /// <param name="ct">取消令牌</param>
        public async Task<bool> ExecuteFullSignFlowAsync(
            byte[] signFile,
            byte[] signData,
            CancellationToken ct = default)
        {
            _log("[Realme] ═══════════════════════════════════════");
            _log("[Realme] 执行完整签名流程 (Xiaomi 模式)");
            _log("[Realme] ═══════════════════════════════════════");
            
            if (_xmlClient == null)
            {
                _log("[Realme] ❌ XML DA 未初始化");
                return false;
            }
            
            try
            {
                // Step 1: DA 已发送 (由调用者处理)
                _log("[Realme] Step 1: DA 已发送");
                
                // Step 2: 发送签名文件
                if (signFile != null && signFile.Length > 0)
                {
                    _log($"[Realme] Step 2: 发送签名文件... ({signFile.Length} 字节)");
                    bool sendOk = await _xmlClient.SendSignFileAsync(signFile, ct);
                    if (!sendOk)
                    {
                        _log("[Realme] ⚠ 发送签名文件失败，继续...");
                    }
                    else
                    {
                        _log("[Realme] ✓ 发送签名文件... 完成");
                    }
                }
                else
                {
                    _log("[Realme] Step 2: 跳过 (无签名文件)");
                }
                
                // Step 3: 读取 Auth 数据
                _log("[Realme] Step 3: 读取 Auth 数据...");
                byte[] authData = await _xmlClient.ReadAuthDataAsync(ct);
                if (authData != null)
                {
                    _log($"[Realme] ✓ 读取 Auth 数据: {authData.Length} 字节");
                    _logDetail($"[Realme] Auth: {BitConverter.ToString(authData).Replace("-", "").Substring(0, Math.Min(32, authData.Length * 2))}...");
                }
                else
                {
                    _log("[Realme] ⚠ 读取 Auth 数据失败，继续...");
                }
                
                // Step 4: 写入签名数据
                if (signData != null && signData.Length > 0)
                {
                    _log($"[Realme] Step 4: 写入签名数据... ({signData.Length} 字节)");
                    bool writeOk = await _xmlClient.WriteSignatureDataAsync(signData, ct);
                    if (writeOk)
                    {
                        _log("[Realme] ✓ 写入签名数据... 完成");
                    }
                    else
                    {
                        _log("[Realme] ❌ 写入签名数据失败");
                        return false;
                    }
                }
                else
                {
                    _log("[Realme] Step 4: 跳过 (无签名数据)");
                }
                
                // Step 5: 检查状态
                _log("[Realme] Step 5: 检查 DA-SLA 状态...");
                string status = await _xmlClient.CheckDaSlaStatusAsync(ct);
                _log($"[Realme] DA-SLA 状态: {status}");
                
                bool success = status == "ENABLED" || status == "1" || status == "TRUE" || 
                              status == "AUTHENTICATED" || status == "DISABLED" || status == "0";
                
                if (success)
                {
                    _log("[Realme] ═══════════════════════════════════════");
                    _log("[Realme] ✓ 签名流程完成");
                    _log("[Realme] ═══════════════════════════════════════");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _log($"[Realme] ❌ 签名流程异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 读取 Auth 数据 (用于云端签名请求)
        /// </summary>
        public async Task<byte[]> ReadAuthDataAsync(CancellationToken ct = default)
        {
            if (_xmlClient == null)
            {
                _log("[Realme] XML DA 未初始化");
                return null;
            }
            
            return await _xmlClient.ReadAuthDataAsync(ct);
        }
        
        /// <summary>
        /// 发送签名文件
        /// </summary>
        public async Task<bool> SendSignFileAsync(byte[] signFile, CancellationToken ct = default)
        {
            if (_xmlClient == null)
            {
                _log("[Realme] XML DA 未初始化");
                return false;
            }
            
            return await _xmlClient.SendSignFileAsync(signFile, ct);
        }
        
        /// <summary>
        /// 执行完整的 Realme 云端签名流程
        /// 
        /// 流程:
        /// 1. 获取设备信息
        /// 2. 调用云端 API 获取签名
        /// 3. 写入签名到设备
        /// 4. 检查 DA-SLA 状态
        /// </summary>
        public async Task<bool> ExecuteAuthAsync(CancellationToken ct = default)
        {
            _log("[Realme] ═══════════════════════════════════════");
            _log("[Realme] 执行 Realme 云端签名认证...");
            _log("[Realme] ═══════════════════════════════════════");
            
            // Step 1: 获取设备信息
            var request = GetSignRequest();
            if (request == null)
            {
                _log("[Realme] ❌ 无法获取设备信息");
                return false;
            }
            
            _log($"[Realme] 平台: {request.Platform}");
            _log($"[Realme] 芯片: {request.Chipset}");
            _log($"[Realme] 硬件代码: {request.HwCode}");
            
            // 获取 Challenge (如果需要)
            bool slaEnabled = _bromClient.TargetConfig.HasFlag(TargetConfigFlags.SlaEnabled);
            if (slaEnabled)
            {
                var challenge = await GetChallengeAsync(ct);
                if (challenge != null)
                {
                    request.Challenge = BitConverter.ToString(challenge).Replace("-", "");
                }
            }
            
            // Step 2: 调用云端 API
            var response = await RequestSignatureAsync(request, ct);
            if (!response.Success)
            {
                _log($"[Realme] ❌ 云端签名失败: {response.Message ?? response.ErrorMessage}");
                return false;
            }
            
            if (response.SignatureData == null || response.SignatureData.Length == 0)
            {
                _log("[Realme] ❌ 签名数据为空");
                return false;
            }
            
            _log($"[Realme] ✓ 获取签名成功: {response.SignatureData.Length} 字节");
            
            // Step 3: 写入签名
            bool writeOk = await WriteSignatureAsync(response.SignatureData, ct);
            if (!writeOk)
            {
                _log("[Realme] ❌ 签名写入失败");
                return false;
            }
            
            // Step 4: 检查状态
            string status = await CheckDaSlaStatusAsync(ct);
            bool enabled = status == "ENABLED" || status == "1" || status == "TRUE" || status == "AUTHENTICATED";
            
            if (enabled)
            {
                _log("[Realme] ✓ Realme 认证成功 - DA-SLA 已启用");
                return true;
            }
            else if (status == "DISABLED" || status == "0" || status == "FALSE")
            {
                _log("[Realme] ✓ DA-SLA 已禁用 (无需签名)");
                return true;
            }
            else
            {
                _log("[Realme] ⚠ DA-SLA 状态未知，继续...");
                return true; // 不阻止流程
            }
        }
        
        /// <summary>
        /// 使用已有签名数据执行认证 (云端 API 已调用)
        /// </summary>
        public async Task<bool> ExecuteAuthWithSignatureAsync(byte[] signatureData, CancellationToken ct = default)
        {
            _log("[Realme] ═══════════════════════════════════════");
            _log("[Realme] 使用预获取签名执行认证...");
            _log("[Realme] ═══════════════════════════════════════");
            
            if (signatureData == null || signatureData.Length == 0)
            {
                _log("[Realme] ❌ 签名数据为空");
                return false;
            }
            
            _log($"[Realme] 签名大小: {signatureData.Length} 字节");
            
            // 写入签名
            bool writeOk = await WriteSignatureAsync(signatureData, ct);
            if (!writeOk)
            {
                _log("[Realme] ❌ 签名写入失败");
                return false;
            }
            
            // 检查状态
            string status = await CheckDaSlaStatusAsync(ct);
            bool enabled = status == "ENABLED" || status == "1" || status == "TRUE" || status == "AUTHENTICATED";
            
            if (enabled)
            {
                _log("[Realme] ✓ 认证成功 - DA-SLA 已启用");
                return true;
            }
            else
            {
                _log($"[Realme] DA-SLA 状态: {status}");
                return true; // 不阻止流程
            }
        }
        
        #endregion
    }
}
