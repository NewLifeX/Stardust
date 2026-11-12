using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife.Web;
using Xunit;
using XCode;

namespace ServerTest.Security;

/// <summary>
/// task-95 二期刀0（2026-11-12，盘点稿 Doc/安全-02 §4 刀0+N1，lead 核准令）：
/// MySqlNode/RedisNode 敏感三键（Password/UserName/WebHook）行级脱敏 + Valid "*****" 守卫；
/// Node.WebHook 行/详情脱敏（N1）。静默清库三形态中 F1（detail 全量展开 PUT）的消解实证：
/// 提交 "*****"（Detail 裁剪后前端原样回传）→ Valid 从库恢复原值并清脏标记 → 真写读回不变。
/// 反射链与 AppPipelineApiListSerializationTests 同构（Index: new Pager → SearchData →
/// OnFillListValues → OnJsonSerialize 生产同参）；DbContext 为 ServerTests 隔离 SQLite。
/// </summary>
public class MaskedRowsKnife0Tests
{
    private static Type MySqlNodeControllerType =>
        Type.GetType("Stardust.Web.Areas.MySql.Controllers.MySqlNodeController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi MySqlNodeController not found");

    private static Type RedisNodeControllerType =>
        Type.GetType("Stardust.Web.Areas.Redis.Controllers.RedisNodeController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi RedisNodeController not found");

    private static Type NodeControllerType =>
        Type.GetType("Stardust.Web.Areas.Nodes.Controllers.NodeController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi NodeController not found");

    #region 反射基建（与 AppPipelineApiListSerializationTests 同源）

    private static Object NewController(Type type)
    {
        foreach (var ctor in type.GetConstructors())
        {
            var ps = ctor.GetParameters();
            if (ps.Any(p => p.ParameterType.IsValueType && !p.HasDefaultValue)) continue;
            return Activator.CreateInstance(type, ps.Select(_ => (Object?)null).ToArray())!;
        }
        return Activator.CreateInstance(type)!;
    }

    private static void InjectQuery(Object controller, String path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = path;
        typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
            .SetValue(controller, new ControllerContext { HttpContext = http });
    }

