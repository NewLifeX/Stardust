using System;
using System.Collections.Generic;
using System.Linq;
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
using NewLife.Serialization;
using NewLife.Threading;
using Stardust;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Monitors;
using Stardust.Monitors;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Communication;

/// <summary>星尘SDK 应用客户端到服务端接口的端到端测试。使用真实 AppClient 连接真实 StarServer（真实 TCP 端口）</summary>
/// <remarks>
/// 应用客户端（AppClient）以 App/* 协议与服务端通讯，
/// 本测试覆盖应用登录、心跳、注销、事件上报、服务注册发现、命令下行回复、埋点上报与配置中心拉取等全部接口面。
/// </remarks>
[Collection("StarServer")]
public class AppCommunicationTests
{
    private readonly StarServerFixture _fixture;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppCommunicationTests(StarServerFixture fixture)
    {
        _fixture = fixture;

        // 追踪数据按天分表，SQLite 下需配置分库连接
        StarDataHelper.SplitSqliteTables();
    }

    #region 辅助
    private AppClient CreateClient(String appId, String? secret = null, String? clientId = null)
    {
        var client = new AppClient(_fixture.ServerUrl)
        {
            AppId = appId,
            Secret = secret,
            Log = XTrace.Log,
        };
        if (!clientId.IsNullOrEmpty()) client.ClientId = clientId;

        return client;
    }

    private static void CleanupApp(String name)
    {
        var app = App.FindByName(name);
        if (app == null) return;

        AppOnline.Delete(AppOnline._.AppId == app.Id);
        app.Delete();
    }

    /// <summary>创建具备应用控制权限的应用，并取得应用令牌（模拟管理端/控制应用）</summary>
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

