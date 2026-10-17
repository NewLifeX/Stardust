using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>Apollo 配置集成服务单元测试。覆盖定时器生命周期与无启用应用时的同步空跑</summary>
public class ApolloServiceTests
{
    [Fact(DisplayName = "启动后停止不抛异常")]
    public async Task Start_Then_Stop()
    {
        var service = new ApolloService();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact(DisplayName = "未启动直接停止不抛异常")]
    public async Task Stop_Without_Start()
    {
        var service = new ApolloService();

        await service.StopAsync(CancellationToken.None);
    }

    [Fact(DisplayName = "没有启用Apollo的应用时同步空跑不抛异常")]
    public void DoApolloSync_NoEnabledApp_DoesNothing()
    {
        var service = new ApolloService();
        var method = typeof(ApolloService).GetMethod("DoApolloSync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var ex = Record.Exception(() => method.Invoke(service, [null]));

        Assert.Null(ex);
    }
}
