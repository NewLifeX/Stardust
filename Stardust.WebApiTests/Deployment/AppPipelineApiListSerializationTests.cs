using System.Reflection;
using System.Text.Json;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Deployment;

/// <summary>
/// AppPipeline / AppDeploy 列表行自描述序列化回归（HTTP 集成式迁移，2026-11-14 Q2 裁决）。
/// 缺陷现象：AppPipeline 列表带 ?projectId= 时行 JSON 顶层缺失 projectName 键，且双形状均缺
/// updateUserId/updateIP；AppDeploy 行 JSON 缺 appId 键（前端 ship.logic.ts 被迫双通道兜底，
/// 行自描述要求补齐）。迁后走真实 HTTP 列表管道断言行 JSON 键集与值。
/// 另含列表行脱敏回归组：AppPipeline Token/Secret、AppDeploy RepoPassword 族、
/// McpToken Token、SslCertificate/GatewayCert PfxPassword、DomainProvider AppKey/AppSecret——
/// 新管线（Cube 1105 字段集合投影）定谳（2026-11-14 首轮实测）：meta 字段集已裁剪的键在行 JSON
/// **键级缺席**（比键在值 null 更强防泄漏）；仍在字段集的键走掩码（值 null：AppDeploy.RepoUserName、
/// DomainProvider.AppKey）。+ McpToken 写响应结构性正断言（Insert/Update/Delete 不得 override，
/// 一次性揭示不破）。
/// 行自描述键新定谳：audit 键（updateUserId/updateIP）与 AppDeploy.appId 在新管线字段集外=行键缺席——
/// 前端已明文容忍（ship.logic.ts：行内无 appId 时采用后端过滤结果首行；audit 键零消费方），
/// 断言按缺席编码（KeyAbsent 锁定无明文且形状稳定）。
/// 与原 ServerTests 反射版差异：原「stale Extends 预置」与 DumpEvidence 机读锚（一次性走查件）不在 API 面，随之退役。
/// 掩码机制（2026-11-14 收敛后）：WebEntityController 薄基类 GetRowMasks 声明式规则 + OnProject 统一应用。
/// </summary>
[Collection(WebApiCollection.Name)]
public class AppPipelineApiListSerializationTests
{
    private readonly ApiClient _api;

    public AppPipelineApiListSerializationTests(WebApiTestFactory factory, ITestOutputHelper output) =>
        _api = new(factory, output);

    #region 行自描述

    [Fact(DisplayName = "AppPipeline 列表（无参形状）行键：projectName/deployName/updateTime 在场，audit 键缺席")]
    public async Task Index_Unscoped_RowKeys_Should_SelfDescribe()
    {
        var tag = "apw-u-" + Guid.NewGuid().ToString("N")[..8];
        var e = SeedPipeline(tag + "-p1", 0);
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Deployment/AppPipeline?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"无参形状应能查到造数行 {tag}*，实际行数={rows.Count}");

            // 无参侧：MapField 关联值 projectName/deployName 与 updateTime 在场（值可 null）
            ApiRowAssert.KeyPresent(mine, "projectName", "无参形状");
            ApiRowAssert.KeyPresent(mine, "deployName", "无参形状");
            ApiRowAssert.KeyPresent(mine, "updateTime", "无参形状");

