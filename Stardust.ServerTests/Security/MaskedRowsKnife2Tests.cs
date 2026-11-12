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
/// task-95 二期刀2（2026-11-12，盘点稿 Doc/安全-02 §4 刀2）：App.Secret/WebHook 行级脱敏。
/// 前置：刀1 预览链已改道 /Config/GetAllByApp（行内 Secret 零消费方）；
/// F3 先例：apps 编辑=partial-PUT，Secret 留空不提交、WebHook 不在 Vue 表单（不 Dirty）→ 行裁不破编辑流；
/// Valid "*****" 守卫（AppDeploy 先例）保 MVC 编辑表单回填原样提交不写坏。
/// 反射链与 MaskedRowsKnife0Tests 同源；DbContext 为 ServerTests 隔离 SQLite。
/// </summary>
public class MaskedRowsKnife2Tests
{
    private static Type AppControllerType =>
        Type.GetType("Stardust.Web.Areas.Registry.Controllers.AppController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi AppController not found");

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

    [Fact(DisplayName = "task-95 刀2：App 列表行 Secret/WebHook 键在值 null（含单实体分支）")]
    public void App_Index_Row_Secret_WebHook_Should_Be_Masked()
    {
        var tag = "mw2a-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.App
            {
                Name = tag + "-app", DisplayName = "刀2造数", Category = "web",
                Secret = "sec-" + tag, WebHook = "http://hook/" + tag, Enable = true,
            };
            e.Insert();

            var rows = InvokeIndexRows(AppControllerType, "/api/Registry/App");
            var mine = rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            AssertMasked(mine, "secret", "App");
            AssertMasked(mine, "webHook", "App");
        }
        finally { CleanupRaw($"Delete From StarApp Where Name='{tag}-app'"); }
    }

    [Fact(DisplayName = "task-95 刀2：App Detail 裁 Secret/WebHook；Valid GET 回显 '*****'（MVC 编辑表单输入侧）")]
    public void App_Detail_Crops_And_ValidGet_Echoes_Mask()
    {
        var tag = "mw2b-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.App
            {
                Name = tag + "-app", DisplayName = "刀2造数", Category = "web",
                Secret = "sec-" + tag, WebHook = "http://hook/" + tag, Enable = true,
            };
            e.Insert();

            var res = InvokeDetail(AppControllerType, NewController(AppControllerType), e.Id.ToString());
            var data = res.GetType().GetProperty("Data")!.GetValue(res)!;
            Assert.Null(data.GetType().GetProperty("Secret")!.GetValue(data));
            Assert.Null(data.GetType().GetProperty("WebHook")!.GetValue(data));

            var echo = Stardust.Data.App.FindById(e.Id);
            InvokeValid(AppControllerType, NewController(AppControllerType), echo, DataObjectMethodType.Update, post: false);
            Assert.Equal("*****", echo.Secret);
        }
        finally { CleanupRaw($"Delete From StarApp Where Name='{tag}-app'"); }
    }

    [Fact(DisplayName = "task-95 刀2：App Valid POST 守卫——MVC 表单原样提交 '*****' 恢复原值并真写读回不变；WebHook 不置脏不受影响")]
    public void App_Valid_Post_MaskedSecret_Restores_Old()
    {
        var tag = "mw2c-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.App
            {
                Name = tag + "-app", DisplayName = "刀2造数", Category = "web",
                Secret = "real-" + tag, WebHook = "http://keep/" + tag, Enable = true,
            };
            e.Insert();

            // 模拟 MVC 编辑表单：Secret 回显 "*****" 原样提交；WebHook 服务端表单原值提交（非掩码）
            var put = new Stardust.Data.App
            {
                Id = e.Id, Name = e.Name, DisplayName = e.DisplayName, Category = e.Category,
                Secret = "*****", WebHook = e.WebHook, Enable = true,
            };
            InvokeValid(AppControllerType, NewController(AppControllerType), put, DataObjectMethodType.Update, post: true);

            Assert.Equal("real-" + tag, put.Secret);
            Assert.Equal("http://keep/" + tag, put.WebHook);

            put.Update();
            var back = Stardust.Data.App.FindById(e.Id);
            Assert.Equal("real-" + tag, back.Secret);
            Assert.Equal("http://keep/" + tag, back.WebHook);
        }
        finally { CleanupRaw($"Delete From StarApp Where Name='{tag}-app'"); }
    }
}