    private async Task<CommandReplyModel?> SendAppCommandAsync(String token, String code, String command, String argument, Int32 timeout)
    {
        using var http = new HttpClient { BaseAddress = new Uri(_fixture.ServerUrl) };
        http.DefaultRequestHeaders.Add("X-Token", token);

        var resp = await http.PostAsync("/App/SendCommand", JsonContent.Create(new { code, command, argument, timeout }));
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
    [Fact(DisplayName = "应用通讯-应用登录：自动注册应用并颁发令牌")]
    public async Task Login_AutoRegister_IssuesToken()
    {
        var name = "e2e_app_" + Rand.NextString(8);
        var client = CreateClient(name, clientId: "e2e_" + Rand.NextString(8));
        try
        {
            var rs = await client.Login();
            Assert.NotNull(rs);
            Assert.True(client.Logined, "应用登录失败");
            Assert.False(rs!.Token.IsNullOrEmpty(), "登录响应缺少访问令牌");

            var app = App.FindByName(name);
            Assert.NotNull(app);
            Assert.True(app!.Enable, "自动注册的应用应处于启用状态");
        }
        finally
        {
            client.Dispose();
            CleanupApp(name);
        }
    }

    [Fact(DisplayName = "应用通讯-应用心跳：返回服务端时间并记录在线")]
    public async Task Ping_KeepsOnline()
    {
        var name = "e2e_app_" + Rand.NextString(8);
        var client = CreateClient(name, clientId: "e2e_" + Rand.NextString(8));
        try
        {
            Assert.NotNull(await client.Login());

            var rs = await client.Ping();
            Assert.NotNull(rs);
            Assert.True(rs!.ServerTime > 0, "心跳响应应包含服务端时间");

            var app = App.FindByName(name);
            Assert.NotNull(app);
            Assert.NotNull(AppOnline.Find(AppOnline._.AppId == app!.Id));
        }
        finally
        {
            client.Dispose();
            CleanupApp(name);
        }
    }

    [Fact(DisplayName = "应用通讯-应用注销：客户端退出登录")]
    public async Task Logout_Works()
    {
        var name = "e2e_app_" + Rand.NextString(8);
        var client = CreateClient(name, clientId: "e2e_" + Rand.NextString(8));
        try
        {
            Assert.NotNull(await client.Login());

            await client.Logout("e2e测试注销");
            Assert.False(client.Logined);
        }
        finally
        {
            client.Dispose();
            CleanupApp(name);
        }
    }

    [Fact(DisplayName = "应用通讯-登录校验：缺少应用标识时返回明确错误而非500")]
    public async Task Login_MissingAppId_RejectedCleanly()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_fixture.ServerUrl) };

        // 不携带 AppId 的登录请求（异常客户端或探测流量）
        var resp = await http.PostAsync("app/login", JsonContent.Create(new { AppName = "e2e_no_id" }));
        var body = await resp.Content.ReadAsStringAsync();

        // 服务端返回明确的参数错误（ApiFilter 约定：HTTP 200 + 错误报文），而不是内部空引用异常
        Assert.Contains("应用标识不能为空", body);
        Assert.DoesNotContain("Object reference", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NullReference", body, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region 事件上报
    [Fact(DisplayName = "应用通讯-事件上报：批量写入并返回条数")]
    public async Task PostEvents_Writes()
    {
        var name = "e2e_app_" + Rand.NextString(8);
        var client = CreateClient(name, clientId: "e2e_" + Rand.NextString(8));
        try
        {
            Assert.NotNull(await client.Login());

            var n = await client.PostEvents(
            [
                new EventModel { Type = "e2e", Name = "comm_test", Remark = "端到端接口测试", Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            ]);

            Assert.True(n >= 1, $"事件上报应成功写入，实际返回 {n}");
        }
        finally
        {
            client.Dispose();
            CleanupApp(name);
        }
    }
    #endregion

    #region 服务注册发现
    [Fact(DisplayName = "应用通讯-服务注册：发布、发现与取消注册全链路")]
    public async Task Registry_Publish_Resolve_Unregister()
    {
        var publisherName = "e2e_pub_" + Rand.NextString(8);
        var consumerName = "e2e_con_" + Rand.NextString(8);
        var serviceName = "e2e_svc_" + Rand.NextString(6);
        var address = "http://127.0.0.1:18080";

        var publisher = CreateClient(publisherName, clientId: "e2e_" + Rand.NextString(8));
        var consumer = CreateClient(consumerName, clientId: "e2e_" + Rand.NextString(8));
        var service = Service.FindByName(serviceName);
        try
        {
            Assert.NotNull(await publisher.Login());
            Assert.NotNull(await consumer.Login());

            // 发布服务
            await publisher.RegisterAsync(serviceName, address, "e2e");

            // 消费发现服务
            var services = await consumer.ResolveAsync(serviceName);
            Assert.NotNull(services);
            Assert.Contains(services!, x => x.ServiceName == serviceName && x.Address != null && x.Address.Contains("18080"));

            // 取消注册
            Assert.NotNull(publisher.Unregister(serviceName));
        }
        finally
        {
            publisher.Dispose();
            consumer.Dispose();

            // 清理注册中心数据
            service ??= Service.FindByName(serviceName);
            if (service != null)
            {
                AppService.Delete(AppService._.ServiceId == service.Id);
                AppConsume.Delete(AppConsume._.ServiceId == service.Id);
                service.Delete();
            }
            CleanupApp(publisherName);
            CleanupApp(consumerName);
        }
    }
    #endregion

    #region 命令下行
    [Fact(DisplayName = "应用通讯-命令下行：WebSocket 推送、执行与回复")]
    public async Task Command_Downlink_And_Reply()
    {
        var (ctlApp, token) = await CreateControllerAppAsync();

        var targetName = "e2e_tgt_" + Rand.NextString(8);
        var clientId = "e2e_" + Rand.NextString(8);
        var target = CreateClient(targetName, clientId: clientId);
        try
        {
            // 注册命令处理器
            var received = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<String, String> handler = arg => { received.TrySetResult(arg ?? ""); return "pong:" + arg; };
            target.RegisterCommand("e2e/echo", handler);

            // 打开连接（登录并启动心跳定时器，触发 WebSocket 通知通道）
            target.Open();
            for (var i = 0; i < 60 && !target.Logined; i++) await Task.Delay(250);
            Assert.True(target.Logined, "目标应用未能在超时时间内登录");

            // 等待服务端建立应用会话（WebSocket 长连接）
            var code = $"{targetName}@{clientId}";
            var sessions = _fixture.Host.Services.GetRequiredService<AppSessionManager>();
            for (var i = 0; i < 60 && sessions.Get(code) == null; i++) await Task.Delay(250);
            Assert.NotNull(sessions.Get(code));

            // 控制应用下发命令
            var reply = await SendAppCommandAsync(token, code, "e2e/echo", "hello", 15);
            Assert.NotNull(reply);
            Assert.Equal("pong:hello", reply!.Data);

            // 目标应用确实执行了处理器
            Assert.True(received.Task.Wait(TimeSpan.FromSeconds(3)), "目标应用未执行命令处理器");
        }
        finally
        {
            target.Dispose();
            CleanupApp(targetName);
            CleanupApp(ctlApp.Name);
        }
    }
    #endregion

    #region 埋点上报
    [Fact(DisplayName = "应用通讯-埋点上报：追踪项与追踪数据落库")]
    public async Task TraceReport_CreatesTraceItemAndData()
    {
        var name = "e2e_trc_" + Rand.NextString(8);
        var client = CreateClient(name, clientId: "e2e_" + Rand.NextString(8));
        try
        {
            Assert.NotNull(await client.Login());

            // 通过真实追踪器产生埋点数据
            var spanName = "e2e_trace_" + Rand.NextString(6);
            var tracer = new StarTracer { AppId = name, Client = client, TrimSelf = false, EnableMeter = false, Period = 3600 };
            using (var span = tracer.NewSpan(spanName))
            {
                span?.SetTag("e2e=1");
            }

            // 立即取出发送，避免等待采样定时器
            var builders = tracer.TakeAll();
            Assert.NotEmpty(builders);

            var model = new TraceModel { AppId = name, ClientId = client.ClientId, Builders = builders };
            var resp = await client.InvokeAsync<TraceResponse>("Trace/Report", model);
            Assert.NotNull(resp);

            // 追踪项与追踪数据已按应用落库
            var appTracer = AppTracer.FindByName(name);
            Assert.NotNull(appTracer);
            var item = TraceItem.FindByAppIdAndName(appTracer!.ID, spanName);
            Assert.NotNull(item);

            var datas = TraceData.Search(appTracer.ID, item!.Id, null, DateTime.Today, 10);
            Assert.NotEmpty(datas);
        }
        finally
        {
            client.Dispose();
            AppTracer.FindByName(name)?.Delete();
            CleanupApp(name);
        }
    }
    [Fact(DisplayName = "应用通讯-埋点上报（大报文）：Trace/ReportRaw 落库")]
    public async Task TraceReportRaw_BigPayload()
    {
        var name = "e2e_raw_" + Rand.NextString(8);
        var client = CreateClient(name, clientId: "e2e_" + Rand.NextString(8));
        try
        {
            Assert.NotNull(await client.Login());

            // 构造超过 1KB 的埋点报文，走客户端的原始字节上报分支
            var spanName = "e2e_raw_" + Rand.NextString(6);
            var tracer = new StarTracer { AppId = name, Client = client, TrimSelf = false, EnableMeter = false, Period = 3600 };
            using (var span = tracer.NewSpan(spanName))
            {
                span?.SetTag("big=" + new String('x', 1200));
            }
            var builders = tracer.TakeAll();
            var model = new TraceModel { AppId = name, ClientId = client.ClientId, Builders = builders };
            var json = model.ToJson();
            Assert.True(json.Length > 1024);

            var resp = await client.InvokeAsync<TraceResponse>("Trace/ReportRaw", json.GetBytes());
            Assert.NotNull(resp);

            var appTracer = AppTracer.FindByName(name);
            Assert.NotNull(appTracer);
            Assert.NotNull(TraceItem.FindByAppIdAndName(appTracer!.ID, spanName));
        }
        finally
        {
            client.Dispose();
            AppTracer.FindByName(name)?.Delete();
            CleanupApp(name);
        }
    }

    #endregion

    #region 配置中心
    [Fact(DisplayName = "应用通讯-配置中心：真实工厂拉取已发布配置")]
    public async Task ConfigCenter_PullPublishedConfig()
    {
        var name = "e2e_cfg_" + Rand.NextString(8);
        var secret = "e2e-secret-" + Rand.NextString(6);
        var app = new App { Name = name, Secret = secret, Enable = true };
        app.Insert();

        var key = "e2e_key_" + Rand.NextString(4);
        var value = "e2e_value_" + Rand.NextString(4);
        try
        {
            // 服务端写入并发布配置
            using (var http = new HttpClient { BaseAddress = new Uri(_fixture.ServerUrl) })
            {
                var resp = await http.PostAsync($"/Config/SetAll?appId={name}&secret={secret}",
                    JsonContent.Create(new { AppId = name, Secret = secret, Configs = new Dictionary<String, Object> { [key] = value } }));
                var body = await resp.Content.ReadAsStringAsync();
                Assert.True(resp.IsSuccessStatusCode, "写入配置失败：" + body);
            }
            AppConfig.FindByName(name)!.Publish();

            // 客户端通过真实星尘工厂拉取配置
            using var star = new StarFactory(_fixture.ServerUrl, name, secret);
            Assert.NotNull(star.App);
            Assert.NotNull(await star.App!.Login());

            var config = star.Config;
            Assert.NotNull(config);
            Assert.Equal(value, config![key]);
        }
        finally
        {
            // StarFactory 会把全局定时器时间提供者指向自身的客户端，销毁后必须还原为系统时间，避免污染同进程内的服务端
            TimerScheduler.GlobalTimeProvider = TimeProvider.System;

            // 清理应用、配置与在线记录
            var cfg = AppConfig.FindByName(name);
            if (cfg != null)
            {
                foreach (var item in ConfigData.FindAllByApp(cfg.Id)) item.Delete();
                cfg.Delete();
            }
            CleanupApp(name);
        }
    }
    #endregion
}
