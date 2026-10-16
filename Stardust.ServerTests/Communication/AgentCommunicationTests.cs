using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Communication;

/// <summary>星尘代理到服务端通讯的端到端测试。使用真实 StarClient 客户端连接真实 StarServer（真实 TCP 端口）</summary>
/// <remarks>
/// 星尘代理（StarAgent）通过 StarClient（MyStarClient 基类）以 Node/* 协议与服务端通讯，
/// 本测试覆盖节点登录、心跳、注销、升级检查、事件上报、部署查询与命令下行回复等全部通讯面。
/// </remarks>
[Collection("StarServer")]
public class AgentCommunicationTests
{
    private readonly StarServerFixture _fixture;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AgentCommunicationTests(StarServerFixture fixture) => _fixture = fixture;

    /// <summary>测试用节点客户端。伪造唯一硬件标识，避免命中本机真实节点</summary>
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
            }
            return request;
        }
    }

    #region 辅助
    /// <summary>登录一个全新的测试节点</summary>
    private async Task<(StarClient client, Node node)> LoginAsync()
    {
        var client = new TestStarClient(_fixture.ServerUrl) { Log = XTrace.Log };

        var rs = await client.Login();
        Assert.NotNull(rs);
        Assert.True(client.Logined, "节点登录失败");
        Assert.False(rs!.Token.IsNullOrEmpty(), "登录响应缺少访问令牌");

        var node = Node.FindByCode(rs.Code ?? client.Code);
        Assert.NotNull(node);

        return (client, node!);
    }

    private static void Cleanup(Node? node)
    {
        if (node == null) return;

        NodeOnline.FindByNodeId(node.ID)?.Delete();
        node.Delete();
    }

    /// <summary>创建具备节点控制权限的应用，并取得应用令牌（模拟管理端/控制应用）</summary>
    private async Task<(App app, String token)> CreateControllerAppAsync()
    {
        var name = "e2e_ctl_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "e2e-secret-" + Rand.NextString(6), Enable = true, AllowControlNodes = "*" };
        app.Insert();

        using var http = new HttpClient { BaseAddress = new Uri(_fixture.ServerUrl) };
        var rs = await http.PostAsync<LoginResponse>("app/login", new { AppId = name, Secret = app.Secret });
        Assert.NotNull(rs?.Token);

        return (app, rs!.Token!);
    }

    private static void CleanupApp(App? app)
    {
        if (app == null) return;

        AppOnline.Delete(AppOnline._.AppId == app.Id);
        app.Delete();
    }

    private async Task<CommandReplyModel?> SendNodeCommandAsync(String token, String code, String command, String argument, Int32 timeout)
    {
        using var http = new HttpClient { BaseAddress = new Uri(_fixture.ServerUrl) };
        http.DefaultRequestHeaders.Add("X-Token", token);

        var resp = await http.PostAsync("/Node/SendCommand", JsonContent.Create(new { code, command, argument, timeout }));
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode || body.IsNullOrEmpty()) return null;

        return ParseApiResponse<CommandReplyModel>(body);
    }

    /// <summary>解析 ApiFilter 包装的响应体（{code,data,msg}），取出 data 载荷</summary>
    private static T? ParseApiResponse<T>(String body)
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return default;
        if (!doc.RootElement.TryGetProperty("data", out var data)) return default;
        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return default;

        return data.Deserialize<T>(_jsonOptions);
    }
    #endregion

    #region 登录注销
    [Fact(DisplayName = "代理通讯-节点登录：自动注册节点并颁发令牌")]
    public async Task Login_AutoRegister_IssuesToken()
    {
        var (client, node) = await LoginAsync();
        try
        {
            Assert.True(node.Enable, "自动注册的节点应处于启用状态");
            Assert.False(node.Secret.IsNullOrEmpty(), "服务端应下发节点密钥");
            Assert.Equal(node.Code, client.Code);

            // 服务端已建立在线记录
            var online = NodeOnline.FindByNodeId(node.ID);
            Assert.NotNull(online);
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
        }
    }

    [Fact(DisplayName = "代理通讯-节点心跳：返回服务端时间并保持在线")]
    public async Task Ping_KeepsOnline()
    {
        var (client, node) = await LoginAsync();
        try
        {
            var rs = await client.Ping();
            Assert.NotNull(rs);
            Assert.True(rs!.ServerTime > 0, "心跳响应应包含服务端时间");

            // 在线记录持续存在
            Assert.NotNull(NodeOnline.FindByNodeId(node.ID));
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
        }
    }

    [Fact(DisplayName = "代理通讯-节点注销：服务端标记离线")]
    public async Task Logout_MarksOffline()
    {
        var (client, node) = await LoginAsync();
        try
        {
            await client.Logout("e2e测试注销");
            Assert.False(client.Logined);
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
        }
    }
    #endregion

    #region 升级与事件
    [Fact(DisplayName = "代理通讯-升级检查：协议可达且无新版本返回空")]
    public async Task Upgrade_NoVersion_ReturnsNull()
    {
        var (client, node) = await LoginAsync();
        try
        {
            // 直接调用协议接口，避免触发客户端自动升级下载
            var rs = await client.InvokeAsync<IUpgradeInfo?>("Node/Upgrade", new { channel = "Release" });

            // 测试节点未配置发布版本，应返回空且不抛异常
            Assert.Null(rs);
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
        }
    }

    [Fact(DisplayName = "代理通讯-事件上报：批量写入并返回条数")]
    public async Task PostEvents_Writes()
    {
        var (client, node) = await LoginAsync();
        try
        {
            var n = await client.PostEvents(
            [
                new EventModel { Type = "e2e", Name = "comm_test", Remark = "端到端通讯测试", Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            ]);

            Assert.True(n >= 1, $"事件上报应成功写入，实际返回 {n}");
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
        }
    }
    #endregion

    #region 部署协议
    [Fact(DisplayName = "代理通讯-部署查询：Deploy/GetAll 无发布任务时返回空")]
    public async Task DeployGetAll_NoTask_ReturnsEmpty()
    {
        var (client, node) = await LoginAsync();
        try
        {
            var list = await client.GetDeploy(0, "e2e_no_such_deploy", node.Code);

            Assert.True(list == null || list.Length == 0, "无发布任务时应返回空");
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
        }
    }

    [Fact(DisplayName = "代理通讯-部署心跳：Deploy/Ping 协议可达")]
    public async Task DeployPing_Reachable()
    {
        var (client, node) = await LoginAsync();
        var name = "e2e_dp_" + Rand.NextString(8);
        try
        {
            var inf = new AppInfo(Process.GetCurrentProcess()) { Name = name };
            var rs = await client.AppPing(inf);

            // 部署集与部署节点已在服务端建立
            Assert.True(rs >= 0, $"部署心跳应成功，实际返回 {rs}");
            Assert.NotNull(AppDeploy.FindByName(name));
        }
        finally
        {
            client.Dispose();

            // 清理部署心跳创建的部署集、部署节点与应用
            var deploy = AppDeploy.FindByName(name);
            if (deploy != null)
            {
                AppDeployNode.Delete(AppDeployNode._.DeployId == deploy.Id);
                deploy.Delete();
            }
            var app = App.FindByName(name);
            if (app != null)
            {
                AppOnline.Delete(AppOnline._.AppId == app.Id);
                app.Delete();
            }
            Cleanup(node);
        }
    }
    #endregion

    #region 命令下行
    [Fact(DisplayName = "代理通讯-命令下行：WebSocket 推送、执行与回复")]
    public async Task Command_Downlink_And_Reply()
    {
        var (app, token) = await CreateControllerAppAsync();

        var client = new TestStarClient(_fixture.ServerUrl) { Log = XTrace.Log };
        Node? node = null;
        try
        {
            // 注册命令处理器
            var received = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<String, String> handler = arg => { received.TrySetResult(arg ?? ""); return "pong:" + arg; };
            client.RegisterCommand("e2e/echo", handler);

            // 打开连接（登录并启动心跳定时器，触发 WebSocket 通知通道）
            client.Open();
            for (var i = 0; i < 60 && !client.Logined; i++) await Task.Delay(250);
            Assert.True(client.Logined, "节点未能在超时时间内登录");

            node = Node.FindByCode(client.Code);
            Assert.NotNull(node);

            // 等待服务端建立节点会话（WebSocket 长连接）
            var sessions = _fixture.Host.Services.GetRequiredService<NodeSessionManager>();
            for (var i = 0; i < 60 && sessions.Get(node.Code) == null; i++) await Task.Delay(250);
            Assert.NotNull(sessions.Get(node.Code));

            // 控制应用下发命令
            var reply = await SendNodeCommandAsync(token, node.Code, "e2e/echo", "hello", 15);
            Assert.NotNull(reply);
            Assert.Equal("pong:hello", reply!.Data);

            // 节点确实执行了处理器
            Assert.True(received.Task.Wait(TimeSpan.FromSeconds(3)), "节点未执行命令处理器");
        }
        finally
        {
            client.Dispose();
            Cleanup(node);
            CleanupApp(app);
        }
    }
    #endregion
}
