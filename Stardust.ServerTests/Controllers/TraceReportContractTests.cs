using System;
using System.Net.Http;
using System.Text;
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
public class TraceReportContractTests
{
    private readonly TestServer _server;

    public TraceReportContractTests()
    {
        // 追踪数据按天分表，SQLite 下需配置分库连接
        StarDataHelper.SplitSqliteTables();

#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
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
}
