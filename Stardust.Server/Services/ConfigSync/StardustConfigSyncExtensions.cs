using Microsoft.Extensions.DependencyInjection.Extensions;
using NewLife;
using NewLife.Common;
using XCode.Configuration;

namespace Stardust.Server.Services.ConfigSync;

/// <summary>集群配置同步扩展</summary>
/// <remarks>
/// 注册后由 <see cref="ConfigSyncManager"/> 定时比较 Parameter 表与本机配置文件的时间，
/// 决定是否用 Parameter 表整份覆盖本机配置文件，并通过事件总线把变更通知其它实例。
/// </remarks>
public static class StardustConfigSyncExtensions
{
    /// <summary>添加集群配置同步</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">同步管理器配置回调，用于设置应用标识并注册参与同步的配置类型</param>
    /// <returns>服务集合，支持链式调用</returns>
    public static IServiceCollection AddConfigSync(this IServiceCollection services, Action<ConfigSyncManager> configure)
    {
        var manager = new ConfigSyncManager(SysConfig.Current.Name);
        configure?.Invoke(manager);

        // 未指定应用标识时取系统配置名称，与单机部署的默认值保持一致
        if (manager.AppId.IsNullOrEmpty()) manager.AppId = SysConfig.Current.Name;

        services.AddSingleton(manager);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, StardustConfigSyncHostedService>());

        return services;
    }
}
