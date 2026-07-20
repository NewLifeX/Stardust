using NewLife;
using NewLife.Serialization;

namespace Stardust.Dns;

/// <summary>腾讯云DNS记录信息</summary>
public class TencentCloudDnsRecordInfo
{
    #region 属性
    /// <summary>记录ID</summary>
    public UInt64? RecordId { get; set; }

    /// <summary>主机记录</summary>
    public String? Name { get; set; }

    /// <summary>记录类型</summary>
    public String? Type { get; set; }

    /// <summary>记录值</summary>
    public String? Value { get; set; }

    /// <summary>域名</summary>
    public String? Domain { get; set; }

    /// <summary>TTL</summary>
    public UInt64? TTL { get; set; }

    /// <summary>记录行</summary>
    public String? Line { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字典解析记录信息</summary>
    /// <param name="dict">JSON字典</param>
    /// <returns></returns>
    public static TencentCloudDnsRecordInfo? Parse(IDictionary<String, Object?>? dict)
    {
        if (dict == null) return null;

        dict.TryGetValue("RecordId", out var recordId);
        dict.TryGetValue("Name", out var name);
        dict.TryGetValue("Type", out var type);
        dict.TryGetValue("Value", out var value);
        dict.TryGetValue("Domain", out var domain);
        dict.TryGetValue("TTL", out var ttl);
        dict.TryGetValue("Line", out var line);

        return new TencentCloudDnsRecordInfo
        {
            RecordId = recordId as UInt64?,
            Name = name as String,
            Type = type as String,
            Value = value as String,
            Domain = domain as String,
            TTL = ttl as UInt64?,
            Line = line as String,
        };
    }
    #endregion
}

/// <summary>腾讯云DNS响应。DNSPod API调用的通用响应结构</summary>
public class TencentCloudDnsResponse
{
    #region 属性
    /// <summary>请求ID</summary>
    public String? RequestId { get; set; }

    /// <summary>记录列表（DescribeRecordList返回）</summary>
    public TencentCloudDnsRecordInfo[]? RecordList { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字符串解析响应</summary>
    /// <param name="json">JSON字符串</param>
    /// <returns></returns>
    public static TencentCloudDnsResponse? Parse(String json)
    {
        if (String.IsNullOrEmpty(json)) return null;

        var root = JsonParser.Decode(json) as IDictionary<String, Object?>;
        if (root == null) return null;

        root.TryGetValue("Response", out var responseObj);
        if (responseObj is not IDictionary<String, Object?> response) return null;

        response.TryGetValue("RequestId", out var requestId);

        var rs = new TencentCloudDnsResponse
        {
            RequestId = requestId as String,
        };

        // 解析记录列表
        if (response.TryGetValue("RecordList", out var recordListObj) &&
            recordListObj is IList<Object> recordList &&
            recordList.Count > 0)
        {
            rs.RecordList = recordList
                .Cast<IDictionary<String, Object?>>()
                .Select(TencentCloudDnsRecordInfo.Parse)
                .Where(e => e != null)
                .Cast<TencentCloudDnsRecordInfo>()
                .ToArray();
        }

        return rs;
    }
    #endregion
}
