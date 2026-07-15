using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Services;

namespace Stardust.Dns;

/// <summary>阿里云DNS供应商。通过阿里云DNS API实现动态域名解析</summary>
public class AliyunDnsProvider : IDnsProvider
{
    #region 属性
    /// <summary>供应商类型</summary>
    public String ProviderType => "Aliyun";

    /// <summary>API端点</summary>
    public String Endpoint { get; set; } = "https://alidns.aliyuncs.com/";

    /// <summary>API版本</summary>
    public String ApiVersion { get; set; } = "2015-01-09";

    /// <summary>日志</summary>
    public ILog Log { get; set; } = XTrace.Log;
    #endregion

    #region 构造
    /// <summary>实例化阿里云DNS供应商</summary>
    public AliyunDnsProvider() { }
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
            // 查询现有记录
            var recordId = await GetRecordIdAsync(config.AppKey!, config.AppSecret!, domain, record, recordType).ConfigureAwait(false);
            if (recordId == null)
                return await AddRecordAsync(config.AppKey!, config.AppSecret!, domain, record, recordType, recordValue).ConfigureAwait(false);
            else
                return await UpdateRecordByIdAsync(config.AppKey!, config.AppSecret!, recordId, record, recordType, recordValue).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error("阿里云DNS更新异常：{0}", ex.Message);
            return false;
        }
    }
    #endregion

    #region 方法
    /// <summary>解析完整域名为RR和主域名</summary>
    /// <param name="domainName">完整域名，如 sh05.newlifex.com</param>
    /// <returns>(RR, Domain)</returns>
    private static (String record, String domain) ParseDomain(String domainName)
    {
        var p = domainName.IndexOf('.');
        if (p < 0) return ("@", domainName);

        return (domainName[..p], domainName[(p + 1)..]);
    }

    /// <summary>获取DNS记录ID</summary>
    private async Task<String?> GetRecordIdAsync(String appKey, String appSecret, String domain, String record, String recordType)
    {
        var response = await RequestAsync(appKey, appSecret, new Dictionary<String, String>
        {
            ["Action"] = "DescribeDomainRecords",
            ["DomainName"] = domain,
            ["RRKeyWord"] = record,
            ["TypeKeyWord"] = recordType
        }).ConfigureAwait(false);

        var rs = AliyunDnsResponse.Parse(response);
        var ri = rs?.Records?.FirstOrDefault(e => e.RR == record && e.Type == recordType);
        return ri?.RecordId;
    }

    /// <summary>添加DNS记录</summary>
    private async Task<Boolean> AddRecordAsync(String appKey, String appSecret, String domain, String record, String recordType, String value)
    {
        var response = await RequestAsync(appKey, appSecret, new Dictionary<String, String>
        {
            ["Action"] = "AddDomainRecord",
            ["DomainName"] = domain,
            ["RR"] = record,
            ["Type"] = recordType,
            ["Value"] = value
        }).ConfigureAwait(false);

        var rs = AliyunDnsResponse.Parse(response);
        return rs != null && !String.IsNullOrEmpty(rs.RecordId);
    }

    /// <summary>更新DNS记录（根据记录ID）</summary>
    private async Task<Boolean> UpdateRecordByIdAsync(String appKey, String appSecret, String recordId, String record, String recordType, String value)
    {
        var response = await RequestAsync(appKey, appSecret, new Dictionary<String, String>
        {
            ["Action"] = "UpdateDomainRecord",
            ["RecordId"] = recordId,
            ["RR"] = record,
            ["Type"] = recordType,
            ["Value"] = value
        }).ConfigureAwait(false);

        return AliyunDnsResponse.Parse(response) != null;
    }

    /// <summary>调用阿里云API</summary>
    private async Task<String?> RequestAsync(String appKey, String appSecret, Dictionary<String, String> parameters)
    {
        using var client = new HttpClient();

        parameters["Format"] = "JSON";
        parameters["Version"] = ApiVersion;
        parameters["AccessKeyId"] = appKey;
        parameters["SignatureMethod"] = "HMAC-SHA1";
        parameters["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        parameters["SignatureVersion"] = "1.0";
        parameters["SignatureNonce"] = Guid.NewGuid().ToString();

        var signature = GenerateSignature(appSecret, parameters);
        parameters["Signature"] = signature;

        var queryString = String.Join("&", parameters.OrderBy(p => p.Key).Select(p => $"{UrlEncode(p.Key)}={UrlEncode(p.Value)}"));
        var url = $"{Endpoint}?{queryString}";

        var response = await client.GetStringAsync(url).ConfigureAwait(false);
        return response;
    }

    /// <summary>生成阿里云API签名</summary>
    private static String GenerateSignature(String appSecret, Dictionary<String, String> parameters)
    {
        var sortedParams = parameters.OrderBy(p => p.Key);
        var queryString = String.Join("&", sortedParams.Select(p => $"{UrlEncode(p.Key)}={UrlEncode(p.Value)}"));

        var stringToSign = $"GET&{UrlEncode("/")}&{UrlEncode(queryString)}";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes($"{appSecret}&"));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }

    /// <summary>URL编码</summary>
    private static String UrlEncode(String value)
    {
        if (value.IsNullOrEmpty()) return String.Empty;

        return Uri.EscapeDataString(value)
            .Replace("+", "%2B")
            .Replace("*", "%2A")
            .Replace("%7E", "~");
    }
    #endregion
}
