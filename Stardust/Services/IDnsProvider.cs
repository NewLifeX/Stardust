namespace Stardust.Services;

/// <summary>DNS供应商接口。定义动态域名解析的统一操作</summary>
public interface IDnsProvider
{
    /// <summary>供应商类型</summary>
    String ProviderType { get; }

    /// <summary>更新DNS记录</summary>
    /// <param name="config">DNS供应商配置</param>
    /// <param name="domainName">完整域名，如 sh05.newlifex.com</param>
    /// <param name="recordValue">记录值（IP地址）</param>
    /// <param name="recordType">记录类型，A/AAAA</param>
    /// <returns>是否成功</returns>
    Task<Boolean> UpdateRecordAsync(IDnsConfig config, String domainName, String recordValue, String recordType = "A");
}
