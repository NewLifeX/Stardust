using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Monitors;
using Stardust.Mcp;
using Stardust.Mcp.Tools.Registry;
using Stardust.Models;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// 应用读工具复用 MVC/API 同一套查询：
/// App.Search、AppOnline.FindAllByProjectId、FindAllByAppId、FindAllByServiceName、App.QueryHealth。
/// 连真实 SQLite，自建数据用独立项目号并在 finally 清理。
/// </summary>
public class AppToolsReuseTests
{
    private const String Category = "mrx-cat";

    private static JsonElement Args(Object value) => JsonSerializer.SerializeToElement(value);

    private static McpContext Ctx(Int32 projectId) => new()
    {
        TokenId = 919221,
        ProjectId = projectId,
        UserId = 0,
        Scopes = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "apps:read" },
    };

    private static String Token() => "mrx" + Guid.NewGuid().ToString("n")[..6];

    private static void Cleanup(Int32 projectId)
    {
        foreach (var app in App.FindAllByProjectId(projectId).ToList())
            DeleteApp(app);
    }

    private static void DeleteApp(App app)
    {
        foreach (var online in AppOnline.FindAllByAppId(app.Id).ToList()) online.Delete();
        foreach (var svc in AppService.FindAllByAppId(app.Id).ToList()) svc.Delete();
        foreach (var consume in AppConsume.FindAllByAppId(app.Id).ToList()) consume.Delete();
        foreach (var deploy in AppDeploy.FindAllByAppId(app.Id).ToList())
        {
            foreach (var pipeline in AppPipeline.FindAllByDeployId(deploy.Id).ToList())
            {
                foreach (var run in AppPipelineRun.FindAllByPipelineId(pipeline.Id).ToList()) run.Delete();
                pipeline.Delete();
            }
            deploy.Delete();
        }
        foreach (var stat in AppMinuteStat.FindAllByAppId(app.Id).ToList()) stat.Delete();
        app.Delete();
    }

    [Fact(DisplayName = "app_list 走 App.Search，在线数按项目在线记录汇总")]
    public async Task AppList_UsesAppSearch_AndProjectOnlineCount()
    {
        const Int32 projectId = 910221;
        const Int32 otherProjectId = 910229;
        Cleanup(projectId);
        Cleanup(otherProjectId);

        var token = Token();
        App appA = null;
        App appB = null;
        App byDisplay = null;
        App wrongCategory = null;
        App otherProject = null;
        try
        {
            appA = InsertApp(projectId, token + "a", Category, true, "1.0");
            appB = InsertApp(projectId, token + "b", Category, false, "2.0");
            byDisplay = InsertApp(projectId, "zz" + Guid.NewGuid().ToString("n")[..8], Category, true, null, token);
            wrongCategory = InsertApp(projectId, token + "c", "other", true, null);
            otherProject = InsertApp(otherProjectId, token + "d", Category, true, null);

            InsertOnline(appA, projectId);
            InsertOnline(appA, otherProjectId);
            InsertOnline(appB, projectId);
            InsertOnline(appB, projectId);

            var tool = new AppListTool();
            var page = new PageParameter
            {
                PageIndex = 1,
                PageSize = 2,
                Sort = App.__.Id,
                RetrieveTotalCount = true,
            };
            var expected = App.Search(projectId, Category, null, DateTime.MinValue, DateTime.MinValue, token, page);
            var result = await tool.InvokeAsync(Args(new Dictionary<String, Object>
            {
                ["keyword"] = token,
                ["category"] = Category,
                ["page"] = 1,
                ["page_size"] = 2,
            }), Ctx(projectId));
            var node = JsonSerializer.SerializeToNode(result)!;
            var records = node["data"]!["records"]!.AsArray();

            Assert.Equal(expected.Select(e => e.Id).ToArray(), records.Select(e => e!["id"]!.GetValue<Int32>()).ToArray());
            Assert.Equal((Int32)page.TotalCount, node["pagination"]!["total"]!.GetValue<Int32>());
            Assert.Equal(3, node["pagination"]!["total"]!.GetValue<Int32>());
            var rowA = records.First(e => e!["id"]!.GetValue<Int32>() == appA.Id);
            Assert.Equal(1, rowA!["online"]!.GetValue<Int32>());
            Assert.DoesNotContain(records, e => e!["id"]!.GetValue<Int32>() == wrongCategory.Id);
            Assert.DoesNotContain(records, e => e!["id"]!.GetValue<Int32>() == otherProject.Id);

            var onlineByApp = AppOnline.FindAllByProjectId(projectId).GroupBy(e => e.AppId).ToDictionary(g => g.Key, g => g.Count());
            foreach (var row in records)
            {
                var id = row!["id"]!.GetValue<Int32>();
                var online = onlineByApp.TryGetValue(id, out var n) ? n : 0;
                Assert.Equal(online, row["online"]!.GetValue<Int32>());
            }

            var page2 = await tool.InvokeAsync(Args(new Dictionary<String, Object>
            {
                ["keyword"] = token,
                ["category"] = Category,
                ["page"] = 2,
                ["page_size"] = 2,
            }), Ctx(projectId));
            var node2 = JsonSerializer.SerializeToNode(page2)!;
            var pageParam = new PageParameter
            {
                PageIndex = 2,
                PageSize = 2,
                Sort = App.__.Id,
                RetrieveTotalCount = true,
            };
            var expected2 = App.Search(projectId, Category, null, DateTime.MinValue, DateTime.MinValue, token, pageParam);
            Assert.Equal(expected2.Select(e => e.Id).ToArray(), node2["data"]!["records"]!.AsArray().Select(e => e!["id"]!.GetValue<Int32>()).ToArray());
            Assert.Single(node2["data"]!["records"]!.AsArray());
        }
        finally
        {
            Cleanup(projectId);
            Cleanup(otherProjectId);
        }
    }

    [Fact(DisplayName = "app_topology 走 FindAllByAppId")]
    public async Task AppTopology_UsesFindAllByAppId()
    {
        const Int32 projectId = 910222;
        Cleanup(projectId);
        var token = Token();
        try
        {
            var app = InsertApp(projectId, token + "a", Category, true, null);
            var other = InsertApp(projectId, token + "b", Category, true, null);
            var provide = new AppService { AppId = app.Id, ServiceName = token + "-api", Address = "10.0.0.1:80", Healthy = true, Enable = true };
            var consume = new AppConsume { AppId = app.Id, ServiceName = token + "-db", Address = "10.0.0.2:3306", Enable = false };
            var noise = new AppService { AppId = other.Id, ServiceName = token + "-noise", Address = "10.0.0.3:80", Healthy = false, Enable = true };
            provide.Insert();
            consume.Insert();
            noise.Insert();

            var result = await new AppTopologyTool().InvokeAsync(Args(new Dictionary<String, Object> { ["app_id"] = app.Id }), Ctx(projectId));
            var node = JsonSerializer.SerializeToNode(result)!;
            var provides = node["data"]!["provides"]!.AsArray().Select(e => e!["ServiceName"]!.GetValue<String>()).ToArray();
            var consumes = node["data"]!["consumes"]!.AsArray().Select(e => e!["ServiceName"]!.GetValue<String>()).ToArray();

            Assert.Equal(AppService.FindAllByAppId(app.Id).Select(e => e.ServiceName).ToArray(), provides);
            Assert.Equal(AppConsume.FindAllByAppId(app.Id).Select(e => e.ServiceName).ToArray(), consumes);
            Assert.DoesNotContain(noise.ServiceName, provides);
        }
        finally
        {
            Cleanup(projectId);
        }
    }

    [Fact(DisplayName = "service_lookup 走项目内服务名包含匹配，精确匹配保持不变")]
    public async Task ServiceLookup_UsesContainsWithinProject()
    {
        const Int32 projectId = 910223;
        const Int32 otherProjectId = 910228;
        Cleanup(projectId);
        Cleanup(otherProjectId);
        var token = Token();
        try
        {
            var app = InsertApp(projectId, token + "a", Category, true, null);
            var other = InsertApp(otherProjectId, token + "b", Category, true, null);
            var hit = new AppService { AppId = app.Id, ServiceName = token + "-api", Address = "10.1.0.1:80", Healthy = true };
            var miss = new AppService { AppId = app.Id, ServiceName = "pay-" + token[..4], Address = "10.1.0.2:80", Healthy = false };
            var outside = new AppService { AppId = other.Id, ServiceName = token + "-ext", Address = "10.1.0.3:80", Healthy = true };
            var consume = new AppConsume { AppId = app.Id, ServiceName = token + "-client", Address = "10.1.0.4:80", Enable = true };
            hit.Insert();
            miss.Insert();
            outside.Insert();
            consume.Insert();

            Assert.DoesNotContain(AppService.FindAllByService(token), e => e.Id == hit.Id);

            var result = await new ServiceLookupTool().InvokeAsync(Args(new Dictionary<String, Object> { ["service_name"] = token }), Ctx(projectId));
            var node = JsonSerializer.SerializeToNode(result)!;
            var providers = node["data"]!["providers"]!.AsArray().Select(e => e!["ServiceName"]!.GetValue<String>()).OrderBy(e => e).ToArray();
            var consumers = node["data"]!["consumers"]!.AsArray().Select(e => e!["ServiceName"]!.GetValue<String>()).OrderBy(e => e).ToArray();

            Assert.Equal(AppService.FindAllByServiceName(token, projectId).Select(e => e.ServiceName).OrderBy(e => e).ToArray(), providers);
            Assert.Equal(AppConsume.FindAllByServiceName(token, projectId).Select(e => e.ServiceName).OrderBy(e => e).ToArray(), consumers);
            Assert.Contains(hit.ServiceName, providers);
            Assert.DoesNotContain(miss.ServiceName, providers);
            Assert.DoesNotContain(outside.ServiceName, providers);
            Assert.Contains(consume.ServiceName, consumers);

            var folded = token.ToUpperInvariant();
            var foldedResult = await new ServiceLookupTool().InvokeAsync(Args(new Dictionary<String, Object> { ["service_name"] = folded }), Ctx(projectId));
            var foldedNode = JsonSerializer.SerializeToNode(foldedResult)!;
            var foldedConsumers = foldedNode["data"]!["consumers"]!.AsArray().Select(e => e!["ServiceName"]!.GetValue<String>()).OrderBy(e => e).ToArray();
            Assert.Equal(AppConsume.FindAllByServiceName(folded, projectId).Select(e => e.ServiceName).OrderBy(e => e).ToArray(), foldedConsumers);
            Assert.Contains(consume.ServiceName, foldedConsumers);
            Assert.DoesNotContain(outside.ServiceName, foldedConsumers);
        }
        finally
        {
            Cleanup(projectId);
            Cleanup(otherProjectId);
        }
    }

    [Fact(DisplayName = "service_lookup 缺少 service_name 时返回参数错误")]
    public async Task ServiceLookup_MissingName_Throws()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => new ServiceLookupTool().InvokeAsync(Args(new { }), Ctx(910223)));
        Assert.Equal(-32602, ex.Code);
    }

    [Fact(DisplayName = "app_health 走 QueryHealth，本项目多条部署取编号更大的那条，错误日志只计错误级别")]
    public async Task AppHealth_UsesQueryHealth()
    {
        const Int32 projectId = 910224;
        const Int32 otherProjectId = 910227;
        Cleanup(projectId);
        var token = Token();
        AppClientLog infoLog = null;
        AppClientLog warnLog = null;
        AppClientLog errorLog = null;
        try
        {
            var app = InsertApp(projectId, token, Category, true, "3.0");
            InsertOnline(app, projectId);
            InsertOnline(app, projectId);

            var olderStat = new AppMinuteStat { AppId = app.Id, StatTime = new DateTime(2026, 11, 5, 8, 0, 0), Total = 10, Errors = 10 };
            var newerStat = new AppMinuteStat { AppId = app.Id, StatTime = new DateTime(2026, 11, 5, 8, 10, 0), Total = 200, Errors = 4 };
            olderStat.Insert();
            newerStat.Insert();

            var olderDeploy = new AppDeploy { Name = "c" + token, AppId = app.Id, ProjectId = projectId, Version = "0.9.0", Enable = true };
            var deploy = new AppDeploy { Name = "d" + token, AppId = app.Id, ProjectId = projectId, Version = "1.2.3", Enable = true };
            var otherDeploy = new AppDeploy { Name = "e" + token, AppId = app.Id, ProjectId = otherProjectId, Version = "9.9.9", Enable = true };
            olderDeploy.Insert();
            deploy.Insert();
            otherDeploy.Insert();
            Assert.True(deploy.Id > olderDeploy.Id);

            var olderPipeline = new AppPipeline { Name = "o" + token, DeployId = deploy.Id, ProjectId = projectId, Enable = true };
            var pipeline = new AppPipeline { Name = "p" + token, DeployId = deploy.Id, ProjectId = projectId, Enable = true };
            olderPipeline.Insert();
            pipeline.Insert();
            Assert.True(pipeline.Id > olderPipeline.Id);
            var seed = Math.Abs(Guid.NewGuid().GetHashCode() % 1_000_000_000) + 3;
            var trapRun = new AppPipelineRun { Id = seed, PipelineId = olderPipeline.Id, Status = PipelineStatus.Failed, CreateTime = DateTime.Now };
            var olderRun = new AppPipelineRun { Id = seed + 1, PipelineId = pipeline.Id, Status = PipelineStatus.Building, CreateTime = DateTime.Now };
            var newerRun = new AppPipelineRun { Id = seed + 2, PipelineId = pipeline.Id, Status = PipelineStatus.Success, CreateTime = DateTime.Now };
            trapRun.Insert();
            olderRun.Insert();
            newerRun.Insert();
            Assert.True(newerRun.Id > olderRun.Id);

            infoLog = new AppClientLog { AppId = app.Id, ClientId = "c" + token, Kind = "Info", Message = token + "-info" };
            warnLog = new AppClientLog { AppId = app.Id, ClientId = "c" + token, Kind = "Warn", Message = token + "-warn" };
            errorLog = new AppClientLog { AppId = app.Id, ClientId = "c" + token, Kind = "Error", Message = token + "-error" };
            infoLog.Insert();
            warnLog.Insert();
            errorLog.Insert();

            var snap = app.QueryHealth(projectId);
            var statPage = new PageParameter { PageIndex = 1, PageSize = 1, Sort = AppMinuteStat.__.StatTime, Desc = true };
            var latest = AppMinuteStat.Search(app.Id, 0, DateTime.MinValue, DateTime.MinValue, null, statPage).First();
            Assert.Equal(AppOnline.FindAllByAppId(app.Id).Count, snap.Online);
            Assert.Equal(2, snap.Online);
            Assert.Equal(Math.Round(4d / 200d, 4), latest.ErrorRate, 4);
            Assert.Equal(latest.ErrorRate, snap.ErrorRate!.Value);
            Assert.Equal("1.2.3", snap.DeployVersion);
            Assert.Equal(PipelineStatus.Success.ToString(), snap.PipelineStatus);
            Assert.Contains(errorLog.Message, snap.RecentErrors);
            Assert.DoesNotContain(infoLog.Message, snap.RecentErrors);
            Assert.DoesNotContain(warnLog.Message, snap.RecentErrors);

            var result = await new AppHealthTool().InvokeAsync(Args(new Dictionary<String, Object> { ["app_id"] = app.Id }), Ctx(projectId));
            var data = JsonSerializer.SerializeToNode(result)!["data"]!;
            Assert.Equal(snap.Online, data["online"]!.GetValue<Int32>());
            Assert.Equal(snap.ErrorRate!.Value, data["error_rate"]!.GetValue<Double>());
            Assert.Equal(snap.DeployVersion, data["deploy_version"]!.GetValue<String>());
            Assert.Equal(snap.PipelineStatus, data["pipeline_status"]!.GetValue<String>());
            Assert.Equal(snap.RecentErrors.Length, data["recent_errors"]!.AsArray().Count);
            Assert.Contains(errorLog.Message, data["recent_errors"]!.AsArray().Select(e => e!.GetValue<String>()));
        }
        finally
        {
            if (infoLog != null && infoLog.Id > 0) infoLog.Delete();
            if (warnLog != null && warnLog.Id > 0) warnLog.Delete();
            if (errorLog != null && errorLog.Id > 0) errorLog.Delete();
            Cleanup(projectId);
        }
    }

    private static App InsertApp(Int32 projectId, String name, String category, Boolean enable, String version, String displayName = null)
    {
        var app = new App
        {
            ProjectId = projectId,
            Name = name,
            DisplayName = displayName,
            Category = category,
            Enable = enable,
            Version = version,
        };
        app.Insert();
        return app;
    }

    private static void InsertOnline(App app, Int32 projectId)
    {
        var online = new AppOnline
        {
            AppId = app.Id,
            ProjectId = projectId,
            Client = "c" + Guid.NewGuid().ToString("n")[..12],
        };
        online.Insert();
    }
}