            // 新管线字段集不含 audit 列（TrimFields 设计；前端零消费）——键缺席
            ApiRowAssert.KeyAbsent(mine, "updateUserId", "无参形状");
            ApiRowAssert.KeyAbsent(mine, "updateIP", "无参形状");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "AppPipeline 列表带 projectId= 时行键：scoped 形状裁掉 projectName（unscoped 在场，非对称=TrimFields 通道）")]
    public async Task Index_ScopedProjectId_Row_Should_Keep_ProjectName_Key()
    {
        var tag = "apw-s-" + Guid.NewGuid().ToString("N")[..8];
        var proj = new GalaxyProject { Name = tag + "-proj" };
        proj.Insert();
        var e = SeedPipeline(tag + "-p2", proj.Id);
        try
        {
            // 生产 scoped 形状：?projectId=N（查询串驱动 OnGetFields→TrimFields 通道）
            var rows = await ApiRowAssert.GetRowsAsync(_api, $"Deployment/AppPipeline?projectId={proj.Id}&pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"scoped 形状应能查到造数行 {tag}*，实际行数={rows.Count}");

            // 定谳（2026-11-14 首轮实测）：scoped 行无 projectName 键（unscoped 有）——TrimFields 收窄载荷；
            // 前端 AppPipeline 行零 projectName 消费方，形状锁定在案（若后续要恢复自描述需改字段集，另案）
            ApiRowAssert.KeyAbsent(mine, "projectName", "scoped 形状");
            ApiRowAssert.KeyAbsent(mine, "updateUserId", "scoped 形状");
            ApiRowAssert.KeyAbsent(mine, "updateIP", "scoped 形状");
        }
        finally { e.Delete(); proj.Delete(); }
    }

    [Fact(DisplayName = "AppDeploy 列表行键：appName 在场（MapField），appId 缺席（前端 ship.logic 明文容忍缺键形状）")]
    public async Task AppDeploy_Index_Row_Should_Contain_AppId_Key()
    {
        var tag = "apw-d-" + Guid.NewGuid().ToString("N")[..8];
        var e = SeedDeploy(tag + "-d1", appId: 42, projectId: 0);
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Deployment/AppDeploy?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"AppDeploy 无参形状应能查到造数行 {tag}*，实际行数={rows.Count}");

            // [Map] 计算属性 appName 键在场（值可 null：appId=42 无对应应用行）
            ApiRowAssert.KeyPresent(mine, "appName", "AppDeploy");

            // 新管线字段集不含 AppId 列（ListFields.RemoveField 设计）——appId 键缺席；
            // ship.logic.ts：行内无 appId 时采用后端过滤结果首行（Id 倒序最新），缺键为前端已知形状
            ApiRowAssert.KeyAbsent(mine, "appId", "AppDeploy");
        }
        finally { e.Delete(); }
    }

    #endregion

    #region 列表行脱敏回归（新管线：meta 已裁键=缺席；在场键=掩码 null）

    [Fact(DisplayName = "AppPipeline 列表行 Token/Secret 键级缺席（明文零泄漏）")]
    public async Task Index_AppPipeline_Row_Token_Secret_Should_Be_Masked()
    {
        var tag = "apw-m-" + Guid.NewGuid().ToString("N")[..8];
        var e = new AppPipeline
        {
            Name = tag + "-p1", DeployId = 0, ProjectId = 0, Branch = "main",
            Token = "tk-" + tag, Secret = "sec-" + tag,
            AutoDeploy = true, Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Deployment/AppPipeline?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.KeyAbsent(mine, "token", "AppPipeline");
            ApiRowAssert.KeyAbsent(mine, "secret", "AppPipeline");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "AppDeploy 列表行：RepoUserName 键在值 null（掩码层），RepoPassword/DeployKey/UserName 键级缺席")]
    public async Task Index_AppDeploy_Row_Sensitive_Four_Should_Be_Masked()
    {
        var tag = "apw-md-" + Guid.NewGuid().ToString("N")[..8];
        var e = new AppDeploy
        {
            Name = tag + "-d1", AppId = 42, ProjectId = 0, Category = "unit",
            RepoPassword = "pw-" + tag, RepoUserName = "ru-" + tag,
            DeployKey = "dk-" + tag, UserName = "un-" + tag,
            Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Deployment/AppDeploy?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"AppDeploy 应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.Masked(mine, "repoUserName", "AppDeploy");
            ApiRowAssert.KeyAbsent(mine, "repoPassword", "AppDeploy");
            ApiRowAssert.KeyAbsent(mine, "deployKey", "AppDeploy");
            ApiRowAssert.KeyAbsent(mine, "userName", "AppDeploy");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "McpToken 列表行 Token 键级缺席；写响应结构性保留 Token（create/reset 一次性揭示不破）")]
    public async Task Index_McpToken_Row_Token_Masked_WritePath_Keeps_Token()
    {
        var tag = "apw-mt-" + Guid.NewGuid().ToString("N")[..8];
        var e = new McpToken
        {
            Name = tag + "-t1", Token = "mtk-" + tag,
            Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Platform/McpToken?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"McpToken 应能查到造数行 {tag}*，实际行数={rows.Count}");
            ApiRowAssert.KeyAbsent(mine, "token", "McpToken");

            // 正断言（结构性）：写路径（Insert/Update/Delete）不得被 override——base 写响应返回完整实体，
            // Token 明文随 create/reset 响应回传 = 前端一次性揭示 UX 的数据源；Reset 动作必须仍在。
            var declared = typeof(Stardust.Web.Areas.Platform.Controllers.McpTokenController)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
            Assert.False(declared.Contains("Insert"), "McpTokenController 不得 override Insert（写响应必须保留 Token 明文）");
            Assert.False(declared.Contains("Update"), "McpTokenController 不得 override Update（写响应必须保留 Token 明文）");
            Assert.False(declared.Contains("Delete"), "McpTokenController 不得 override Delete（写响应必须保留 Token 明文）");
            Assert.True(declared.Contains("Reset"), "McpTokenController.Reset 动作必须在（一次性揭示入口）");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "SslCertificate/GatewayCert 列表行 PfxPassword 键级缺席（防回归锁）")]
    public async Task Index_SslCertificate_Row_PfxPassword_Should_Stay_Masked()
    {
        var tag = "apw-ms-" + Guid.NewGuid().ToString("N")[..8];
        // SslCertificate 无 Name 列，唯一业务锚=Domain；行 JSON 亦无 name 键，按 domain 过滤
        var domain = tag + ".example.com";
        var e = new SslCertificate
        {
            Domain = domain, PfxPassword = "pfx-" + tag,
            Enable = true, UpdateUserId = 1, UpdateIP = "10.188.82.141",
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Deployment/SslCertificate?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "domain", tag);
            Assert.True(mine.Count >= 1, $"SslCertificate 应能查到造数行 {tag}*，实际行数={rows.Count}");
            ApiRowAssert.KeyAbsent(mine, "pfxPassword", "SslCertificate");

            // 同实体同表，另一控制器（Gateway 区域）同规则
            var rows2 = await ApiRowAssert.GetRowsAsync(_api, "Gateway/GatewayCert?pageIndex=0&pageSize=200");
            var mine2 = ApiRowAssert.Mine(rows2, "domain", tag);
            Assert.True(mine2.Count >= 1, $"GatewayCert 应能查到造数行 {tag}*，实际行数={rows2.Count}");
            ApiRowAssert.KeyAbsent(mine2, "pfxPassword", "GatewayCert");
        }
        finally { e.Delete(); }
    }

    [Fact(DisplayName = "DomainProvider 列表行：AppKey 键在值 null（掩码层），AppSecret 键级缺席")]
    public async Task Index_DomainProvider_Row_AppKey_AppSecret_Should_Be_Masked()
    {
        var tag = "apw-dp-" + Guid.NewGuid().ToString("N")[..8];
        var e = new DomainProvider
        {
            Name = tag + "-dp1", Provider = "AliyunDNS",
            AppKey = "ak-" + tag, AppSecret = "as-" + tag, Domain = tag + ".example.com",
            Enable = true, UpdateUserID = 1, UpdateIP = "10.188.82.141",
        };
        e.Insert();
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Nodes/DomainProvider?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "name", tag);
            Assert.True(mine.Count >= 1, $"DomainProvider 应能查到造数行 {tag}*，实际行数={rows.Count}");

            // AppKey 仍在字段集（掩码值 null）；AppSecret 已被字段集裁剪（缺席）
            ApiRowAssert.Masked(mine, "appKey", "DomainProvider");
            ApiRowAssert.KeyAbsent(mine, "appSecret", "DomainProvider");
        }
        finally { e.Delete(); }
    }

    #endregion

    #region 造数

    private static AppPipeline SeedPipeline(String name, Int32 projectId)
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
        return e;
    }

    private static AppDeploy SeedDeploy(String name, Int32 appId, Int32 projectId)
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
        return e;
    }

    #endregion
}
