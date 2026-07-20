using NewLife;
using NewLife.Serialization;

namespace Stardust.Dns;

/// <summary>优刻得UDNS记录值项</summary>
public class UCloudDnsValueSet
{
    #region 属性
    /// <summary>记录值（IP地址）</summary>
    public String? Data { get; set; }

    /// <summary>权重，1-10</summary>
    public Int32 Weight { get; set; }

    /// <summary>是否启用，1启用 0禁用</summary>
    public Int32 IsEnabled { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字典解析</summary>
    /// <param name="dict"></param>
    /// <returns></returns>
    public static UCloudDnsValueSet? Parse(IDictionary<String, Object?>? dict)
    {
        if (dict == null) return null;

        dict.TryGetValue("Data", out var data);
        dict.TryGetValue("Weight", out var weight);
        dict.TryGetValue("IsEnabled", out var enabled);

        return new UCloudDnsValueSet
        {
            Data = data as String,
            Weight = (weight as Int32?) ?? 1,
            IsEnabled = (enabled as Int32?) ?? 1,
        };
    }

    /// <summary>格式化为API要求的字符串：Value|权重|是否启用</summary>
    public String ToApiString() => $"{Data}|{Weight}|{IsEnabled}";
    #endregion
}

/// <summary>优刻得UDNS记录信息</summary>
public class UCloudDnsRecordInfo
{
    #region 属性
    /// <summary>记录ID</summary>
    public String? RecordId { get; set; }

    /// <summary>主机记录</summary>
    public String? Name { get; set; }

    /// <summary>记录类型</summary>
    public String? Type { get; set; }

    /// <summary>数值组</summary>
    public UCloudDnsValueSet[]? ValueSet { get; set; }

    /// <summary>TTL，单位秒</summary>
    public Int32 TTL { get; set; }

    /// <summary>备注</summary>
    public String? Remark { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字典解析</summary>
    /// <param name="dict"></param>
    /// <returns></returns>
    public static UCloudDnsRecordInfo? Parse(IDictionary<String, Object?>? dict)
    {
        if (dict == null) return null;

        dict.TryGetValue("RecordId", out var recordId);
        dict.TryGetValue("Name", out var name);
        dict.TryGetValue("Type", out var type);
        dict.TryGetValue("TTL", out var ttl);
        dict.TryGetValue("Remark", out var remark);

        var ri = new UCloudDnsRecordInfo
        {
            RecordId = recordId as String,
            Name = name as String,
            Type = type as String,
            TTL = (ttl as Int32?) ?? 5,
            Remark = remark as String,
        };

        // 解析ValueSet
        if (dict.TryGetValue("ValueSet", out var valueSetObj) &&
            valueSetObj is IList<Object> valueSetList)
        {
            ri.ValueSet = valueSetList
                .Cast<IDictionary<String, Object?>>()
                .Select(UCloudDnsValueSet.Parse)
                .Where(e => e != null)
                .Cast<UCloudDnsValueSet>()
                .ToArray();
        }

        return ri;
    }

    /// <summary>获取第一个记录值</summary>
    public String? FirstValue => ValueSet?.FirstOrDefault()?.Data;
    #endregion
}

/// <summary>优刻得UDNS响应</summary>
public class UCloudDnsResponse
{
    #region 属性
    /// <summary>操作指令名称</summary>
    public String? Action { get; set; }

    /// <summary>返回状态码，0成功</summary>
    public Int32 RetCode { get; set; }

    /// <summary>错误消息</summary>
    public String? Message { get; set; }

    /// <summary>新增记录的ID（CreateUDNSRecord返回）</summary>
    public String? DNSRecordId { get; set; }

    /// <summary>记录列表（DescribeUDNSRecord返回）</summary>
    public UCloudDnsRecordInfo[]? RecordInfos { get; set; }

    /// <summary>总数量</summary>
    public Int32 TotalCount { get; set; }
    #endregion

    #region 方法
    /// <summary>从JSON字符串解析响应</summary>
    /// <param name="json">JSON字符串</param>
    /// <returns></returns>
    public static UCloudDnsResponse? Parse(String json)
    {
        if (String.IsNullOrEmpty(json)) return null;

        var dict = JsonParser.Decode(json) as IDictionary<String, Object?>;
        if (dict == null) return null;

        dict.TryGetValue("Action", out var action);
        dict.TryGetValue("RetCode", out var retCode);
        dict.TryGetValue("Message", out var message);
        dict.TryGetValue("DNSRecordId", out var dnsRecordId);
        dict.TryGetValue("TotalCount", out var totalCount);

        var rs = new UCloudDnsResponse
        {
            Action = action as String,
            RetCode = (retCode as Int32?) ?? -1,
            Message = message as String,
            DNSRecordId = dnsRecordId as String,
            TotalCount = (totalCount as Int32?) ?? 0,
        };

        // 解析记录列表
        if (dict.TryGetValue("RecordInfos", out var recordInfosObj) &&
            recordInfosObj is IList<Object> recordInfos)
        {
            rs.RecordInfos = recordInfos
                .Cast<IDictionary<String, Object?>>()
                .Select(UCloudDnsRecordInfo.Parse)
                .Where(e => e != null)
                .Cast<UCloudDnsRecordInfo>()
                .ToArray();
        }

        return rs;
    }

    /// <summary>是否成功</summary>
    public Boolean Success => RetCode == 0;
    #endregion
}
