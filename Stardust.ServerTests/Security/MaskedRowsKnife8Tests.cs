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
/// task-95 二期刀8/N2（2026-11-12，lead 放行令）：AppDeployNode.UserName 行级脱敏三件套。
/// ①Search 行 CropRow 值级置 null（键不删）；②Detail 掩码回显 "*****"（Vue 编辑抽屉走 Detail 取数，防 F1 静默清库）；
/// ③Valid POST 守卫——提交 "*****" 从库恢复原值并清脏标记，空串放行=显式清空。
/// 反射链与 MaskedRowsKnife0Tests 同源（Index: new Pager → SearchData → OnFillListValues → OnJsonSerialize 生产同参）；
/// DbContext 为 ServerTests 隔离 SQLite。读回断言走裸 SQL QueryScalar 绕开 XCode 实体缓存；
/// FindById 返回共享缓存实例，禁止改其属性后当作守卫恢复源（守卫步骤先用全新 put 实体）。
/// </summary>
public class MaskedRowsKnife8Tests
{
    private static Type AppDeployNodeControllerType =>
        Type.GetType("Stardust.Web.Areas.Deployment.Controllers.AppDeployNodeController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi AppDeployNodeController not found");

    #region 反射基建（与 MaskedRowsKnife0Tests 同源）

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

    /// <summary>裸 SQL 标量读回（绕开 XCode 实体缓存，验证库中真值）</summary>
    private static String QueryScalar(String sql)
    {
        var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
        var ds = dal.Session.Query(sql);
        var dt = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : null;
        if (dt == null || dt.Rows.Count == 0) return "";
        return dt.Rows[0][0]?.ToString() ?? "";
    }

    #endregion

    [Fact(DisplayName = "task-95 刀8/N2：AppDeployNode 列表行 UserName 键在值 null；Detail 掩码回显 '*****'")]
    public void AppDeployNode_Row_Masked_And_Detail_Echoes()
    {
        var tag = "mw8a-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Deployment.AppDeployNode
            {
                DeployId = 1, NodeId = 1, DeployName = tag + "-dn", UserName = "u-" + tag, Enable = true,
            };
            e.Insert();

            // ①Search 行 CropRow：UserName 值级置 null（键不删）
            var rows = InvokeIndexRows(AppDeployNodeControllerType, "/api/Deployment/AppDeployNode");
            var mine = rows.Where(r => r.TryGetValue("deployName", out var n) && (n.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            AssertMasked(mine, "userName", "AppDeployNode");

            // ②Detail 掩码回显 "*****"（Vue 编辑抽屉取数侧）
            var res = InvokeDetail(AppDeployNodeControllerType, NewController(AppDeployNodeControllerType), e.Id.ToString());
            var data = res.GetType().GetProperty("Data")!.GetValue(res)!;
            Assert.Equal("*****", data.GetType().GetProperty("UserName")!.GetValue(data));
        }
        finally { CleanupRaw($"Delete From AppDeployNode Where DeployName='{tag}-dn'"); }
    }

    [Fact(DisplayName = "task-95 刀8/N2：AppDeployNode Valid POST 守卫——'*****' 恢复原值真写读回不变；空串放行=显式清空")]
    public void AppDeployNode_Valid_Post_Guard_Restores_And_Empty_Passes()
    {
        var tag = "mw8b-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Deployment.AppDeployNode
            {
                DeployId = 1, NodeId = 1, DeployName = tag + "-dn", UserName = "real-" + tag, Enable = true,
            };
            e.Insert();

            // POST 守卫（先做：避免改写 XCode 实体缓存共享实例污染恢复源）。
            // put 用全新实体（Knife2/34 同模式，initializer 置脏）；F1 提交体=Detail 全量展开，含 DeployId/NodeId（Valid 强校验必填）；
            // 恢复源 FindById=库值（缓存未污染）
            var put = new Stardust.Data.Deployment.AppDeployNode
            {
                Id = e.Id, DeployId = e.DeployId, NodeId = e.NodeId, DeployName = e.DeployName, UserName = "*****", Enable = true,
            };
            InvokeValid(AppDeployNodeControllerType, NewController(AppDeployNodeControllerType), put, DataObjectMethodType.Update, post: true);
            Assert.Equal("real-" + tag, put.UserName);
            put.Update();
            Assert.Equal("real-" + tag, QueryScalar($"Select UserName From AppDeployNode Where Id={e.Id}"));

            // 空串放行：显式清空（空值不走哨兵分支直通写库）
            var clear = new Stardust.Data.Deployment.AppDeployNode
            {
                Id = e.Id, DeployId = e.DeployId, NodeId = e.NodeId, DeployName = e.DeployName, UserName = "", Enable = true,
            };
            InvokeValid(AppDeployNodeControllerType, NewController(AppDeployNodeControllerType), clear, DataObjectMethodType.Update, post: true);
            clear.Update();
            Assert.True(String.IsNullOrEmpty(QueryScalar($"Select UserName From AppDeployNode Where Id={e.Id}")));
        }
        finally { CleanupRaw($"Delete From AppDeployNode Where DeployName='{tag}-dn'"); }
    }
}
