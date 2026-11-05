using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using ServerTest.Fixtures;
using Stardust;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Managers;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Communication;

/// <summary>
/// 节点指令基建验收：真 StarServer + 真 StarClient + ServiceManager.Attach（与 StarAgent 进程相同的注册路径）。
/// 平台「未配置日志目录、不下发指令」由 Stardust.WebApiTests 的 DeploymentDeployLogApiTests 覆盖。
/// </summary>
[Collection("StarServer")]
public class DeployLogNodeCommandTests
{
    private readonly StarServerFixture _fixture;

    public DeployLogNodeCommandTests(StarServerFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "节点指令-应用日志：list/tail 走真实 DeployLogCommands")]
    public async Task DeployLog_ListAndTail_UsesRealHandler()
    {
        using var seed = new DeployLogSeed();
        var (app, token) = await CreateControllerAppAsync();
        var client = new TestStarClient(_fixture.ServerUrl) { Log = XTrace.Log };
        var manager = new ServiceManager();
        Node? node = null;
        try
        {
            // ClientBase 首次登录超时只有 1 秒，之后按次数递增到 Timeout。
            // 冷启动建节点经常超过 1 秒，必须在同一个客户端上重试，不能每次 new 一个。
            Exception? last = null;
            for (var i = 0; i < 8 && !client.Logined; i++)
            {
                try
                {
                    await client.Login();
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
            Assert.True(client.Logined, "节点未能登录：" + last?.Message);
            manager.Attach(client);

            node = Node.FindByCode(client.Code);
            Assert.NotNull(node);
            seed.Bind(node!);
            Assert.Equal(node!.ID, seed.DeployNode!.NodeId);

            var sessions = _fixture.Host.Services.GetRequiredService<NodeSessionManager>();
            for (var i = 0; i < 60 && sessions.Get(node.Code) == null; i++) await Task.Delay(250);
            Assert.NotNull(sessions.Get(node.Code));

            var listArg = new DeployLogListArg { LogDirectory = seed.LogDirectory, Pattern = "*.log" }.ToJson();
            var list = await NodeCommandClient.SendAsync(_fixture.ServerUrl, token, node.Code, "deploy/log/list", listArg!);
            Assert.NotNull(list);
            Assert.Equal(CommandStatus.已完成, list!.Status);
            Assert.Contains(seed.LogFileName, list.Data?.ToString());

            var tailArg = new DeployLogTailArg { LogDirectory = seed.LogDirectory, File = seed.LogFileName, MaxBytes = 65536 }.ToJson();
            var tail = await NodeCommandClient.SendAsync(_fixture.ServerUrl, token, node.Code, "deploy/log/tail", tailArg!);
            Assert.NotNull(tail);
            Assert.Equal(CommandStatus.已完成, tail!.Status);
            Assert.Contains(DeployLogSeed.SampleLine, tail.Data?.ToString());
        }
        finally
        {
            client.Dispose();
            manager.Dispose();
            Cleanup(node);
            CleanupApp(app);
        }
    }

    /// <summary>伪造硬件标识，避免命中本机已注册节点。</summary>
    private sealed class TestStarClient(String urls) : StarClient(urls)
    {
        public override ILoginRequest BuildLoginRequest()
        {
            var request = base.BuildLoginRequest();
            if (request is LoginInfo inf && inf.Node != null)
            {
                inf.Node.Macs = "aabbcc" + Rand.NextBytes(6).ToHex();
                inf.Node.MachineGuid = Guid.NewGuid().ToString("N");
                inf.Node.UUID = Guid.NewGuid().ToString("N");
                inf.Node.SerialNumber = "sn" + Guid.NewGuid().ToString("N");
                inf.Node.DiskID = "disk" + Guid.NewGuid().ToString("N");
                inf.Node.Board = "board" + Guid.NewGuid().ToString("N");
            }
            return request;
        }
    }

    private async Task<(App app, String token)> CreateControllerAppAsync()
    {
        var name = "logctl_" + Guid.NewGuid().ToString("N")[..8];
        var app = new App { Name = name, Secret = "log-secret-" + Guid.NewGuid().ToString("N")[..6], Enable = true, AllowControlNodes = "*" };
        app.Insert();

        using var http = new HttpClient { BaseAddress = new Uri(_fixture.ServerUrl) };
        var rs = await http.PostAsync<LoginResponse>("app/login", new { AppId = name, Secret = app.Secret });
        Assert.False(rs?.Token.IsNullOrEmpty());
        return (app, rs!.Token!);
    }

    private static void Cleanup(Node? node)
    {
        if (node == null) return;
        NodeOnline.FindByNodeId(node.ID)?.Delete();
        node.Delete();
    }

    private static void CleanupApp(App? app)
    {
        if (app == null) return;
        AppOnline.Delete(AppOnline._.AppId == app.Id);
        app.Delete();
    }
}
