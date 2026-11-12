using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Web;
using Xunit;
using XCode;

namespace ServerTest.Security;

/// <summary>
/// task-95 二期刀3/刀4（2026-11-12，盘点稿 Doc/安全-02 §4 刀3/4）：AlarmGroup/AppTracer WebHook 行级掩码+F2 消解。
/// 刀3/4 偏离一期「值级 null」惯例（lead 刀6 "••••••" 同款哨兵）：F2=前端编辑抽屉从列表行回填且 PUT 恒带
/// webHook 键（alarmGroup.logic groupBody / appTracer.logic tracerPutBody）——null 回填→PUT 空→静默清库；
/// 哨兵回填→原样提交→Valid POST 守卫识别还原。断言=键在值等于哨兵（非 null）。
/// 反射链与 MaskedRowsKnife0Tests 同源；DbContext 为 ServerTests 隔离 SQLite。
/// </summary>
public class MaskedRowsKnife34Tests
{
    private const String Mask = "••••••";

    private static (Type type, String asm) Controller(string area, string name) =>
        (Type.GetType($"Stardust.Web.Areas.{area}.Controllers.{name}, StarWebApi")
            ?? throw new InvalidOperationException($"{name} not found"), name);

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

    /// <summary>裸 SQL 标量读回（绕开 XCode 实体缓存，验证库中真值）</summary>
    private static String QueryScalar(String sql)
    {
        var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
        var ds = dal.Session.Query(sql);
        var dt = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : null;
        if (dt == null || dt.Rows.Count == 0) return "";
        return dt.Rows[0][0]?.ToString() ?? "";
    }

    private static void AssertSentinel(IList<Dictionary<String, JsonElement>> rows, String key, String ctx)
    {
        Assert.All(rows, r => Assert.True(r.ContainsKey(key), $"{ctx}: 行 JSON 缺键 '{key}'"));
        Assert.All(rows, r => Assert.True(r[key].ValueKind == JsonValueKind.String && r[key].GetString() == Mask,
            $"{ctx}: 行 JSON '{key}' 非掩码哨兵（期望 \"{Mask}\"，实际 {r[key].ValueKind}:{r[key].GetString()}）"));
    }

    [Fact(DisplayName = "task-95 刀3：AlarmGroup 列表行 WebHook=掩码哨兵（F2 哨兵语义，非 null）")]
    public void AlarmGroup_Index_WebHook_Sentinel()
    {
        var tag = "mw3a-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Monitors.AlarmGroup
            {
                Name = tag + "-grp", WebHook = "https://hook.example/" + tag, Enable = true,
            };
            e.Insert();

            var rows = InvokeIndexRows(Controller("Monitors", "AlarmGroupController").type, "/api/Monitors/AlarmGroup");
            var mine = rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*");

            AssertSentinel(mine, "webHook", "AlarmGroup");
        }
        finally { CleanupRaw($"Delete From AlarmGroup Where Name='{tag}-grp'"); }
    }

    [Fact(DisplayName = "task-95 刀3：AlarmGroup Detail 掩码；Valid GET 回显哨兵；POST 守卫真写读回原值；空值放行=显式清空")]
    public void AlarmGroup_Detail_Echo_Guard()
    {
        var tag = "mw3b-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Monitors.AlarmGroup
            {
                Name = tag + "-grp", WebHook = "https://keep.example/" + tag, Enable = true,
            };
            e.Insert();

            var res = InvokeDetail(Controller("Monitors", "AlarmGroupController").type, NewController(Controller("Monitors", "AlarmGroupController").type), e.Id.ToString());
            var data = res.GetType().GetProperty("Data")!.GetValue(res)!;
            Assert.Equal(Mask, data.GetType().GetProperty("WebHook")!.GetValue(data));

            // POST 守卫（先做：避免 GET 回显步改写 XCode 实体缓存共享实例污染恢复源）。
            // put 用新实体（App 刀2 T3 同模式，initializer 置脏）；恢复源 FindById=库值（缓存未污染）
            var put = new Stardust.Data.Monitors.AlarmGroup
            {
                Id = e.Id, Name = e.Name, WebHook = Mask, Enable = false,
            };
            InvokeValid(Controller("Monitors", "AlarmGroupController").type, NewController(Controller("Monitors", "AlarmGroupController").type), put, DataObjectMethodType.Update, post: true);
            Assert.Equal("https://keep.example/" + tag, put.WebHook);
            put.Update();
            Assert.Equal("https://keep.example/" + tag, QueryScalar($"Select WebHook From AlarmGroup Where Id={e.Id}"));

            // GET 回显（MVC 编辑表单输入侧；置后于守卫防缓存污染恢复源，先于清空步）
            var echo = Stardust.Data.Monitors.AlarmGroup.FindById(e.Id);
            InvokeValid(Controller("Monitors", "AlarmGroupController").type, NewController(Controller("Monitors", "AlarmGroupController").type), echo, DataObjectMethodType.Update, post: false);
            Assert.Equal(Mask, echo.WebHook);

            // 空值放行：显式清空（空值不走哨兵分支直通写库）
            var clear = new Stardust.Data.Monitors.AlarmGroup
            {
                Id = e.Id, Name = e.Name, WebHook = "", Enable = true,
            };
            InvokeValid(Controller("Monitors", "AlarmGroupController").type, NewController(Controller("Monitors", "AlarmGroupController").type), clear, DataObjectMethodType.Update, post: true);
            clear.Update();
            Assert.True($"{QueryScalar($"Select WebHook From AlarmGroup Where Id={e.Id}")}".IsNullOrEmpty());
        }
        finally { CleanupRaw($"Delete From AlarmGroup Where Name='{tag}-grp'"); }
    }

    [Fact(DisplayName = "task-95 刀4：AppTracer 列表行/Detail WebHook=掩码哨兵 + Valid 守卫真写读回")]
    public void AppTracer_Row_Detail_Guard()
    {
        var tag = "mw4-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Monitors.AppTracer
            {
                Name = tag + "-trc", WebHook = "https://tracer.example/" + tag, Enable = true,
            };
            e.Insert();

            var rows = InvokeIndexRows(Controller("Monitors", "AppTracerController").type, "/api/Monitors/AppTracer");
            var mine = rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*");
            AssertSentinel(mine, "webHook", "AppTracer");

            var res = InvokeDetail(Controller("Monitors", "AppTracerController").type, NewController(Controller("Monitors", "AppTracerController").type), e.ID.ToString());
            var data = res.GetType().GetProperty("Data")!.GetValue(res)!;
            Assert.Equal(Mask, data.GetType().GetProperty("WebHook")!.GetValue(data));

            var put = Stardust.Data.Monitors.AppTracer.FindByKey(e.ID);
            put.WebHook = Mask;
            InvokeValid(Controller("Monitors", "AppTracerController").type, NewController(Controller("Monitors", "AppTracerController").type), put, DataObjectMethodType.Update, post: true);
            Assert.Equal("https://tracer.example/" + tag, put.WebHook);
        }
        finally { CleanupRaw($"Delete From AppTracer Where Name='{tag}-trc'"); }
    }
}
