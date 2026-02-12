using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;

namespace StarAgent;

/// <summary>阿里云DNS客户端。用于动态域名解析</summary>
public class AliyunDnsClient
{
    #region 属性
    /// <summary>AccessKeyId</summary>
    public String AccessKeyId { get; set; } = null!;

    /// <summary>AccessKeySecret</summary>
    public String AccessKeySecret { get; set; } = null!;

    /// <summary>域名。例如：example.com</summary>
    public String Domain { get; set; } = null!;

    /// <summary>记录。例如：www，表示www.example.com。@表示根域名</summary>
    public String Record { get; set; } = null!;

    /// <summary>记录类型。默认A记录</summary>
    public String RecordType { get; set; } = "A";

    /// <summary>API端点</summary>
    public String Endpoint { get; set; } = "https://alidns.aliyuncs.com/";

    /// <summary>性能追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = XTrace.Log;

    private HttpClient? _Client;
    private String? _lastIp;
    private String? _recordId;
    #endregion

    #region 构造
    /// <summary>实例化阿里云DNS客户端</summary>
    public AliyunDnsClient() { }

    /// <summary>实例化阿里云DNS客户端</summary>
    /// <param name="accessKeyId">AccessKeyId</param>
    /// <param name="accessKeySecret">AccessKeySecret</param>
    /// <param name="domain">域名</param>
    /// <param name="record">记录</param>
    public AliyunDnsClient(String accessKeyId, String accessKeySecret, String domain, String record)
    {
        AccessKeyId = accessKeyId;
        AccessKeySecret = accessKeySecret;
        Domain = domain;
        Record = record;
    }
    #endregion