    private static IList<Dictionary<String, JsonElement>> InvokeIndexRows(Type type, String path)
    {
        var controller = NewController(type);
        InjectQuery(controller, path);

        var pager = new Pager { PageIndex = 0, PageSize = 20, RetrieveTotalCount = true };

        var searchData = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(m => m.Name == "SearchData" && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(Pager));
        var list = searchData.Invoke(controller, new Object[] { pager })!;

        var fillMethod = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(m => m.Name == "OnFillListValues" && m.GetParameters().Length == 2);
        fillMethod.Invoke(controller, new[] { list, (Object?)null });

        MethodInfo? ser = null;
        for (var t = type; t != null && ser == null; t = t.BaseType)
            ser = t.GetMethod("OnJsonSerialize", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Object) }, null);
        Assert.NotNull(ser);
        var json = (String)ser!.Invoke(controller, new[] { list })!;

        using var doc = JsonDocument.Parse(json);
        var rows = new List<Dictionary<String, JsonElement>>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var dict = new Dictionary<String, JsonElement>(StringComparer.Ordinal);
            foreach (var prop in row.EnumerateObject()) dict[prop.Name] = prop.Value.Clone();
            rows.Add(dict);
        }
        return rows;
    }

    /// <summary>task-94 负断言：键在（值级置 null 语义）且值为 JSON null。</summary>
    private static void AssertMasked(IList<Dictionary<String, JsonElement>> rows, String key, String ctx)
    {
        Assert.All(rows, r => Assert.True(r.ContainsKey(key),
            $"{ctx}: 行 JSON 缺键 '{key}'（值级置 null 语义要求键不删）；实际键=[{String.Join(",", r.Keys)}]"));
        Assert.All(rows, r => Assert.True(r[key].ValueKind == JsonValueKind.Null,
            $"{ctx}: 行 JSON '{key}' 值未脱敏（期望 JSON null，实际 {r[key].ValueKind}）"));
    }

    private static Object InvokeDetail(Type type, Object controller, String id)
    {
        var m = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(x => x.Name == "Detail" && x.GetParameters().Length == 1
                && x.GetParameters()[0].ParameterType == typeof(String));
        return m.Invoke(controller, new Object[] { id })!;
    }

    /// <summary>反射调 Valid（protected override Boolean Valid(T, DataObjectMethodType, Boolean)）。</summary>
    private static void InvokeValid(Type type, Object controller, Object entity, DataObjectMethodType mtype, Boolean post)
    {
        var m = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .First(x => x.Name == "Valid" && x.GetParameters().Length == 3
                && x.GetParameters()[2].ParameterType == typeof(Boolean));
        m.Invoke(controller, new[] { entity, (Object)mtype, (Object)post });
    }

    private static void CleanupRaw(params String[] sqls)
    {
        var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
        foreach (var sql in sqls) dal.Session.Execute(sql);
    }

    #endregion

    [Fact(DisplayName = "task-95 刀0：MySqlNode 列表行 Password/UserName/WebHook 键在值 null（含单实体分支）")]
    public void MySqlNode_Index_Row_Sensitive_Three_Should_Be_Masked()
    {
        var tag = "mw0my-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Nodes.MySqlNode
            {
                Name = tag + "-n1", Server = "127.0.0.1", Port = 3306,
                UserName = "root-" + tag, Password = "pw-" + tag, WebHook = "http://hook/" + tag,
                DatabaseName = "mysql", Version = "8.0", Enable = true,
            };
            e.Insert();

            var rows = InvokeIndexRows(MySqlNodeControllerType, "/api/MySql/MySqlNode");
            var mine = rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            AssertMasked(mine, "password", "MySqlNode");
            AssertMasked(mine, "userName", "MySqlNode");
            AssertMasked(mine, "webHook", "MySqlNode");
        }
        finally { CleanupRaw($"Delete From MySqlNode Where Name='{tag}-n1'"); }
    }

    [Fact(DisplayName = "task-95 刀0：MySqlNode Detail 裁敏感三键且 Valid GET 回显 '*****'（F1 消解输入侧）")]
    public void MySqlNode_Detail_Crops_And_ValidGet_Echoes_Mask()
    {
        var tag = "mw0md-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Nodes.MySqlNode
            {
                Name = tag + "-n1", Server = "127.0.0.1", Port = 3306,
                UserName = "root-" + tag, Password = "pw-" + tag, WebHook = "http://hook/" + tag,
                Enable = true,
            };
            e.Insert();

            var res = InvokeDetail(MySqlNodeControllerType, NewController(MySqlNodeControllerType), e.Id.ToString());
            var data = res.GetType().GetProperty("Data")!.GetValue(res)!;
            Assert.Null(data.GetType().GetProperty("Password")!.GetValue(data));
            Assert.Null(data.GetType().GetProperty("UserName")!.GetValue(data));
            Assert.Null(data.GetType().GetProperty("WebHook")!.GetValue(data));

            // GET 回显（MVC 编辑表单与前端回填同源）：非空 → "*****"
            var getEcho = Stardust.Data.Nodes.MySqlNode.FindById(e.Id);
            InvokeValid(MySqlNodeControllerType, NewController(MySqlNodeControllerType), getEcho, DataObjectMethodType.Update, post: false);
            Assert.Equal("*****", getEcho.Password);
            Assert.Equal("*****", getEcho.WebHook);
            Assert.Equal("root-" + tag, getEcho.UserName);
        }
        finally { CleanupRaw($"Delete From MySqlNode Where Name='{tag}-n1'"); }
    }

    [Fact(DisplayName = "task-95 刀0：MySqlNode Valid POST 守卫——提交 '*****' 恢复原值并真写读回不变（静默清库消解实证）")]
    public void MySqlNode_Valid_Post_MaskedValue_Restores_Old()
    {
        var tag = "mw0mg-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Nodes.MySqlNode
            {
                Name = tag + "-n1", Server = "127.0.0.1", Port = 3306,
                Password = "real-" + tag, WebHook = "http://keep/" + tag, Enable = true,
            };
            e.Insert();

            // 模拟 F1 提交：Detail 全量展开（Password/WebHook 已被裁成 "*****" 回显）+ 表单未改
            var put = new Stardust.Data.Nodes.MySqlNode
            {
                Id = e.Id, Name = e.Name, Server = e.Server, Port = e.Port,
                Password = "*****", WebHook = "*****", Enable = true,
            };
            InvokeValid(MySqlNodeControllerType, NewController(MySqlNodeControllerType), put, DataObjectMethodType.Update, post: true);

            Assert.Equal("real-" + tag, put.Password);
            Assert.Equal("http://keep/" + tag, put.WebHook);

            put.Update();
            var back = Stardust.Data.Nodes.MySqlNode.FindById(e.Id);
            Assert.Equal("real-" + tag, back.Password);
            Assert.Equal("http://keep/" + tag, back.WebHook);
        }
        finally { CleanupRaw($"Delete From MySqlNode Where Name='{tag}-n1'"); }
    }

    [Fact(DisplayName = "task-95 刀0：RedisNode 列表行 Password/UserName/WebHook 键在值 null")]
    public void RedisNode_Index_Row_Sensitive_Three_Should_Be_Masked()
    {
        var tag = "mw0rd-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Nodes.RedisNode
            {
                Name = tag + "-n1", Server = $"127.0.0.1:{Random.Shared.Next(20000, 60000)}", Category = "缓存",
                UserName = "default-" + tag, Password = "pw-" + tag, WebHook = "http://hook/" + tag,
                Version = "7.2", Mode = "standalone", Enable = true,
            };
            e.Insert();

            var rows = InvokeIndexRows(RedisNodeControllerType, "/api/Redis/RedisNode");
            var mine = rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            AssertMasked(mine, "password", "RedisNode");
            AssertMasked(mine, "userName", "RedisNode");
            AssertMasked(mine, "webHook", "RedisNode");
        }
        finally { CleanupRaw($"Delete From RedisNode Where Name='{tag}-n1'"); }
    }

    [Fact(DisplayName = "task-95 刀0：RedisNode Valid POST 守卫——提交 '*****' 恢复原值并真写读回不变")]
    public void RedisNode_Valid_Post_MaskedValue_Restores_Old()
    {
        var tag = "mw0rg-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var server = $"127.0.0.1:{Random.Shared.Next(20000, 60000)}";
            var e = new Stardust.Data.Nodes.RedisNode
            {
                Name = tag + "-n1", Server = server, Category = "缓存",
                Password = "real-" + tag, WebHook = "http://keep/" + tag, Enable = true,
            };
            e.Insert();

            var put = new Stardust.Data.Nodes.RedisNode
            {
                Id = e.Id, Name = e.Name, Server = server, Category = e.Category,
                Password = "*****", WebHook = "*****", Enable = true,
            };
            InvokeValid(RedisNodeControllerType, NewController(RedisNodeControllerType), put, DataObjectMethodType.Update, post: true);

            Assert.Equal("real-" + tag, put.Password);
            Assert.Equal("http://keep/" + tag, put.WebHook);

            put.Update();
            var back = Stardust.Data.Nodes.RedisNode.FindById(e.Id);
            Assert.Equal("real-" + tag, back.Password);
            Assert.Equal("http://keep/" + tag, back.WebHook);
        }
        finally { CleanupRaw($"Delete From RedisNode Where Name='{tag}-n1'"); }
    }

    [Fact(DisplayName = "task-95 刀0/N1：Node 列表行 Secret/WebHook 键在值 null；Detail 裁 WebHook 保 Secret（MVC 编辑/注册流程）")]
    public void Node_Row_Secret_WebHook_Masked_And_Detail_Keeps_Secret_Crops_WebHook()
    {
        var tag = "mw0nd-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Nodes.Node
            {
                Name = tag + "-node", Code = tag + "-code", Secret = "sec-" + tag,
                WebHook = "http://hook/" + tag, Enable = true,
            };
            e.Insert();

            var rows = InvokeIndexRows(NodeControllerType, "/api/Nodes/Node");
            var mine = rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            AssertMasked(mine, "secret", "Node");
            AssertMasked(mine, "webHook", "Node");

            // Detail：WebHook 裁剪（N1），Secret 保守保留（一期裁定：详情/注册流程在用）
            var res = InvokeDetail(NodeControllerType, NewController(NodeControllerType), e.ID.ToString());
            var data = res.GetType().GetProperty("Data")!.GetValue(res)!;
            Assert.Equal("sec-" + tag, data.GetType().GetProperty("Secret")!.GetValue(data));
            Assert.Null(data.GetType().GetProperty("WebHook")!.GetValue(data));
        }
        finally { CleanupRaw($"Delete From Node Where Name='{tag}-node'"); }
    }
}
