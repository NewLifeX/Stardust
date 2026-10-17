using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>注册中心流程测试。覆盖 App/RegisterService 发布服务与 App/ResolveService 发现服务</summary>
[Collection(StarTestServerCollection.Name)]
public class RegistryFlowTests : StarTestBase
{
    public RegistryFlowTests(StarTestServer fixture) : base(fixture) { }

    private async Task<String> LoginAsync(String name, String secret)
    {
        var client = _server.CreateClient();
        var resp = await client.PostAsync("app/login", JsonContent.Create(new { AppId = name, Secret = secret }));
        var body = await resp.Content.ReadAsStringAsync();

        var m = Regex.Match(body, "\"token\"\\s*:\\s*\"([^\"]+)\"");
        Assert.True(m.Success, "登录应下发令牌：" + body);

        return m.Groups[1].Value;
    }

    private static void Cleanup(String name, String secret)
    {
        var svc = Service.FindByName(name);
        if (svc != null)
        {
            foreach (var item in AppService.FindAllByService(svc.Id))
            {
                item.Delete();
            }
            foreach (var item in AppConsume.FindAllByService(svc.Id))
            {
                item.Delete();
            }
            svc.Delete();
        }

        App.FindByName(name)?.Delete();
    }

    [Fact(DisplayName = "注册服务后可被解析到服务地址")]
    public async Task RegisterThenResolve_ReturnsAddress()
    {
        var serviceName = "reg_svc_" + Rand.NextString(8);
        var appName = "reg_app_" + Rand.NextString(8);
        var secret = "reg-secret-" + Rand.NextString(6);
        var app = new App { Name = appName, Secret = secret, Enable = true };
        app.Insert();

        try
        {
            var token = await LoginAsync(appName, secret);
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            var clientId = "10.1.2.3@9527";

            // 发布服务
            var registerResp = await client.PostAsync("App/RegisterService", JsonContent.Create(new
            {
                ServiceName = serviceName,
                ClientId = clientId,
                IP = "10.1.2.3",
                Version = "1.0.0",
                Address = "http://*:8080",
            }));
            var registerBody = await registerResp.Content.ReadAsStringAsync();

            Assert.True(registerResp.IsSuccessStatusCode, "注册服务应成功，实际响应：" + registerBody);
            Assert.Contains(serviceName, registerBody);
            Assert.Contains("8080", registerBody);

            // 落库校验
            var info = Service.FindByName(serviceName);
            Assert.NotNull(info);
            var svc = AppService.FindAllByService(info!.Id).FirstOrDefault(e => e.AppId == app.Id);
            Assert.NotNull(svc);
            Assert.True(svc!.Healthy);
            Assert.True(svc.Enable);

            // 发现服务
            var resolveResp = await client.PostAsync("App/ResolveService", JsonContent.Create(new
            {
                ServiceName = serviceName,
                ClientId = clientId,
            }));
            var resolveBody = await resolveResp.Content.ReadAsStringAsync();

            Assert.True(resolveResp.IsSuccessStatusCode, "解析服务应成功，实际响应：" + resolveBody);
            Assert.Contains(serviceName, resolveBody);
            Assert.Contains("8080", resolveBody);
        }
        finally
        {
            Cleanup(serviceName, secret);
        }
    }
}
