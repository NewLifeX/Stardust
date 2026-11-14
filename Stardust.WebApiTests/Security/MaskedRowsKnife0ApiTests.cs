using System.Text.Json;
using Stardust.Data.Nodes;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Security;

/// <summary>
/// task-95 二期刀0（2026-11-12，盘点稿 Doc/安全-02 §4 刀0+N1，lead 核准令）HTTP 集成式迁移（2026-11-14 Q2 裁决）：
/// MySqlNode/RedisNode 敏感三键（Password/UserName/WebHook）行级脱敏 + Valid "*****" 守卫；Node.WebHook 行/详情脱敏（N1）。
/// 静默清库三形态中 F1（detail 全量展开 PUT）的消解实证：提交 "*****"（Detail 裁剪后前端原样回传）→
/// Valid 从库恢复原值并清脏标记 → 真写读回不变。
/// 与原 ServerTests 反射版（SearchData→OnFillListValues→OnJsonSerialize 链）差异：
/// ①全部走真实 HTTP 管道（TestServer 进程内）；②Valid GET 回显（MVC 编辑表单输入侧）非 API 面，不再断言；
/// ③读回断言走实体 Find 直连查询（绕过实体缓存，CertPfx 先例）。
/// ④新管线（Cube 1105 字段集合投影）契约定谳（2026-11-14 首轮实测）：meta 字段集已裁剪的敏感键
/// （MySql/Redis 的 Password/UserName/WebHook、Node 的 Secret/WebHook）在行 JSON 中**键级缺席**——
/// 比「键在值 null」更强的防泄漏；掩码层只在键仍在字段集时生效（值 null/哨兵）。
/// 掩码机制（2026-11-14 收敛后）：WebEntityController 薄基类 GetRowMasks 声明式规则 + OnProject 统一应用。
/// </summary>
[Collection(WebApiCollection.Name)]
public class MaskedRowsKnife0ApiTests
{
    private readonly ApiClient _api;
    private readonly ITestOutputHelper _output;

