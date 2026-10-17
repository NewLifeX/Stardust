using System;
using System.Collections.Generic;
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
using Stardust.Data.Configs;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>配置中心写入测试。覆盖 Config/SetAll 落库与读取回环，以及只读应用拒绝写入</summary>
[Collection(StarTestServerCollection.Name)]
public class ConfigSetAllTests : StarTestBase
{
    public ConfigSetAllTests(StarTestServer fixture) : base(fixture) { }

    /// <summary>创建测试应用，返回应用名与密钥</summary>
    private static (String name, String secret) CreateApp()
    {
        var name = "csa_" + Rand.NextString(8);
        var secret = "csa-secret-" + Rand.NextString(6);
        var app = new App { Name = name, Secret = secret, Enable = true };
        app.Insert();

        return (name, secret);
    }

    private static void Cleanup(String name)
    {
        var config = AppConfig.FindByName(name);
        if (config != null)
        {
            foreach (var item in ConfigData.FindAllByApp(config.Id))
            {
                item.Delete();
            }
            config.Delete();
        }

        App.FindByName(name)?.Delete();
    }

    [Fact(DisplayName = "批量设置配置后可通过拉取配置读回")]
    public async Task SetAll_ThenGetAll_RoundTrip()
    {
        var (name, secret) = CreateApp();
        try
        {
            var key = "cs_key_" + Rand.NextString(4);
            var value = "cs_value_" + Rand.NextString(4);

            var client = _server.CreateClient();
            var configs = new Dictionary<String, Object> { [key] = value };
            var setResp = await client.PostAsync($"/Config/SetAll?appId={name}&secret={secret}",
                JsonContent.Create(new { AppId = name, Secret = secret, Configs = configs }));
            var setBody = await setResp.Content.ReadAsStringAsync();

            Assert.True(setResp.IsSuccessStatusCode, "设置配置应成功，实际响应：" + setBody);
            Assert.Equal("1", ExtractData(setBody));

            // 落库校验：新配置进入"未发布"的新版本
            var config = AppConfig.FindByName(name);
            Assert.NotNull(config);
            var data = ConfigData.FindAllByApp(config!.Id).FirstOrDefault(e => e.Key == key);
            Assert.NotNull(data);
            Assert.Equal(value, data!.Value);
            Assert.True(data.Version > 0);

            // 发布后拉取配置应能读回刚写入的键值
            config.Publish();

            var getResp = await client.GetAsync($"/Config/GetAll?appId={name}&secret={secret}");
            var getBody = await getResp.Content.ReadAsStringAsync();

            Assert.True(getResp.IsSuccessStatusCode, "拉取配置应成功，实际响应：" + getBody);
            Assert.Contains(key, getBody);
            Assert.Contains(value, getBody);
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact(DisplayName = "只读应用禁止批量设置配置")]
    public async Task SetAll_ReadonlyApp_Rejected()
    {
        var (name, secret) = CreateApp();
        try
        {
            var client = _server.CreateClient();

            // 首次拉取会按应用名自动创建 AppConfig
            await client.GetAsync($"/Config/GetAll?appId={name}&secret={secret}");

            var config = AppConfig.FindByName(name);
            Assert.NotNull(config);
            config!.Readonly = true;
            config.Update();

            var configs = new Dictionary<String, Object> { ["cs_ro"] = "1" };
            var resp = await client.PostAsync($"/Config/SetAll?appId={name}&secret={secret}",
                JsonContent.Create(new { AppId = name, Secret = secret, Configs = configs }));
            var body = await resp.Content.ReadAsStringAsync();

            Assert.Contains("只读", body);
        }
        finally
        {
            Cleanup(name);
        }
    }

    private static String ExtractData(String body)
    {
        var m = Regex.Match(body, "\"data\"\\s*:\\s*([0-9]+)");
        return m.Success ? m.Groups[1].Value : null;
    }
}
