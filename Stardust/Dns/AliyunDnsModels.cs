using NewLife.Serialization;

namespace Stardust.Dns;

/// <summary>阿里云DNS记录信息</summary>
public class AliyunDnsRecordInfo
{
    #region 属性
    /// <summary>记录ID</summary>
    public String? RecordId { get; set; }

    /// <summary>主机记录</summary>
    public String? RR { get; set; }

    /// <summary>记录类型</summary>
    public String? Type { get; set; }

    /// <summary>记录值</summary>
    public String? Value { get; set; }

    /// <summary>域名</summary>
    public String? DomainName { get; set; }

    /// <summary>TTL</summary>
    public Int64? TTL { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字典解析记录信息</summary>
    /// <param name="dict">JSON字典</param>
    /// <returns></returns>
    public static AliyunDnsRecordInfo? Parse(IDictionary<String, Object?>? dict)
    {
        if (dict == null) return null;

        dict.TryGetValue("RecordId", out var recordId);
        dict.TryGetValue("RR", out var rr);
        dict.TryGetValue("Type", out var type);
        dict.TryGetValue("Value", out var value);
        dict.TryGetValue("DomainName", out var domainName);
        dict.TryGetValue("TTL", out var ttl);

        return new AliyunDnsRecordInfo
        {
            RecordId = recordId as String,
            RR = rr as String,
            Type = type as String,
            Value = value as String,
            DomainName = domainName as String,
            TTL = ttl as Int64?,
        };
    }
    #endregion
}

/// <summary>阿里云DNS响应。API调用的通用响应结构</summary>
public class AliyunDnsResponse
{
    #region 属性
    /// <summary>请求ID</summary>
    public String? RequestId { get; set; }

    /// <summary>新增记录的ID</summary>
    public String? RecordId { get; set; }

    /// <summary>域名记录列表（DescribeDomainRecords返回）</summary>
    public AliyunDnsRecordInfo[]? Records { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字符串解析响应</summary>
    /// <param name="json">JSON字符串</param>
    /// <returns></returns>
    public static AliyunDnsResponse? Parse(String json)
    {
        if (String.IsNullOrEmpty(json)) return null;

        var dict = JsonParser.Decode(json) as IDictionary<String, Object?>;
        if (dict == null) return null;

        dict.TryGetValue("RequestId", out var requestId);
        dict.TryGetValue("RecordId", out var recordId);

        var rs = new AliyunDnsResponse
        {
            RequestId = requestId as String,
            RecordId = recordId as String,
        };

        // 解析记录列表
        if (dict.TryGetValue("DomainRecords", out var domainRecordsObj) &&
            domainRecordsObj is IDictionary<String, Object?> domainRecords &&
            domainRecords.TryGetValue("Record", out var recordListObj) &&
            recordListObj is IList<Object> recordList &&
            recordList.Count > 0)
        {
            rs.Records = recordList
                .Cast<IDictionary<String, Object?>>()
                .Select(AliyunDnsRecordInfo.Parse)
                .Where(e => e != null)
                .Cast<AliyunDnsRecordInfo>()
                .ToArray();
        }

        return rs;
    }

    /// <summary>判断是否有错误</summary>
    public Boolean HasError => String.IsNullOrEmpty(RequestId) && String.IsNullOrEmpty(RecordId) && (Records == null || Records.Length == 0);
    #endregion
}
