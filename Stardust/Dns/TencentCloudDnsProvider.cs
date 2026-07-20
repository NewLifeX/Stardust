using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Services;

namespace Stardust.Dns;

/// <summary>腾讯云DNS供应商。通过腾讯云DNSPod API实现动态域名解析</summary>
public class TencentCloudDnsProvider : IDnsProvider
{
    #region 属性
    /// <summary>供应商类型</summary>
    public String ProviderType => "TencentCloud";

    /// <summary>API端点</summary>
    public String Endpoint { get; set; } = "dnspod.tencentcloudapi.com";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = XTrace.Log;
    #endregion

    #region IDnsProvider 成员
    /// <summary>更新DNS记录。自动添加或更新</summary>
    /// <param name="config">DNS供应商配置</param>
    /// <param name="domainName">完整域名，如 sh05.newlifex.com</param>
    /// <param name="recordValue">IP地址</param>
    /// <param name="recordType">记录类型，默认A</param>
    /// <returns>是否成功</returns>
    public async Task<Boolean> UpdateRecordAsync(IDnsConfig config, String domainName, String recordValue, String recordType = "A")
    {
        if (config.AppKey.IsNullOrEmpty() || config.AppSecret.IsNullOrEmpty()) return false;

        // 支持自定义端点
        if (!config.Endpoint.IsNullOrEmpty()) Endpoint = config.Endpoint;

        var (record, domain) = ParseDomain(domainName);

        try
        {
            var recordId = await GetRecordIdAsync(config.AppKey!, config.AppSecret!, domain, record, recordType).ConfigureAwait(false);
            if (recordId == null)
                return await CreateRecordAsync(config.AppKey!, config.AppSecret!, domain, record, recordType, recordValue).ConfigureAwait(false);
            else
                return await ModifyRecordAsync(config.AppKey!, config.AppSecret!, domain, record, recordType, recordValue, recordId.Value).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error("腾讯云DNS更新异常：{0}", ex.Message);
            return false;
        }
    }
    #endregion

    #region 方法
    /// <summary>解析完整域名为RR和主域名</summary>
    private static (String record, String domain) ParseDomain(String domainName)
    {
        var p = domainName.IndexOf('.');
        if (p < 0) return ("@", domainName);

        return (domainName[..p], domainName[(p + 1)..]);
    }

    /// <summary>获取DNS记录ID</summary>
    private async Task<UInt64?> GetRecordIdAsync(String appKey, String appSecret, String domain, String record, String recordType)
    {
        var response = await RequestAsync(appKey, appSecret, "DescribeRecordList", new Dictionary<String, Object?>
        {
            ["Domain"] = domain,
            ["Subdomain"] = record,
            ["RecordType"] = recordType,
            ["Limit"] = 100
        }).ConfigureAwait(false);

        var rs = TencentCloudDnsResponse.Parse(response);
        return rs?.RecordList?.FirstOrDefault(e => e.Name == record && e.Type == recordType)?.RecordId;
    }

    /// <summary>创建DNS记录</summary>
    private async Task<Boolean> CreateRecordAsync(String appKey, String appSecret, String domain, String record, String recordType, String value)
    {
        var response = await RequestAsync(appKey, appSecret, "CreateRecord", new Dictionary<String, Object?>
        {
            ["Domain"] = domain,
            ["SubDomain"] = record,
            ["RecordType"] = recordType,
            ["RecordLine"] = "默认",
            ["Value"] = value
        }).ConfigureAwait(false);

        return TencentCloudDnsResponse.Parse(response) != null;
    }

    /// <summary>修改DNS记录</summary>
    private async Task<Boolean> ModifyRecordAsync(String appKey, String appSecret, String domain, String record, String recordType, String value, UInt64 recordId)
    {
        var response = await RequestAsync(appKey, appSecret, "ModifyRecord", new Dictionary<String, Object?>
        {
            ["Domain"] = domain,
            ["SubDomain"] = record,
            ["RecordType"] = recordType,
            ["RecordLine"] = "默认",
            ["Value"] = value,
            ["RecordId"] = recordId
        }).ConfigureAwait(false);

        return TencentCloudDnsResponse.Parse(response) != null;
    }

    /// <summary>调用腾讯云API（TC3-HMAC-SHA256签名）</summary>
    private async Task<String?> RequestAsync(String appKey, String appSecret, String action, Dictionary<String, Object?> payload)
    {
        using var client = new HttpClient();

        var now = DateTime.UtcNow;
        var timestamp = ((Int64)(now - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        var date = now.ToString("yyyy-MM-dd");
        var service = "dnspod";

        // 构建规范请求
        var httpRequestMethod = "POST";
        var canonicalUri = "/";
        var canonicalQueryString = "";
        var contentType = "application/json; charset=utf-8";

        var jsonPayload = payload.ToJson(false, false, false);
        var canonicalHeaders = $"content-type:{contentType}\nhost:{Endpoint}\nx-tc-action:{action.ToLower()}\n";
        var signedHeaders = "content-type;host;x-tc-action";

        var hashedRequestPayload = SHA256Hash(jsonPayload);
        var canonicalRequest = $"{httpRequestMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedRequestPayload}";

        // 构建待签名字符串
        var algorithm = "TC3-HMAC-SHA256";
        var credentialScope = $"{date}/{service}/tc3_request";
        var hashedCanonicalRequest = SHA256Hash(canonicalRequest);
        var stringToSign = $"{algorithm}\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}";

        // 计算签名
        var secretDate = HmacSHA256(date, $"TC3{appSecret}");
        var secretService = HmacSHA256(service, secretDate);
        var secretSigning = HmacSHA256("tc3_request", secretService);
        var signature = HmacSHA256Hex(stringToSign, secretSigning);

        // 构建 Authorization
        var authorization = $"{algorithm} Credential={appKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        // 发送请求
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Endpoint}")
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("X-TC-Action", action);
        request.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-TC-Version", "2021-03-23");

        var response = await client.SendAsync(request).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("腾讯云API调用失败：{0} {1}", response.StatusCode, responseBody);
            return null;
        }

        return responseBody;
    }

    private static String SHA256Hash(String value)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    private static Byte[] HmacSHA256(String value, String key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static Byte[] HmacSHA256(String value, Byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static String HmacSHA256Hex(String value, Byte[] key)
    {
        var bytes = HmacSHA256(value, key);
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
    #endregion
}
