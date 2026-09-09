using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>应用登录安全测试。验证已存在应用无法通过错误/空密钥触发动态注册冒用身份（安全扫描 V-1）</summary>
public class AppLoginSecurityTests
{
    private readonly TestServer _server;

    public AppLoginSecurityTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private Task<String> Login(String appId, String secret) => LoginWith(appId, secret, null, null, null);

    private async Task<String> LoginWith(String appId, String secret, String nodeCode, String uuid, String macs)
    {
        var client = _server.CreateClient();
        var model = new { AppId = appId, Secret = secret, NodeCode = nodeCode, UUID = uuid, Macs = macs };
        var content = JsonContent.Create(model);
        var response = await client.PostAsync("app/login", content);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "已存在应用错误密钥登录被拒绝，不泄露令牌与真实密钥")]
    public async Task Login_ExistingApp_WrongSecret_Rejected()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-abc-123", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "wrong-secret");

            // 不得泄露真实密钥
            Assert.DoesNotContain("secret-abc-123", body, StringComparison.OrdinalIgnoreCase);
            // 不得签发令牌，应返回业务错误
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "已存在应用空密钥登录被拒绝")]
    public async Task Login_ExistingApp_EmptySecret_Rejected()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-abc-456", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "");

            // 不得泄露真实密钥，应返回业务错误
            Assert.DoesNotContain("secret-abc-456", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "已存在应用正确密钥登录成功（回归保护）")]
    public async Task Login_ExistingApp_CorrectSecret_Ok()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-ok-789", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "secret-ok-789");

            // 正确密钥应正常签发令牌
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "全新应用自动注册仍可用（回归保护）")]
    public async Task Login_NewApp_AutoRegister_Ok()
    {
        var name = "sec_" + Rand.NextString(8);
        try
        {
            var body = await Login(name, "");

            // 新应用自动注册仍应签发令牌并下发新密钥
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"secret\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            App.FindByName(name)?.Delete();
        }
    }

    [Fact(DisplayName = "收敛窗口模式下，空密钥登录恢复旧行为并下发密钥")]
    public async Task Login_ExistingApp_AutoMode_EmptySecret_Recover()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-rec-001", Enable = true, ReRegister = ReRegisterModes.Auto };
        app.Insert();
        try
        {
            var body = await Login(name, "");

            // 收敛窗口开启时按旧逻辑放行：签发令牌并回传真实密钥，供新实例自动接入
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret-rec-001", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "收敛窗口模式下，错误密钥登录恢复旧行为并下发密钥")]
    public async Task Login_ExistingApp_AutoMode_WrongSecret_Recover()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-rec-002", Enable = true, ReRegister = ReRegisterModes.Auto };
        app.Insert();
        try
        {
            var body = await Login(name, "wrong-secret");

            // 收敛窗口开启时按旧逻辑放行：签发令牌并回传真实密钥
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret-rec-002", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "已存在应用默认关闭重注册，错误密钥登录仍被拒绝（回归保护）")]
    public async Task Login_ExistingApp_DefaultOff_Rejected()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-rec-003", Enable = true };
        app.Insert();
        try
        {
            var body = await Login(name, "wrong-secret");

            // 默认（ReRegister=关闭）维持安全收紧：不得泄露真实密钥，不得签发令牌
            Assert.DoesNotContain("secret-rec-003", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "收敛窗口模式下，正确密钥登录成功自动关闭窗口")]
    public async Task Login_ExistingApp_AutoMode_CorrectSecret_AutoClose()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-ok-acc", Enable = true, ReRegister = ReRegisterModes.Auto };
        app.Insert();
        try
        {
            var body = await Login(name, "secret-ok-acc");

            // 正确密钥登录成功并下发令牌
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);

            // 密钥配对成功，收敛窗口自动关闭（不再出现在窗口集合）
            Assert.DoesNotContain(name, App.FindAllReRegisterWindow().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "开放模式下，空密钥登录领取密钥后仍保持开放")]
    public async Task Login_ExistingApp_Open_EmptySecret_StaysOpen()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-open-001", Enable = true, ReRegister = ReRegisterModes.Open };
        app.Insert();
        try
        {
            var body = await Login(name, "");

            // 开放模式放行：签发令牌并回传真实密钥
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret-open-001", body, StringComparison.OrdinalIgnoreCase);

            // 开放模式不自动关闭，仍保持开放
            Assert.Equal(ReRegisterModes.Open, App.FindByName(name)?.ReRegister);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "可信接入模式下，陌生机器（无节点无指纹）空密钥登录被拒绝")]
    public async Task Login_ExistingApp_Trusted_UnknownMachine_Denied()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-trust-001", Enable = true, ReRegister = ReRegisterModes.Trusted };
        app.Insert();
        try
        {
            var body = await LoginWith(name, "", null, "uuid-unknown-001", null);

            // 非可信机器不得泄露真实密钥，不得签发令牌
            Assert.DoesNotContain("secret-trust-001", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "可信接入模式下，UUID命中该应用近期指纹的机器可自助接入")]
    public async Task Login_ExistingApp_Trusted_Fingerprint_Allowed()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-trust-002", Enable = true, ReRegister = ReRegisterModes.Trusted };
        app.Insert();
        var uuid = "uuid-seen-" + Rand.NextString(8);
        var online = new AppOnline { AppId = app.Id, Name = name, Client = "t_" + Guid.NewGuid().ToString("N")[..6], Uuid = uuid };
        online.Insert();
        try
        {
            var body = await LoginWith(name, "", null, uuid, null);

            // 命中近期指纹：签发令牌并回传真实密钥
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret-trust-002", body, StringComparison.OrdinalIgnoreCase);

            // 可信接入模式保持长期有效，不自动关闭
            Assert.Equal(ReRegisterModes.Trusted, App.FindByName(name)?.ReRegister);
        }
        finally
        {
            online.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "可信接入模式下，同项目受管节点可自助接入")]
    public async Task Login_ExistingApp_Trusted_Node_Allowed()
    {
        var name = "sec_" + Rand.NextString(8);
        var projectId = Rand.Next(1, 100000);
        var app = new App { Name = name, Secret = "secret-trust-003", Enable = true, ProjectId = projectId, ReRegister = ReRegisterModes.Trusted };
        app.Insert();
        var node = new Node { Code = "nt_" + Guid.NewGuid().ToString("N")[..6], Name = "trust-node", Enable = true, ProjectId = projectId };
        node.Insert();
        try
        {
            var body = await LoginWith(name, "", node.Code, null, null);

            // 同项目受管节点：签发令牌并回传真实密钥
            Assert.Contains("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret-trust-003", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            node.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "可信接入模式下，异项目节点被拒绝")]
    public async Task Login_ExistingApp_Trusted_Node_WrongProject_Denied()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-trust-004", Enable = true, ProjectId = 900001, ReRegister = ReRegisterModes.Trusted };
        app.Insert();
        var node = new Node { Code = "nt_" + Guid.NewGuid().ToString("N")[..6], Name = "trust-node-other", Enable = true, ProjectId = 900002 };
        node.Insert();
        try
        {
            var body = await LoginWith(name, "", node.Code, null, null);

            // 异项目节点不算可信：不得泄露真实密钥，不得签发令牌
            Assert.DoesNotContain("secret-trust-004", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"code\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            node.Delete();
            app.Delete();
        }
    }
}