    public MaskedRowsKnife0ApiTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _api = new(factory, output);
        _output = output;
    }

    private const String Mask = "*****";

    [Fact(DisplayName = "task-95 刀0：MySqlNode 列表行 Password/UserName/WebHook 键级缺席（meta 已裁，明文零泄漏）")]
    public async Task MySqlNode_Index_Row_Sensitive_Three_Should_Be_Masked()
    {
        var tag = "mw0my-" + Guid.NewGuid().ToString("N")[..8];
        var e = new MySqlNode
        {
            Name = tag + "-n1", Server = "127.0.0.1", Port = 3306,
            UserName = "root-" + tag, Password = "pw-" + tag, WebHook = "http://hook/" + tag,
            DatabaseName = "mysql", Version = "8.0", Enable = true,
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, $"MySql/MySqlNode?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.KeyAbsent(mine, "password", "MySqlNode");
            ApiRowAssert.KeyAbsent(mine, "userName", "MySqlNode");
            ApiRowAssert.KeyAbsent(mine, "webHook", "MySqlNode");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀0：MySqlNode Detail——Password 键缺席（写入口令不投影），UserName/WebHook 键在值 null")]
    public async Task MySqlNode_Detail_Crops_Sensitive_Three()
    {
        var tag = "mw0md-" + Guid.NewGuid().ToString("N")[..8];
        var e = new MySqlNode
        {
            Name = tag + "-n1", Server = "127.0.0.1", Port = 3306,
            UserName = "root-" + tag, Password = "pw-" + tag, WebHook = "http://hook/" + tag,
            Enable = true,
        };
        e.Insert();
        try
        {
            var api = await _api.GetJsonAsync($"MySql/MySqlNode/{e.Id}");

            ApiRowAssert.DetailHidden(api.Data, "password", "MySqlNode");
            ApiRowAssert.DetailMasked(api.Data, "userName", "MySqlNode");
            ApiRowAssert.DetailMasked(api.Data, "webHook", "MySqlNode");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀0：MySqlNode F1 提交——PUT '*****' 守卫恢复原值并真写读回不变（静默清库消解实证）")]
    public async Task MySqlNode_Put_MaskedValue_Restores_Old()
    {
        var tag = "mw0mg-" + Guid.NewGuid().ToString("N")[..8];
        var e = new MySqlNode
        {
            Name = tag + "-n1", Server = "127.0.0.1", Port = 3306,
            UserName = "root-" + tag, Password = "real-" + tag, WebHook = "http://keep/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            // 模拟 F1 提交：Detail 全量展开（Password/WebHook 已被裁）+ 表单未改 → 原样回传 "*****"
            var put = new
            {
                id = e.Id,
                name = e.Name,
                server = e.Server,
                port = e.Port,
                password = Mask,
                webHook = Mask,
                enable = true,
            };
            var api = await _api.PutApiAsync<JsonElement>($"MySql/MySqlNode/{e.Id}", put);

            var back = MySqlNode.FindById(e.Id);
            Assert.NotNull(back);
            Assert.Equal("real-" + tag, back.Password);
            Assert.Equal("http://keep/" + tag, back.WebHook);
            Assert.Equal("root-" + tag, back.UserName);   // PUT 未提交 userName → 保持库值
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀0：RedisNode 列表行 Password/UserName/WebHook 键级缺席（meta 已裁）")]
    public async Task RedisNode_Index_Row_Sensitive_Three_Should_Be_Masked()
    {
        var tag = "mw0rd-" + Guid.NewGuid().ToString("N")[..8];
        var e = new RedisNode
        {
            Name = tag + "-n1", Server = $"127.0.0.1:{Random.Shared.Next(20000, 60000)}", Category = "缓存",
            UserName = "default-" + tag, Password = "pw-" + tag, WebHook = "http://hook/" + tag,
            Version = "7.2", Mode = "standalone", Enable = true,
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, $"Redis/RedisNode?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.KeyAbsent(mine, "password", "RedisNode");
            ApiRowAssert.KeyAbsent(mine, "userName", "RedisNode");
            ApiRowAssert.KeyAbsent(mine, "webHook", "RedisNode");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀0：RedisNode F1 提交——PUT '*****' 守卫恢复原值并真写读回不变")]
    public async Task RedisNode_Put_MaskedValue_Restores_Old()
    {
        var tag = "mw0rg-" + Guid.NewGuid().ToString("N")[..8];
        var server = $"127.0.0.1:{Random.Shared.Next(20000, 60000)}";
        var e = new RedisNode
        {
            Name = tag + "-n1", Server = server, Category = "缓存",
            Password = "real-" + tag, WebHook = "http://keep/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            var put = new
            {
                id = e.Id,
                name = e.Name,
                server = server,
                category = e.Category,
                password = Mask,
                webHook = Mask,
                enable = true,
            };
            var api = await _api.PutApiAsync<JsonElement>($"Redis/RedisNode/{e.Id}", put);

            var back = RedisNode.FindById(e.Id);
            Assert.NotNull(back);
            Assert.Equal("real-" + tag, back.Password);
            Assert.Equal("http://keep/" + tag, back.WebHook);
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀0/N1：Node 列表行 Secret/WebHook 键级缺席；Detail Secret/WebHook 键级缺席+WebHook 值 null（注册流程在用）")]
    public async Task Node_Row_Secret_WebHook_Masked_And_Detail_Keeps_Secret_Crops_WebHook()
    {
        var tag = "mw0nd-" + Guid.NewGuid().ToString("N")[..8];
        var e = new Node
        {
            Name = tag + "-node", Code = tag + "-code", Secret = "sec-" + tag,
            WebHook = "http://hook/" + tag, Enable = true,
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, $"Nodes/Node?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.KeyAbsent(mine, "secret", "Node");
            ApiRowAssert.KeyAbsent(mine, "webHook", "Node");

            // Detail：Secret 键级缺席（新字段集把 Secret 从详情面也裁掉——强于一期「保守保留」契约）、
            // WebHook 键在值 null（N1）
            var api = await _api.GetJsonAsync($"Nodes/Node/{e.ID}");
            ApiRowAssert.DetailHidden(api.Data, "secret", "Node");
            ApiRowAssert.DetailMasked(api.Data, "webHook", "Node");
        }
        finally { e.Delete(); }
    }
}
