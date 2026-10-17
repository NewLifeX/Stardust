using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>应用心跳保活测试。覆盖 App/Ping 的令牌鉴权与心跳响应字段</summary>
[Collection(StarTestServerCollection.Name)]
public class AppPingTests : StarTestBase
{
    public AppPingTests(StarTestServer fixture) : base(fixture) { }

    private static String ExtractToken(String body)
    {
        var m = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    [Fact(DisplayName = "登录后心跳成功并返回服务端时间与心跳周期")]
    public async Task Ping_AfterLogin_ReturnsServerTimeAndPeriod()
    {
        var name = "ping_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-" + Rand.NextString(8), Enable = true };
        app.Insert();
        try
        {
            var client = _server.CreateClient();

            // 登录获取令牌
            var loginResp = await client.PostAsync("app/login", JsonContent.Create(new { AppId = name, Secret = app.Secret }));
            var loginBody = await loginResp.Content.ReadAsStringAsync();
            var token = ExtractToken(loginBody);
            Assert.False(token.IsNullOrEmpty(), "登录应下发令牌：" + loginBody);

            // 携带令牌发起心跳
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var pingResp = await client.PostAsync("app/ping", JsonContent.Create(new { Name = name, Version = "1.0.0", MachineName = "test-machine" }));
            var pingBody = await pingResp.Content.ReadAsStringAsync();

            Assert.True(pingResp.IsSuccessStatusCode, "心跳应成功，实际响应：" + pingBody);
            Assert.Contains("serverTime", pingBody, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("period", pingBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "未携带令牌的心跳被拒绝且不下发心跳数据")]
    public async Task Ping_WithoutToken_Rejected()
    {
        var client = _server.CreateClient();

        var resp = await client.PostAsync("app/ping", JsonContent.Create(new { Name = "nobody" }));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("serverTime", body, StringComparison.OrdinalIgnoreCase);
    }
}
