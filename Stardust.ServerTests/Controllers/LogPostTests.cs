using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Data;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>应用日志接收测试。覆盖 Log/Post 的日志行解析落库与未启用应用拒收</summary>
public class LogPostTests
{
    private readonly TestServer _server;

    public LogPostTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    /// <summary>按应用查询当日日志。AppClientLog 按编号时间分表，需走分表感知查询</summary>
    private static IList<AppClientLog> SearchToday(Int32 appId)
        => AppClientLog.Search(appId, null, 0, DateTime.Today, DateTime.Today.AddDays(1), null, new PageParameter { PageSize = 100 });

    private async Task<String> PostAsync(String appName, String content)
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-AppId", appName);
        client.DefaultRequestHeaders.Add("X-ClientId", "log-client@1");

        var resp = await client.PostAsync("/Log", new StringContent(content, Encoding.UTF8, "text/plain"));

        return $"{(Int32)resp.StatusCode}:{await resp.Content.ReadAsStringAsync()}";
    }

    /// <summary>等待日志异步落库</summary>
    private static IList<AppClientLog> WaitLogs(Int32 appId)
    {
        for (var i = 0; i < 25; i++)
        {
            var list = SearchToday(appId);
            if (list.Count > 0) return list;

            Thread.Sleep(200);
        }

        return SearchToday(appId);
    }

    [Fact(DisplayName = "上报日志行被解析并写入应用客户端日志")]
    public async Task Post_LogLines_ParsedAndSaved()
    {
        var name = "lpa_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "log-secret", Enable = true };
        app.Insert();

        try
        {
            // 格式：时间(12字符) 线程 类型 名称 消息，后续非日志行并入上一条消息
            var content = "00:00:04.205  7 Y NewLife.Core Hello Stardust\r\nsecond line of message\r\n";
            var status = await PostAsync(name, content);

            var list = WaitLogs(app.Id);
            Assert.True(list.Count > 0, "应写入应用客户端日志，POST 响应：" + status);

            var log = list[0];
            Assert.Equal(app.Id, log.AppId);
            Assert.Equal("00:00:04.205", log.Time);
            Assert.Equal("7", log.ThreadId);
            Assert.Equal("Y", log.Kind);
            Assert.Equal("NewLife.Core", log.Name);
            Assert.Contains("Hello Stardust", log.Message);
            Assert.Contains("second line of message", log.Message);
            Assert.Equal("log-client@1", log.ClientId);
        }
        finally
        {
            foreach (var item in SearchToday(app.Id))
            {
                item.Delete();
            }
            app.Delete();
        }
    }

    [Fact(DisplayName = "未启用应用上报日志被忽略")]
    public async Task Post_DisabledApp_Ignored()
    {
        var name = "lpd_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "log-secret", Enable = false };
        app.Insert();

        try
        {
            await PostAsync(name, "00:00:04.205  7 Y NewLife.Core should be ignored\r\n");

            Assert.Empty(SearchToday(app.Id));
        }
        finally
        {
            app.Delete();
        }
    }
}
