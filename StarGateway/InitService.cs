using Stardust.Data;
using Stardust.Data.Gateway;

namespace StarGateway;

class InitService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 配置
        var set = NewLife.Setting.Current;
        if (set.IsNew)
        {
            set.DataPath = "../Data";
            set.Save();
        }

        // 初始化数据库（触发反向工程建表）
        _ = App.Meta.Count;
        _ = GatewayCluster.Meta.Count;
        _ = GatewayNode.Meta.Count;
        _ = GatewayRoute.Meta.Count;
        _ = GatewayCert.Meta.Count;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}