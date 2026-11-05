using System;
using System.Linq;
using System.Text.Json;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Mcp;
using Stardust.Mcp.Tools.Config;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>配置读工具复用 MVC/WebApi 已有查询。
/// 列表按应用定位走 AppConfig.FindByAppId，不在工具里拼 Where，也不像管理页那样缺配置就插入。
/// 配置项走 ConfigData.Search(配置, 空键, 空作用域, MinValue, MinValue, 无关键字)，与两边 ConfigDataController.Search 相同。
/// 取值走 ConfigData.Search，键条件在 Biz 里。</summary>
public class ConfigReadReuseTests
{
    private const Int32 ProjectId = 999776;

    [Fact(DisplayName = "config_list 用 FindByAppId 定位，配置项与空条件 ConfigData.Search 同结果且不返回值；config_get 同结果并脱敏")]
    public async Task ListAndGet_ReuseBizQueries()
    {
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var plain = "plain-" + suffix;
        var secret = "secret-" + suffix;
        var app = new App { Name = "cfg-read-" + suffix, ProjectId = ProjectId, Enable = true };
        app.Insert();
        var otherApp = new App { Name = "cfg-read-other-" + suffix, ProjectId = ProjectId, Enable = true };
        otherApp.Insert();

        AppConfig cfg = null;
        AppConfig otherCfg = null;
        var ctx = new McpContext { TokenId = 999999, ProjectId = ProjectId };
        try
        {
            var before = await new ConfigListTool().InvokeAsync(JsonSerializer.SerializeToElement(new { app_id = app.Id }), ctx);
            using (var beforeDoc = JsonDocument.Parse(JsonSerializer.Serialize(before)))
            {
                Assert.Contains("还没有配置", beforeDoc.RootElement.GetProperty("summary").GetString());
                Assert.Equal(0, beforeDoc.RootElement.GetProperty("data").GetProperty("records").GetArrayLength());
            }

            cfg = new AppConfig { Name = app.Name, AppId = app.Id, ProjectId = ProjectId, Enable = true, Version = 3 };
            cfg.Insert();
            otherCfg = new AppConfig { Name = otherApp.Name, AppId = otherApp.Id, ProjectId = ProjectId, Enable = true, Version = 1 };
            otherCfg.Insert();

            var plainRow = new ConfigData { ConfigId = cfg.Id, Key = "ServerName", Value = plain, Enable = true, Version = 2 };
            var secretRow = new ConfigData { ConfigId = cfg.Id, Key = "Password", Value = secret, Enable = false, Version = 4 };
            var scopedValue = "scoped-" + suffix;
            var scopedRow = new ConfigData { ConfigId = cfg.Id, Key = "Region", Scope = "prod", Value = scopedValue, Enable = true, Version = 9 };
            var otherRow = new ConfigData { ConfigId = otherCfg.Id, Key = "OtherOnly" + suffix, Value = "other-" + suffix, Enable = true, Version = 1 };
            plainRow.Insert();
            secretRow.Insert();
            scopedRow.Insert();
            otherRow.Insert();

            var located = AppConfig.FindByAppId(app.Id);
            Assert.Equal(cfg.Id, located.Id);

            var listed = await new ConfigListTool().InvokeAsync(JsonSerializer.SerializeToElement(new { app_id = app.Id }), ctx);
            var listedJson = JsonSerializer.Serialize(listed);
            // 空键、空作用域、时间 MinValue、不按关键字。精确键、作用域、关键字都会让 Search 少返回。
            var searched = ConfigData.Search(cfg.Id, "", "", DateTime.MinValue, DateTime.MinValue, null, null)
                .OrderBy(e => e.Id)
                .Select(e => (Id: e.Id, Key: e.Key ?? "", Version: e.Version, Enable: e.Enable, Secret: McpGuard.Secret(e.Key)))
                .ToArray();
            using (var listedDoc = JsonDocument.Parse(listedJson))
            {
                var data = listedDoc.RootElement.GetProperty("data");
                Assert.Equal(cfg.Id, data.GetProperty("config_id").GetInt32());
                Assert.Equal(3, data.GetProperty("version").GetInt32());
                var records = data.GetProperty("records");
                var listedRows = records.EnumerateArray().Select(e =>
                {
                    Assert.Equal(5, e.EnumerateObject().Count());
                    Assert.False(e.TryGetProperty("Value", out _));
                    return (
                        Id: e.GetProperty("Id").GetInt32(),
                        Key: e.GetProperty("Key").GetString() ?? "",
                        Version: e.GetProperty("Version").GetInt32(),
                        Enable: e.GetProperty("Enable").GetBoolean(),
                        Secret: e.GetProperty("secret").GetBoolean());
                }).OrderBy(e => e.Id).ToArray();
                Assert.Equal(searched, listedRows);
            }
            Assert.DoesNotContain(plain, listedJson);
            Assert.DoesNotContain(secret, listedJson);
            Assert.DoesNotContain(scopedValue, listedJson);
            Assert.DoesNotContain("OtherOnly" + suffix, listedJson);
            Assert.True(searched.Length > ConfigData.Search(cfg.Id, "ServerName", "", DateTime.MinValue, DateTime.MinValue, null, null).Count);
            Assert.True(searched.Length > ConfigData.Search(cfg.Id, "", "prod", DateTime.MinValue, DateTime.MinValue, null, null).Count);
            Assert.True(searched.Length > ConfigData.Search(cfg.Id, "", "", DateTime.MinValue, DateTime.MinValue, scopedValue, null).Count);

            var all = ConfigData.Search(cfg.Id, null, "", DateTime.MinValue, DateTime.MinValue, null, null);
            var gotAll = await new ConfigGetTool().InvokeAsync(JsonSerializer.SerializeToElement(new { config_id = cfg.Id }), ctx);
            Assert.Equal(all.Count, RecordCount(gotAll));

            var one = ConfigData.Search(cfg.Id, "ServerName", "", DateTime.MinValue, DateTime.MinValue, null, null);
            var gotOne = await new ConfigGetTool().InvokeAsync(JsonSerializer.SerializeToElement(new { config_id = cfg.Id, key = "ServerName" }), ctx);
            var gotOneJson = JsonSerializer.Serialize(gotOne);
            Assert.Equal(one.Count, RecordCount(gotOne));
            Assert.Contains(plain, gotOneJson);
            Assert.DoesNotContain(secret, gotOneJson);

            // 半截键只能命中 Search 的精确键匹配。若工具把 key 当成关键字 Contains，这里会多返回 ServerName。
            var partial = ConfigData.Search(cfg.Id, "Server", "", DateTime.MinValue, DateTime.MinValue, null, null);
            var gotPartial = await new ConfigGetTool().InvokeAsync(JsonSerializer.SerializeToElement(new { config_id = cfg.Id, key = "Server" }), ctx);
            Assert.Equal(partial.Count, RecordCount(gotPartial));

            var gotSecret = await new ConfigGetTool().InvokeAsync(JsonSerializer.SerializeToElement(new { config_id = cfg.Id, key = "Password" }), ctx);
            var gotSecretJson = JsonSerializer.Serialize(gotSecret);
            Assert.Contains("\"value\":\"***\"", gotSecretJson);
            Assert.Contains("\"masked\":true", gotSecretJson);
            Assert.DoesNotContain(secret, gotSecretJson);
        }
        finally
        {
            Cleanup(cfg);
            Cleanup(otherCfg);
            otherApp.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "config_list 不返回其它项目挂在同一应用上的配置")]
    public async Task List_HidesConfigOutsideTokenProject()
    {
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var app = new App { Name = "cfg-read-xproj-" + suffix, ProjectId = ProjectId, Enable = true };
        app.Insert();
        var cfg = new AppConfig { Name = app.Name, AppId = app.Id, ProjectId = ProjectId + 1, Enable = true, Version = 1 };
        cfg.Insert();
        var row = new ConfigData { ConfigId = cfg.Id, Key = "Leak" + suffix, Value = "leak-" + suffix, Enable = true, Version = 1 };
        row.Insert();
        var ctx = new McpContext { TokenId = 999999, ProjectId = ProjectId };
        try
        {
            var listed = await new ConfigListTool().InvokeAsync(JsonSerializer.SerializeToElement(new { app_id = app.Id }), ctx);
            var json = JsonSerializer.Serialize(listed);
            using (var listedDoc = JsonDocument.Parse(json))
            {
                var summary = listedDoc.RootElement.GetProperty("summary").GetString();
                Assert.Contains("还没有配置", summary);
                Assert.DoesNotContain(app.Name, summary);
                Assert.Equal(0, listedDoc.RootElement.GetProperty("data").GetProperty("records").GetArrayLength());
            }
            Assert.DoesNotContain(app.Name, json);
            Assert.DoesNotContain("Leak" + suffix, json);
            Assert.DoesNotContain("leak-" + suffix, json);
            Assert.Equal(cfg.Id, AppConfig.FindByAppId(app.Id).Id);
            Assert.Equal("Leak" + suffix, Assert.Single(ConfigData.Search(cfg.Id, "", "", DateTime.MinValue, DateTime.MinValue, null, null)).Key);
        }
        finally
        {
            Cleanup(cfg);
            app.Delete();
        }
    }

    private static Int32 RecordCount(Object content)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(content));
        return doc.RootElement.GetProperty("data").GetProperty("records").GetArrayLength();
    }

    private static void Cleanup(AppConfig cfg)
    {
        if (cfg == null || cfg.Id <= 0) return;
        foreach (var row in ConfigData.FindAllByApp(cfg.Id).ToList()) row.Delete();
        ConfigHistory.Delete(ConfigHistory._.ConfigId == cfg.Id);
        cfg.Delete();
    }
}
