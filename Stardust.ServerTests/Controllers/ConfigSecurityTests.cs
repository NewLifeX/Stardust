using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>配置中心跨应用越权安全测试。验证应用令牌只能读写自身配置（安全扫描 V-5）</summary>
[Collection(StarTestServerCollection.Name)]
public class ConfigSecurityTests : StarTestBase
{
    public ConfigSecurityTests(StarTestServer fixture) : base(fixture) { }

    private async Task<String> AppLoginTokenAsync(String appId, String secret)
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("app/login", new { AppId = appId, Secret = secret });
        Assert.NotNull(rs?.Token);
        return rs.Token;
    }

    private async Task<String> GetAllAsync(String appId, String token)
    {
        var client = _server.CreateClient();
        var response = await client.GetAsync($"/Config/GetAll?appId={Uri.EscapeDataString(appId)}&token={token}");
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "应用令牌不能读取其它应用配置")]
    public async Task Token_ReadOtherApp_Rejected()
    {
        var a = new App { Name = "cfa_" + Rand.NextString(8), Secret = "cf-secret-a", Enable = true };
        var b = new App { Name = "cfb_" + Rand.NextString(8), Secret = "cf-secret-b", Enable = true };
        a.Insert();
        b.Insert();
        try
        {
            var token = await AppLoginTokenAsync(a.Name, "cf-secret-a");

            var body = await GetAllAsync(b.Name, token);

            Assert.Contains("\"code\"", body);
            Assert.Contains("越权", body);
        }
        finally
        {
            AppConfig.FindByName(a.Name)?.Delete();
            AppConfig.FindByName(b.Name)?.Delete();
            a.Delete();
            b.Delete();
        }
    }

    [Fact(DisplayName = "应用令牌可正常读取自身配置（回归保护）")]
    public async Task Token_ReadSelf_Ok()
    {
        var a = new App { Name = "cfa_" + Rand.NextString(8), Secret = "cf-secret-a", Enable = true };
        a.Insert();
        try
        {
            var token = await AppLoginTokenAsync(a.Name, "cf-secret-a");

            var body = await GetAllAsync(a.Name, token);

            Assert.DoesNotContain("越权", body);
            Assert.Contains("data", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppConfig.FindByName(a.Name)?.Delete();
            a.Delete();
        }
    }

    [Fact(DisplayName = "应用令牌不带appId时按令牌主体拉取自身配置（回归保护）")]
    public async Task Token_NoAppId_UsesSubject()
    {
        var a = new App { Name = "cfa_" + Rand.NextString(8), Secret = "cf-secret-a", Enable = true };
        a.Insert();
        try
        {
            var token = await AppLoginTokenAsync(a.Name, "cf-secret-a");

            var client = _server.CreateClient();
            var response = await client.GetAsync($"/Config/GetAll?token={token}");
            var body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("越权", body);
            Assert.Contains("data", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppConfig.FindByName(a.Name)?.Delete();
            a.Delete();
        }
    }
}
