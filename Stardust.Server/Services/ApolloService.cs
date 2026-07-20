using NewLife;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Configs;
using XCode;

namespace Stardust.Server.Services;

/// <summary>Apollo 配置集成服务。定时从 Apollo 配置中心同步配置到本地 StarServer</summary>
public class ApolloService : IHostedService
{
    private TimerX _timer;

    /// <summary>启动服务，初始化定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(DoApolloSync, null, 60_000, 300_000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>停止服务，销毁定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    /// <summary>执行 Apollo 配置同步。遍历已启用 Apollo 的应用配置，拉取并更新本地配置</summary>
    /// <param name="state">定时器状态参数</param>
    private void DoApolloSync(Object state)
    {
        foreach (var item in AppConfig.FindAllWithCache())
        {
            if (!item.Enable || !item.EnableApollo || item.ApolloMetaServer.IsNullOrEmpty()) continue;

            var httpConfig = new ApolloConfigProvider
            {
                Server = item.ApolloMetaServer,
                AppId = item.ApolloAppId
            };

            var nameSpace = item.ApolloNameSpace;
            if (nameSpace.IsNullOrEmpty()) nameSpace = "application";
            httpConfig.SetApollo(nameSpace);

            try
            {
                // 一次性加载所有配置
                httpConfig.LoadAll();

                if (httpConfig.Keys.Count > 0)
                {
                    // 配置匹配到本地
                    var ds = ConfigData.FindAllByApp(item.Id);
                    foreach (var elm in httpConfig.Keys)
                    {
                        var cfg = ds.FirstOrDefault(e => e.Key.EqualIgnoreCase(elm));
                        if (cfg == null)
                        {
                            cfg = new ConfigData { ConfigId = item.Id, Key = elm, Enable = true, };
                            ds.Add(cfg);
                        }

                        cfg.Value = httpConfig[elm];

                        if (cfg is IEntity entity && entity.HasDirty) cfg.Version = item.AcquireNewVersion();
                    }
                    ds.Save(true);
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }
}