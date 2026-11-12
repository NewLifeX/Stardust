using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>OpenAI 兼容接口鉴权测试。覆盖未携带令牌、应用禁用与平台未启用三种拒绝路径</summary>
[Collection(StarTestServerCollection.Name)]
public class OpenAIChatSecurityTests : StarTestBase
{
    public OpenAIChatSecurityTests(StarTestServer fixture) : base(fixture) { }

    private static String ExtractToken(String body)
    {
        var m = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static OpenAIChatRequest NewRequest() => new()
    {
        Messages = [new OpenAIChatMessage { Content = "hello" }],
    };

    [Fact(DisplayName = "未携带令牌调用OpenAI接口返回401错误体")]
    public async Task Chat_WithoutToken_Rejected()
    {
        var client = _server.CreateClient();

        var resp = await client.PostAsync("v1/chat/completions", JsonContent.Create(NewRequest()));
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Contains("\"error\"", body);
        Assert.Contains("authentication_error", body);
    }

    [Fact(DisplayName = "应用禁用后调用OpenAI接口返回403")]
    public async Task Chat_DisabledApp_Rejected()
    {
        var name = "ai_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-" + Rand.NextString(8), Enable = true };
        app.Insert();
        try
        {
            var client = _server.CreateClient();

            var loginResp = await client.PostAsync("app/login", JsonContent.Create(new { AppId = name, Secret = app.Secret }));
            var token = ExtractToken(await loginResp.Content.ReadAsStringAsync());
            Assert.False(token.IsNullOrEmpty(), "登录应下发令牌");

            app.Enable = false;
            app.Update();

            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var resp = await client.PostAsync("v1/chat/completions", JsonContent.Create(NewRequest()));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            Assert.Contains("permission_error", body);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "平台未启用AI时OpenAI接口返回403")]
    public async Task Chat_AIDisabled_Rejected()
    {
        var set = StarServerSetting.Current;
        var old = set.EnableAI;

        var name = "ai_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-" + Rand.NextString(8), Enable = true };
        app.Insert();
        try
        {
            set.EnableAI = false;

            var client = _server.CreateClient();

            var loginResp = await client.PostAsync("app/login", JsonContent.Create(new { AppId = name, Secret = app.Secret }));
            var token = ExtractToken(await loginResp.Content.ReadAsStringAsync());
            Assert.False(token.IsNullOrEmpty(), "登录应下发令牌");

            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var resp = await client.PostAsync("v1/chat/completions", JsonContent.Create(NewRequest()));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            Assert.Contains("\"error\"", body);
            Assert.False(body.IsNullOrWhiteSpace());
        }
        finally
        {
            set.EnableAI = old;
            app.Delete();
        }
    }
}
