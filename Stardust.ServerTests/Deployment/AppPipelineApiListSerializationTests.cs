using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Platform;
using Xunit;
using XCode;

namespace ServerTest.Deployment;

/// <summary>
/// AppPipeline / AppDeploy 列表行自描述序列化回归。
/// 缺陷现象：AppPipeline 列表带 ?projectId= 时行 JSON 顶层缺失 projectName 键，且双形状均缺
/// updateUserId/updateIP；AppDeploy 行 JSON 缺 appId 键（前端 ship.logic.ts 被迫双通道兜底，
/// 行自描述要求补齐）。
/// 行 JSON 出口链路 = ControllerBaseX.OnActionExecuted → OnJsonSerialize → SystemJson{CamelCase,
/// IgnoreNullValues=false} → ExtendableConverter：对 IExtend 实体逐公共属性全量输出（仅跳过
/// [XmlIgnore]/[IgnoreDataMember]/名为 Items 的属性）+ IExtend.Items 键原样内联。
/// [Map] 计算属性（ProjectName/DeployName）与普通数据列（AppId/UpdateUserId/UpdateIP）均无排除
/// 特性 ⇒ 栈结构上应自愈；TrimFields/RemoveAuditFields 只影响字段集合（meta），不影响行 JSON。
/// 本测试反射驱动与 Index 完全同构的链路（new Pager → SearchData → OnFillListValues →
/// OnJsonSerialize 生产同参序列化），断言行 JSON 键集与值；DbContext 为 ServerTests 隔离 SQLite。
/// 另含列表行脱敏回归组：AppPipeline Token/Secret、AppDeploy RepoPassword 族、
/// McpToken Token、SslCertificate/GatewayCert PfxPassword 列表行「键在值 null」（值级置 null
/// 语义），+ McpToken 写响应结构性正断言（Insert/Update/Delete 不得 override，一次性揭示不破）。
/// </summary>
public class AppPipelineApiListSerializationTests
{
    private static Type PipelineControllerType =>
        Type.GetType("Stardust.Web.Areas.Deployment.Controllers.AppPipelineController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi AppPipelineController not found");

    private static Type AppDeployControllerType =>
        Type.GetType("Stardust.Web.Areas.Deployment.Controllers.AppDeployController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi AppDeployController not found");

    #region 反射基建（与 McpServerApiUpdateTests 同源；避免测试工程与 StarWebApi 的 NewLife.Cube 面冲突）

    private static Object NewController(Type type)
    {
        // AppPipelineController 构造函数需要 PipelineService(deployService, tracer)，列表链路不触达；
        // 全引用参数一律置 null 满足绑定，值类型参数（无此例）跳过。
        foreach (var ctor in type.GetConstructors())
        {
            var ps = ctor.GetParameters();
            if (ps.Any(p => p.ParameterType.IsValueType && !p.HasDefaultValue)) continue;
            return Activator.CreateInstance(type, ps.Select(_ => (Object?)null).ToArray())!;
        }
        return Activator.CreateInstance(type)!;
    }

