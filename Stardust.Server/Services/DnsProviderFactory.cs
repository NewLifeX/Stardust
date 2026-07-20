using NewLife;
using Stardust.Services;

namespace Stardust.Server.Services;

/// <summary>DNS供应商工厂。解析所有已注册的IDnsProvider，按类型查找</summary>
public class DnsProviderFactory
{
    private readonly IEnumerable<IDnsProvider> _providers;

    /// <summary>实例化DNS供应商工厂</summary>
    /// <param name="providers">所有已注册的DNS供应商实例</param>
    public DnsProviderFactory(IEnumerable<IDnsProvider> providers) => _providers = providers;

    /// <summary>获取DNS供应商</summary>
    /// <param name="providerType">供应商类型：Aliyun/TencentCloud/UCloud</param>
    /// <returns></returns>
    public IDnsProvider? GetProvider(String providerType)
    {
        if (providerType.IsNullOrEmpty()) return null;

        return _providers.FirstOrDefault(p => p.ProviderType.EqualIgnoreCase(providerType));
    }
}