    #region 方法
    /// <summary>更新DNS记录到指定IP地址</summary>
    /// <param name="ipAddress">IP地址。为空时自动获取公网IP</param>
    /// <returns>是否更新成功</returns>
    public async Task<Boolean> UpdateAsync(String? ipAddress = null)
    {
        try
        {
            // 获取公网IP
            if (ipAddress.IsNullOrEmpty())
            {
                ipAddress = await GetPublicIpAsync();
                if (ipAddress.IsNullOrEmpty())
                {
                    Log.Error("无法获取公网IP地址");
                    return false;
                }
            }

            // IP地址未变化，无需更新
            if (ipAddress == _lastIp && !_recordId.IsNullOrEmpty())
            {
                Log.Debug("IP地址未变化：{0}，无需更新", ipAddress);
                return true;
            }

            Log.Info("开始更新阿里云DNS记录：{0}.{1} => {2}", Record, Domain, ipAddress);

            // 查询记录ID
            if (_recordId.IsNullOrEmpty())
            {
                _recordId = await GetRecordIdAsync();
            }

            Boolean result;
            if (_recordId.IsNullOrEmpty())
            {
                // 记录不存在，添加新记录
                result = await AddRecordAsync(ipAddress);
            }
            else
            {
                // 记录存在，更新记录
                result = await UpdateRecordAsync(ipAddress);
            }

            if (result)
            {
                _lastIp = ipAddress;
                Log.Info("成功更新阿里云DNS记录：{0}.{1} => {2}", Record, Domain, ipAddress);
            }
            else
            {
                Log.Error("更新阿里云DNS记录失败：{0}.{1} => {2}", Record, Domain, ipAddress);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error("更新阿里云DNS记录异常：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>获取公网IP地址</summary>
    private async Task<String?> GetPublicIpAsync()
    {
        try
        {
            _Client ??= Tracer?.CreateHttpClient() ?? new HttpClient();

            // 使用多个公网IP查询服务，提高可用性
            var urls = new[]
            {
                "https://api.ipify.org",
                "https://ifconfig.me/ip",
                "https://icanhazip.com"
            };

            foreach (var url in urls)
            {
                try
                {
                    var ip = await _Client.GetStringAsync(url);
                    ip = ip?.Trim();
                    if (!ip.IsNullOrEmpty())
                    {
                        Log.Debug("获取公网IP地址：{0} from {1}", ip, url);
                        return ip;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("从 {0} 获取公网IP失败：{1}", url, ex.Message);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error("获取公网IP地址异常：{0}", ex.Message);
            return null;
        }
    }

    /// <summary>获取DNS记录ID</summary>
    private async Task<String?> GetRecordIdAsync()
    {
        try
        {
            var parameters = new Dictionary<String, String>
            {
                ["Action"] = "DescribeDomainRecords",
                ["DomainName"] = Domain,
                ["RRKeyWord"] = Record,
                ["TypeKeyWord"] = RecordType
            };

            var response = await CallApiAsync(parameters);
            if (response == null) return null;

            var json = JsonParser.Decode(response);
            if (json == null) return null;

            // 检查返回结果
            var records = json["DomainRecords"] as IDictionary<String, Object?>;
            if (records == null) return null;

            var recordList = records["Record"] as IList<Object>;
            if (recordList == null || recordList.Count == 0) return null;

            // 找到匹配的记录
            foreach (var item in recordList)
            {
                if (item is not IDictionary<String, Object?> record) continue;

                var rr = record["RR"] as String;
                var type = record["Type"] as String;
                if (rr == Record && type == RecordType)
                {
                    var recordId = record["RecordId"] as String;
                    Log.Debug("找到DNS记录ID：{0}", recordId);
                    return recordId;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error("获取DNS记录ID异常：{0}", ex.Message);
            return null;
        }
    }

    /// <summary>添加DNS记录</summary>
    private async Task<Boolean> AddRecordAsync(String ipAddress)
    {
        try
        {
            var parameters = new Dictionary<String, String>
            {
                ["Action"] = "AddDomainRecord",
                ["DomainName"] = Domain,
                ["RR"] = Record,
                ["Type"] = RecordType,
                ["Value"] = ipAddress
            };

            var response = await CallApiAsync(parameters);
            if (response == null) return false;

            var json = JsonParser.Decode(response);
            if (json == null) return false;

            // 获取新记录的ID
            if (json.TryGetValue("RecordId", out var recordId))
            {
                _recordId = recordId?.ToString();
                Log.Debug("添加DNS记录成功，记录ID：{0}", _recordId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error("添加DNS记录异常：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>更新DNS记录</summary>
    private async Task<Boolean> UpdateRecordAsync(String ipAddress)
    {
        try
        {
            var parameters = new Dictionary<String, String>
            {
                ["Action"] = "UpdateDomainRecord",
                ["RecordId"] = _recordId!,
                ["RR"] = Record,
                ["Type"] = RecordType,
                ["Value"] = ipAddress
            };

            var response = await CallApiAsync(parameters);
            if (response == null) return false;

            var json = JsonParser.Decode(response);
            return json != null;
        }
        catch (Exception ex)
        {
            Log.Error("更新DNS记录异常：{0}", ex.Message);
            return false;
        }
    }

    /// <summary>调用阿里云API</summary>
    private async Task<String?> CallApiAsync(Dictionary<String, String> parameters)
    {
        try
        {
            _Client ??= Tracer?.CreateHttpClient() ?? new HttpClient();

            // 添加公共参数
            parameters["Format"] = "JSON";
            parameters["Version"] = "2015-01-09";
            parameters["AccessKeyId"] = AccessKeyId;
            parameters["SignatureMethod"] = "HMAC-SHA1";
            parameters["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            parameters["SignatureVersion"] = "1.0";
            parameters["SignatureNonce"] = Guid.NewGuid().ToString();

            // 生成签名
            var signature = GenerateSignature(parameters);
            parameters["Signature"] = signature;

            // 构建查询字符串
            var queryString = String.Join("&", parameters.OrderBy(p => p.Key).Select(p => $"{UrlEncode(p.Key)}={UrlEncode(p.Value)}"));
            var url = $"{Endpoint}?{queryString}";

            // 发送请求
            var response = await _Client.GetStringAsync(url);
            return response;
        }
        catch (Exception ex)
        {
            Log.Error("调用阿里云API异常：{0}", ex.Message);
            return null;
        }
    }

    /// <summary>生成签名</summary>
    private String GenerateSignature(Dictionary<String, String> parameters)
    {
        // 按字母顺序排序
        var sortedParams = parameters.OrderBy(p => p.Key).ToList();

        // 构建待签名字符串
        var canonicalizedQueryString = String.Join("&", sortedParams.Select(p => $"{UrlEncode(p.Key)}={UrlEncode(p.Value)}"));
        var stringToSign = $"GET&{UrlEncode("/")}&{UrlEncode(canonicalizedQueryString)}";

        // 使用HMAC-SHA1计算签名
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes($"{AccessKeySecret}&"));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    /// <summary>URL编码</summary>
    private static String UrlEncode(String value)
    {
        if (value.IsNullOrEmpty()) return String.Empty;

        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                c == '-' || c == '_' || c == '.' || c == '~')
            {
                sb.Append(c);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(c.ToString());
                foreach (var b in bytes)
                {
                    sb.AppendFormat("%{0:X2}", b);
                }
            }
        }
        return sb.ToString();
    }
    #endregion
}