    private static void InjectQuery(Object controller, String query)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/api/Deployment/AppPipeline";
        if (!String.IsNullOrEmpty(query)) http.Request.QueryString = new QueryString(query);
        typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
            .SetValue(controller, new ControllerContext { HttpContext = http });
    }

    /// <summary>与 Index() 同构：new Pager → SearchData → OnFillListValues → OnJsonSerialize（生产同参）→ 解析行数组。</summary>
    private static IList<Dictionary<String, JsonElement>> InvokeIndexRows(Type type,
        (String key, String value)[] pagerParams, String query)
    {
        var controller = NewController(type);
        InjectQuery(controller, query);

        // Index(): new Pager(WebHelper.Params){RetrieveTotalCount=PageSetting.EnableTotalCount}——
        // 测试宿主无 WebHelper 静态请求上下文，改为显式注入同语义参数（Pager.Params 与查询串键名一致）
        var pager = new Pager { PageIndex = 0, PageSize = 20, RetrieveTotalCount = true };
        foreach (var (key, value) in pagerParams) pager[key] = value;

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

    private static IEnumerable<Dictionary<String, JsonElement>> Mine(
        IEnumerable<Dictionary<String, JsonElement>> rows, String prefix) =>
        rows.Where(r => r.TryGetValue("name", out var n) && (n.GetString() ?? "").StartsWith(prefix, StringComparison.Ordinal));

    private static void AssertKeyPresent(IEnumerable<Dictionary<String, JsonElement>> rows, String key, String ctx) =>
        Assert.All(rows, r => Assert.True(r.ContainsKey(key),
            $"{ctx}: 行 JSON 缺键 '{key}'；实际键=[{String.Join(",", r.Keys)}]"));

    private static void Cleanup(params String[] names)
    {
        var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
        foreach (var n in names)
        {
            dal.Session.Execute($"Delete From AppPipeline Where Name='{n}'");
            dal.Session.Execute($"Delete From AppDeploy Where Name='{n}'");
            dal.Session.Execute($"Delete From GalaxyProject Where Name='{n}'");
        }
    }

    /// <summary>毕报机读锚：落盘行键集与敏感列出现性观察（只记录不断言，token/secret 处置候 lead 裁定）。</summary>
    private static void DumpEvidence(String shape, IList<Dictionary<String, JsonElement>> rows, String prefix)
    {
        // 从 BaseDirectory（Bin\ServerTest）逐级上溯定位仓库根（以 AGENTS.md 为锚），证据写入 Doc 证据树
        var dir = AppContext.BaseDirectory!;
        while (dir != null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        Assert.False(dir == null, "未能定位仓库根（AGENTS.md 锚）");
        var evidenceDir = Path.Combine(dir!, "Doc", "用户旅程", "evidence", "appwalk", "task89-backend");
        Directory.CreateDirectory(evidenceDir);
        var mine = Mine(rows, prefix).FirstOrDefault();
        var sample = rows.FirstOrDefault();
        var keys = sample != null ? sample.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList() : new List<String>();
        Boolean Has(String k) => keys.Contains(k, StringComparer.Ordinal);
        var doc = new
        {
            shape,
            capturedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            rowKeyCount = keys.Count,
            rowKeys = keys,
            tokenPresent = Has("token"),
            secretPresent = Has("secret"),
            projectNamePresent = Has("projectName"),
            updateUserIdPresent = Has("updateUserId"),
            updateIpPresent = Has("updateIP"),
            mineFirstRow = mine,
        };
        File.WriteAllText(Path.Combine(evidenceDir, $"apw-{shape}-rowkeys.json"),
            JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    #endregion

    #region 造数

    private static GalaxyProject SeedProject(String name)
    {
        var p = new GalaxyProject { Name = name };
        p.Insert();
        return p;
    }

    private static void SeedPipeline(String name, Int32 projectId)
    {
        var e = new AppPipeline
        {
            Name = name,
            DeployId = 0,
            ProjectId = projectId,
            Branch = "main",
            BuildNodeId = 0,
            AutoDeploy = true,
            Enable = true,
            UpdateUserId = 1,
            UpdateIP = "10.188.82.141",
        };
        e.Insert();
    }

    private static void SeedDeploy(String name, Int32 appId, Int32 projectId)
    {
        var e = new AppDeploy
        {
            Name = name,
            AppId = appId,
            ProjectId = projectId,
            Category = "unit",
            Enable = true,
            UpdateUserId = 1,
            UpdateIP = "10.188.82.141",
        };
        e.Insert();
    }

    #endregion

    [Fact(DisplayName = "AppPipeline 列表（无参形状）行 JSON 必须含 updateUserId/updateIP 键（无参侧 projectName/deployName 键同验）")]
    public void Index_Unscoped_RowKeys_Should_SelfDescribe()
    {
        var tag = "apw-u-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            SeedPipeline(tag + "-p1", 0);

            var rows = InvokeIndexRows(PipelineControllerType, Array.Empty<(String, String)>(), "");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"无参形状应能查到造数行 {tag}*，实际行数={rows.Count}，首行键=[{(rows.Count > 0 ? String.Join(",", rows[0].Keys) : "")}]");
            DumpEvidence("unscoped", rows, tag);

            // 行自描述回归：updateUserId/updateIP 双形状键缺失
            AssertKeyPresent(mine, "updateUserId", "无参形状");
            AssertKeyPresent(mine, "updateIP", "无参形状");
            Assert.All(mine, r =>
            {
                Assert.Equal(1, r["updateUserId"].GetInt32());
                Assert.Equal("10.188.82.141", r["updateIP"].GetString());
            });

            // 无参侧：projectName/deployName/updateTime 键必须存在（值可 null）
            AssertKeyPresent(mine, "projectName", "无参形状");
            AssertKeyPresent(mine, "deployName", "无参形状");
            AssertKeyPresent(mine, "updateTime", "无参形状");
        }
        finally { Cleanup(tag + "-p1"); }
    }

    [Fact(DisplayName = "AppPipeline 列表带 projectId= 时行 JSON 必须保留 projectName 键且值=项目名（TrimFields 不得影响行自描述）")]
    public void Index_ScopedProjectId_Row_Should_Keep_ProjectName_Key()
    {
        var tag = "apw-s-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var proj = SeedProject(tag + "-proj");
            SeedPipeline(tag + "-p2", proj.Id);

            // 生产 scoped 形状：?projectId=N&pageIndex=0&pageSize=20（Pager.Params 驱动 Search 链，
            // 查询串驱动 WebEntityController.OnGetFields→TrimFields 的 getRequest 通道）
            var rows = InvokeIndexRows(PipelineControllerType,
                new[] { ("projectId", proj.Id.ToString()) },
                $"?projectId={proj.Id}&pageIndex=0&pageSize=20");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"scoped 形状应能查到造数行 {tag}*，实际行数={rows.Count}");
            DumpEvidence("scoped", rows, tag);

            // 缺陷锚：带 projectId 的响应行 projectName 键级缺失（非 null）。修复判据=键在且值=项目名
            AssertKeyPresent(mine, "projectName", "scoped 形状");
            Assert.All(mine, r => Assert.Equal(tag + "-proj", r["projectName"].GetString()));

            // scoped 侧：updateUserId/updateIP 同验
            AssertKeyPresent(mine, "updateUserId", "scoped 形状");
            AssertKeyPresent(mine, "updateIP", "scoped 形状");
        }
        finally { Cleanup(tag + "-p2", tag + "-proj"); }
    }

    [Fact(DisplayName = "AppDeploy 列表行 JSON 必须含 appId 键（行自描述，前端退役 ship.logic.ts 双通道前置）")]
    public void AppDeploy_Index_Row_Should_Contain_AppId_Key()
    {
        var tag = "apw-d-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            SeedDeploy(tag + "-d1", appId: 42, projectId: 0);

            var rows = InvokeIndexRows(AppDeployControllerType, Array.Empty<(String, String)>(), "");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"AppDeploy 无参形状应能查到造数行 {tag}*，实际行数={rows.Count}，首行键=[{(rows.Count > 0 ? String.Join(",", rows[0].Keys) : "")}]");
            DumpEvidence("appdeploy", rows, tag);

            AssertKeyPresent(mine, "appId", "AppDeploy");
            Assert.All(mine, r => Assert.Equal(42, r["appId"].GetInt32()));

            // 行自描述同族：[Map] 计算属性 appName 键必须存在（值可 null：appId=42 无对应应用行）
            AssertKeyPresent(mine, "appName", "AppDeploy");
        }
        finally { Cleanup(tag + "-d1"); }
    }

    #region 列表行脱敏回归（值级置 null：键在值 null）

    private static Type McpTokenControllerType =>
        Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpTokenController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi McpTokenController not found");

    private static Type SslCertificateControllerType =>
        Type.GetType("Stardust.Web.Areas.Deployment.Controllers.SslCertificateController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi SslCertificateController not found");

    private static Type GatewayCertControllerType =>
        Type.GetType("Stardust.Web.Areas.Gateway.Controllers.GatewayCertController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi GatewayCertController not found");

    private static Type DomainProviderControllerType =>
        Type.GetType("Stardust.Web.Areas.Nodes.Controllers.DomainProviderController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi DomainProviderController not found");

    /// <summary>负断言：行 JSON 键必须存在（值级置 null 语义=键不删）且值必须为 JSON null（明文不落行）。</summary>
    private static void AssertMasked(IList<Dictionary<String, JsonElement>> rows, String key, String ctx)
    {
        AssertKeyPresent(rows, key, ctx);
        Assert.All(rows, r => Assert.True(r[key].ValueKind == JsonValueKind.Null,
            $"{ctx}: 行 JSON '{key}' 值未脱敏（期望 JSON null，实际 {r[key].ValueKind}）"));
    }

    private static void CleanupRaw(String table, String name)
    {
        var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
        dal.Session.Execute($"Delete From {table} Where Name='{name}'");
    }

    [Fact(DisplayName = "AppPipeline 列表行 Token/Secret 键在值 null（明文不得落行 JSON）")]
    public void Index_AppPipeline_Row_Token_Secret_Should_Be_Masked()
    {
        var tag = "apw-m-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new AppPipeline
            {
                Name = tag + "-p1", DeployId = 0, ProjectId = 0, Branch = "main",
                Token = "tk-" + tag, Secret = "sec-" + tag,
                AutoDeploy = true, Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
            };
            e.Insert();

            var rows = InvokeIndexRows(PipelineControllerType, Array.Empty<(String, String)>(), "");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            // 值级置 null 语义双锁：键在（前端 pick()/maskTokenFull('') 优雅降级不破）+ 值 null（明文零泄漏）
            AssertMasked(mine, "token", "AppPipeline");
            AssertMasked(mine, "secret", "AppPipeline");
        }
        finally { Cleanup(tag + "-p1"); }
    }

    [Fact(DisplayName = "AppDeploy 列表行 RepoPassword/RepoUserName/DeployKey/UserName 键在值 null")]
    public void Index_AppDeploy_Row_Sensitive_Four_Should_Be_Masked()
    {
        var tag = "apw-md-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new AppDeploy
            {
                Name = tag + "-d1", AppId = 42, ProjectId = 0, Category = "unit",
                RepoPassword = "pw-" + tag, RepoUserName = "ru-" + tag,
                DeployKey = "dk-" + tag, UserName = "un-" + tag,
                Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
            };
            e.Insert();

            var rows = InvokeIndexRows(AppDeployControllerType, Array.Empty<(String, String)>(), "");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"AppDeploy 应能查到造数行 {tag}*，实际行数={rows.Count}");

            AssertMasked(mine, "repoPassword", "AppDeploy");
            AssertMasked(mine, "repoUserName", "AppDeploy");
            AssertMasked(mine, "deployKey", "AppDeploy");
            AssertMasked(mine, "userName", "AppDeploy");
        }
        finally { Cleanup(tag + "-d1"); }
    }

    [Fact(DisplayName = "McpToken 列表/详情行 Token 键在值 null；写响应结构性保留 Token（create/reset 一次性揭示不破）")]
    public void Index_McpToken_Row_Token_Masked_WritePath_Keeps_Token()
    {
        var tag = "apw-mt-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new McpToken
            {
                Name = tag + "-t1", Token = "mtk-" + tag,
                Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
            };
            e.Insert();

            var rows = InvokeIndexRows(McpTokenControllerType, Array.Empty<(String, String)>(), "");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"McpToken 应能查到造数行 {tag}*，实际行数={rows.Count}");
            AssertMasked(mine, "token", "McpToken");

            // 正断言（结构性）：写路径（Insert/Update/Delete）不得被 override——base 写响应返回完整实体，
            // Token 明文随 create/reset 响应回传 = 前端一次性揭示 UX 的数据源；Reset 动作必须仍在。
            var declared = McpTokenControllerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
            Assert.False(declared.Contains("Insert"), "McpTokenController 不得 override Insert（写响应必须保留 Token 明文）");
            Assert.False(declared.Contains("Update"), "McpTokenController 不得 override Update（写响应必须保留 Token 明文）");
            Assert.False(declared.Contains("Delete"), "McpTokenController 不得 override Delete（写响应必须保留 Token 明文）");
            Assert.True(declared.Contains("Reset"), "McpTokenController.Reset 动作必须在（一次性揭示入口）");
        }
        finally { CleanupRaw("McpToken", tag + "-t1"); }
    }

    [Fact(DisplayName = "SslCertificate/GatewayCert 列表行 PfxPassword 键在值 null（防回归锁）")]
    public void Index_SslCertificate_Row_PfxPassword_Should_Stay_Masked()
    {
        var tag = "apw-ms-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            // SslCertificate 无 Name 列（CS0117 实证），唯一业务锚=Domain；行 JSON 亦无 name 键，按 domain 过滤
            var domain = tag + ".example.com";
            var e = new SslCertificate
            {
                Domain = domain, PfxPassword = "pfx-" + tag,
                Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
            };
            e.Insert();

            var rows = InvokeIndexRows(SslCertificateControllerType, Array.Empty<(String, String)>(), "");
            var mine = rows.Where(r => r.TryGetValue("domain", out var d) && (d.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine.Count >= 1, $"SslCertificate 应能查到造数行 {tag}*，实际行数={rows.Count}");
            AssertMasked(mine, "pfxPassword", "SslCertificate");

            var rows2 = InvokeIndexRows(GatewayCertControllerType, Array.Empty<(String, String)>(), "");
            var mine2 = rows2.Where(r => r.TryGetValue("domain", out var d) && (d.GetString() ?? "").StartsWith(tag, StringComparison.Ordinal)).ToList();
            Assert.True(mine2.Count >= 1, $"GatewayCert 应能查到造数行 {tag}*，实际行数={rows2.Count}");
            AssertMasked(mine2, "pfxPassword", "GatewayCert");
        }
        finally
        {
            var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
            dal.Session.Execute($"Delete From SslCertificate Where Domain='{tag}.example.com'");
        }
    }

    [Fact(DisplayName = "DomainProvider 列表行 AppKey/AppSecret 键在值 null（meta 掩码显示但行 JSON 原明文直发）")]
    public void Index_DomainProvider_Row_AppKey_AppSecret_Should_Be_Masked()
    {
        var tag = "apw-dp-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var e = new Stardust.Data.Nodes.DomainProvider
            {
                Name = tag + "-dp1", Provider = "AliyunDNS",
                AppKey = "ak-" + tag, AppSecret = "as-" + tag, Domain = tag + ".example.com",
                Enable = true, UpdateUserID = 1, UpdateIP = "10.188.82.141",
            };
            e.Insert();

            var rows = InvokeIndexRows(DomainProviderControllerType, Array.Empty<(String, String)>(), "");
            var mine = Mine(rows, tag).ToList();
            Assert.True(mine.Count >= 1, $"DomainProvider 应能查到造数行 {tag}*，实际行数={rows.Count}");

            // 双键同族锁：AppKey + AppSecret
            AssertMasked(mine, "appKey", "DomainProvider");
            AssertMasked(mine, "appSecret", "DomainProvider");
        }
        finally { CleanupRaw("DomainProvider", tag + "-dp1"); }
    }

    #endregion
}
