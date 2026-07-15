using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Data;
using Stardust.Services;

namespace Stardust.Dns;

/// <summary>优刻得UDNS供应商。通过优刻得UDNS API实现动态域名解析</summary>
/// <remarks>
/// UCloud UDNS 使用资源ID（DNSZoneId）而非域名来标识DNS Zone，
/// 因此需在 DomainProvider 中配置：
///   Domain   — 托管域名，如 newlifex.com（凭据匹配用）
///   Endpoint — API端点，默认 https://api.ucloud.cn
///   DNSZoneId — DNS Zone ID（必填，通过 IExtend 弱类型获取）
///   Region   — 地域，默认 cn-bj2（通过 IExtend 弱类型获取）
/// DNSZoneId 和 Region 不在 IDnsConfig 接口中，属于 UCloud 特有配置，
/// 通过 IExtend.Items 从实体类 DomainProvider 的对应属性获取。
/// </remarks>
public class UCloudDnsProvider : IDnsProvider
{
    #region 属性
    /// <summary>供应商类型</summary>
    public String ProviderType => "UCloud";

    /// <summary>默认API端点</summary>
    public String DefaultEndpoint { get; set; } = "https://api.ucloud.cn";

    /// <summary>默认地域</summary>
    public String DefaultRegion { get; set; } = "cn-bj2";

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

        // 通过 IExtend 弱类型获取 UCloud 特有配置（DNSZoneId、Region）
        String? dnsZoneId = null, region = null;
        if (config is IExtend ext)
        {
            if (ext["DNSZoneId"] is String zoneId) dnsZoneId = zoneId;
            if (ext["Region"] is String r) region = r;
        }
        var endpoint = config.Endpoint;

        if (dnsZoneId.IsNullOrEmpty()) return false;
        if (region.IsNullOrEmpty()) region = DefaultRegion;
        if (endpoint.IsNullOrEmpty()) endpoint = DefaultEndpoint;

        // 从完整域名中提取主机记录（如 sh05.newlifex.com → sh05）
        var record = domainName;
        var p = domainName.IndexOf('.');
        if (p > 0) record = domainName[..p];

        try
        {
            var recordId = await GetRecordIdAsync(config.AppKey!, config.AppSecret!, dnsZoneId, record, recordType, region, endpoint).ConfigureAwait(false);
            if (recordId == null)
                return await CreateRecordAsync(config.AppKey!, config.AppSecret!, dnsZoneId, record, recordType, recordValue, region, endpoint).ConfigureAwait(false);
            else
                return await ModifyRecordAsync(config.AppKey!, config.AppSecret!, dnsZoneId, recordId, record, recordType, recordValue, region, endpoint).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error("UCloud DNS更新异常：{0}", ex.Message);
            return false;
        }
    }
    #endregion

    #region 方法
    /// <summary>获取DNS记录ID</summary>
    private async Task<String?> GetRecordIdAsync(String appKey, String appSecret, String dnsZoneId, String record, String recordType, String region, String endpoint)
    {
        var parameters = new Dictionary<String, String>
        {
            ["Action"] = "DescribeUDNSRecord",
            ["Region"] = region,
            ["DNSZoneId"] = dnsZoneId,
            ["Limit"] = "100"
        };

        var response = await RequestAsync(appKey, appSecret, parameters, endpoint).ConfigureAwait(false);
        var rs = UCloudDnsResponse.Parse(response);

        return rs?.RecordInfos?.FirstOrDefault(e => e.Name == record && e.Type == recordType)?.RecordId;
    }

    /// <summary>创建DNS记录</summary>
    private async Task<Boolean> CreateRecordAsync(String appKey, String appSecret, String dnsZoneId, String record, String recordType, String value, String region, String endpoint)
    {
        var parameters = new Dictionary<String, String>
        {
            ["Action"] = "CreateUDNSRecord",
            ["Region"] = region,
            ["DNSZoneId"] = dnsZoneId,
            ["Name"] = record,
            ["Type"] = recordType,
            ["Value"] = $"{value}|1|1",
            ["ValueType"] = "Normal",
            ["TTL"] = "60"
        };

        var response = await RequestAsync(appKey, appSecret, parameters, endpoint).ConfigureAwait(false);
        var rs = UCloudDnsResponse.Parse(response);

        if (rs == null || !rs.Success)
        {
            Log.Error("UCloud创建记录失败：{0}", rs?.Message);
            return false;
        }

        return true;
    }

    /// <summary>修改DNS记录</summary>
    private async Task<Boolean> ModifyRecordAsync(String appKey, String appSecret, String dnsZoneId, String recordId, String record, String recordType, String value, String region, String endpoint)
    {
        var parameters = new Dictionary<String, String>
        {
            ["Action"] = "ModifyUDNSRecord",
            ["Region"] = region,
            ["DNSZoneId"] = dnsZoneId,
            ["RecordId"] = recordId,
            ["Type"] = recordType,
            ["Value"] = $"{value}|1|1",
            ["ValueType"] = "Normal",
            ["TTL"] = "60"
        };

        var response = await RequestAsync(appKey, appSecret, parameters, endpoint).ConfigureAwait(false);
        var rs = UCloudDnsResponse.Parse(response);

        if (rs == null || !rs.Success)
        {
            Log.Error("UCloud修改记录失败：{0}", rs?.Message);
            return false;
        }

        return true;
    }

    /// <summary>调用UCloud API</summary>
    private async Task<String?> RequestAsync(String appKey, String appSecret, Dictionary<String, String> parameters, String endpoint)
    {
        using var client = new HttpClient();

        // 添加公共参数
        parameters["PublicKey"] = appKey;

        // UCloud签名算法：
        // 1. 参数按名称升序排列
        // 2. 拼接为 Key1Value1Key2Value2...（无分隔符，无URL编码）
        // 3. 末尾拼接 PrivateKey
        // 4. SHA1 哈希
        var signature = GenerateSignature(appSecret, parameters);
        parameters["Signature"] = signature;

        // 构建查询字符串（需要URL编码）
        var queryString = String.Join("&", parameters
            .OrderBy(p => p.Key)
            .Select(p => $"{UrlEncode(p.Key)}={UrlEncode(p.Value)}"));

        var url = $"{endpoint}?{queryString}";

        try
        {
            var response = await client.GetStringAsync(url).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            Log.Error("UCloud API调用异常：{0}", ex.Message);
            return null;
        }
    }

    /// <summary>生成UCloud API签名</summary>
    private static String GenerateSignature(String appSecret, Dictionary<String, String> parameters)
    {
        // 1. 参数按名称升序排列
        var sortedParams = parameters.OrderBy(p => p.Key, StringComparer.Ordinal);

        // 2. 拼接为 Key1Value1Key2Value2...
        var sb = new StringBuilder();
        foreach (var kv in sortedParams)
        {
            sb.Append(kv.Key);
            sb.Append(kv.Value);
        }

        // 3. 末尾拼接 PrivateKey
        sb.Append(appSecret);

        // 4. SHA1 哈希
        var stringToSign = sb.ToString();
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
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
