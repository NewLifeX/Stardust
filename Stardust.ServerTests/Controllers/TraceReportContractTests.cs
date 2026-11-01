using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>非 .NET 客户端接入契约测试。按 Doc/APM-17-监控接入API.md 的 JSON 报文上报埋点，验证追踪项与追踪数据落库</summary>
[Collection(StarTestServerCollection.Name)]
public class TraceReportContractTests : StarTestBase
{
    public TraceReportContractTests(StarTestServer fixture) : base(fixture) { }

    [Fact(DisplayName = "非法时间字段的报文返回错误而非静默丢弃")]
    public async Task Report_InvalidTimeField_ReturnsError()
    {
        var name = "trb_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "trb-secret", Enable = true };
        app.Insert();
        new AppTracer { Name = name, AppId = app.Id, Enable = true }.Insert();

        try
        {
            var loginClient = _server.CreateClient();
            var login = await loginClient.PostAsync<LoginResponse>("app/login", new { AppId = name, Secret = "trb-secret" });
            Assert.NotNull(login?.Token);

            // 时间字段误用日期字符串，服务端无法绑定整个模型
            var json = $@"{{""AppId"":""{name}"",""Builders"":[{{""Name"":""probe/op"",""StartTime"":""2026-07-15T10:00:00""}}]}}";

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", login!.Token);

            var resp = await client.PostAsync("/Trace/Report", new StringContent(json, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Contains("解析失败", body);
            Assert.DoesNotContain("period", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppTracer.FindByName(name)?.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "非.NET 客户端上报埋点并生成追踪项与追踪数据")]
    public async Task Report_JsonPayload_CreatesTraceItemAndData()
    {
        var name = "trc_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "trc-secret", Enable = true };
        app.Insert();
        new AppTracer { Name = name, AppId = app.Id, Enable = true }.Insert();

        var itemName = "http://example.com/api/" + Rand.NextString(6);
        try
        {
            var loginClient = _server.CreateClient();
            var login = await loginClient.PostAsync<LoginResponse>("app/login", new { AppId = name, Secret = "trc-secret" });
            Assert.NotNull(login?.Token);

            // 时间字段为 Unix 毫秒，与 SDK/Go、SDK/JavaScript 等实现一致
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var json = $@"{{
  ""AppId"": ""{name}"",
  ""ClientId"": ""192.168.1.100@12345"",
  ""Version"": ""3.7"",
  ""Builders"": [
    {{
      ""Name"": ""{itemName}"",
      ""StartTime"": {now},
      ""Total"": 100,
      ""Errors"": 2,
      ""Cost"": 5000,
      ""MaxCost"": 200,
      ""MinCost"": 1,
      ""Samples"": [
        {{ ""Id"": ""span-001"", ""StartTime"": {now}, ""EndTime"": {now + 5}, ""Tag"": ""GET {itemName}"" }}
      ]
    }}
  ]
}}";

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", login!.Token);

            var resp = await client.PostAsync("/Trace/Report", new StringContent(json, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, "上报应成功，实际响应：" + body);
            Assert.Contains("period", body, StringComparison.OrdinalIgnoreCase);

            // 埋点项已按应用落库
            var tracer = AppTracer.FindByName(name);
            Assert.NotNull(tracer);
            var item = TraceItem.FindByAppIdAndName(tracer!.ID, itemName);
            Assert.NotNull(item);

            // 追踪数据已落库（按天分表，StatDate 精确到日；AppId 为 AppTracer 编号）
            var datas = TraceData.Search(tracer.ID, item!.Id, null, DateTime.Today, 10);
            Assert.NotNull(datas);
            Assert.NotEmpty(datas);
            Assert.Equal(100, datas[0].Total);
        }
        finally
        {
            var tracer = AppTracer.FindByName(name);
            if (tracer != null)
            {
                foreach (var item in TraceItem.FindAllByApp(tracer.ID))
                {
                    item.Delete();
                }
                tracer.Delete();
            }
            app.Delete();
        }
    }

    [Fact(DisplayName = "WebSocket 对端断开不计入错误数，异常采样压缩为短摘要")]
    public async Task Report_WebSocketDisconnect_FiltersErrorAndCompressesSample()
    {
        var name = "trws_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "trws-secret", Enable = true };
        app.Insert();
        new AppTracer { Name = name, AppId = app.Id, Enable = true }.Insert();

        var itemName = "/app/notify";
        try
        {
            var loginClient = _server.CreateClient();
            var login = await loginClient.PostAsync<LoginResponse>("app/login", new { AppId = name, Secret = "trws-secret" });
            Assert.NotNull(login?.Token);

            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var error = "System.Net.WebSockets.WebSocketException (0x80004005): The remote party closed the WebSocket connection without completing the close handshake." +
                Environment.NewLine + "   at System.Net.WebSockets.ManagedWebSocket.ThrowEOFUnexpected()" +
                Environment.NewLine + "   at NewLife.Remoting.Extensions.Services.WsCommandSession.ReceiveLoopAsync(IPEndPoint remote, CancellationTokenSource source)" +
                Environment.NewLine + "   at Stardust.Server.Controllers.AppController.HandleNotify(DeviceContext context, WebSocket socket, CancellationToken cancellationToken)";

            var payload = new
            {
                AppId = name,
                ClientId = "192.168.1.100@12345",
                Builders = new[]
                {
                    new
                    {
                        Name = itemName,
                        StartTime = now,
                        Total = 1,
                        Errors = 1,
                        Cost = 5,
                        MaxCost = 5,
                        MinCost = 5,
                        ErrorSamples = new[]
                        {
                            new { Id = "span-err", StartTime = now, EndTime = now + 5, Error = error }
                        }
                    }
                }
            };

            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", login!.Token);

            var resp = await client.PostAsync("/Trace/Report", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.IsSuccessStatusCode, "上报应成功，实际响应：" + body);

            var tracer = AppTracer.FindByName(name);
            Assert.NotNull(tracer);
            var item = TraceItem.FindByAppIdAndName(tracer!.ID, itemName);
            Assert.NotNull(item);

            var datas = TraceData.Search(tracer.ID, item!.Id, null, DateTime.Today, 10);
            Assert.NotNull(datas);
            Assert.NotEmpty(datas);
            Assert.Equal(0, datas[0].Errors);
            Assert.Equal(1, datas[0].ErrorSamples);

            var samples = SampleData.FindAllByDataIds([datas[0].Id], DateTime.Today);
            var sample = samples.FirstOrDefault(e => !e.Success);
            Assert.NotNull(sample);
            Assert.Contains("WebSocketException", sample.Error);
            Assert.Contains("AppController.HandleNotify", sample.Error);
            Assert.DoesNotContain("ThrowEOFUnexpected", sample.Error);
        }
        finally
        {
            var tracer = AppTracer.FindByName(name);
            if (tracer != null)
            {
                foreach (var item in TraceItem.FindAllByApp(tracer.ID))
                {
                    item.Delete();
                }
                tracer.Delete();
            }
            app.Delete();
        }
    }
}
