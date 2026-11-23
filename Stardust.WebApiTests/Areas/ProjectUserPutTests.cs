using System.Text.Json;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using XCode.Membership;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>项目用户关系 PUT 集合路径部分更新的集成测试（走查 C06/C07 修复 TDD，j-config 排程令 2026-11-10）。
/// 契约（Stardust.Web.Vue projectMember.logic.ts:206-222）：PUT /Platform/ProjectUser body {id, enable}，
/// 部分更新、未提交字段保持原值；前端仅 query 携带 projectId 作页面导航，不参与更新。
/// 修复前：NewLife.Cube(API版) 泛型 Update 要求主键在路径 {id}，集合路径 PUT 无路由匹配返回 404
/// （run9 实走证据：三形态全 404，同通道 POST/DELETE 均 200）。</summary>
[Collection(WebApiCollection.Name)]
public class ProjectUserPutTests
{
    private readonly ApiClient _api;

    public ProjectUserPutTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    [Fact(DisplayName = "PUT 集合路径 body{id,enable} 翻转启用态且未提交字段保持原值")]
    public async Task Put_CollectionPath_PartialUpdate_FlipsEnable()
    {
        // 种子：项目直插（实体哨兵式，同 ServerTests/GalaxyProjectTests；EnsureLink 由 POST 通道真实校验）
        var project = new GalaxyProject { Name = "put-test-" + Guid.NewGuid().ToString("n")[..8], Enable = true };
        project.Insert();
        var rowId = 0;
        try
        {
            var user = User.FindByName("admin");
            Assert.NotNull(user);

            // 前置造行：与走查 run9 s6 同形态（POST 集合路径 200 工作正常）
            var created = await _api.PostApiAsync<JsonElement>($"Platform/ProjectUser?projectId={project.Id}",
                new { projectId = project.Id, userId = user.ID, enable = false });
            rowId = created.Data.GetProperty("id").GetInt32();
            Assert.True(rowId > 0, $"POST 建行未返回主键：{created.Data}");

            // 契约形态：PUT 集合路径 + body 仅 {id, enable}
            var rs = await _api.PutApiAsync<ProjectUser>($"Platform/ProjectUser?projectId={project.Id}",
                new { id = rowId, enable = true });
            Assert.True(rs.Data.Enable, "信封 data 应回传启用态");

            // 库态：启用翻转；未提交字段（projectId/userId）保持原值
            var row = ProjectUser.FindById(rowId);
            Assert.NotNull(row);
            Assert.True(row.Enable, "库中启用态应为 true");
            Assert.Equal(project.Id, row.ProjectId);
            Assert.Equal(user.ID, row.UserId);
        }
        finally
        {
            if (rowId > 0) ProjectUser.FindById(rowId)?.Delete();
            project.Delete();
        }
    }

    [Fact(DisplayName = "PUT 未知编号返回失败信封而非 HTTP 404")]
    public async Task Put_UnknownId_FailsEnvelope()
    {
        // 失败路径：信封级断言只要求 HTTP 成功+可反序列化，Code 由测试自行断言
        var rs = await _api.PutApiEnvelopeAsync<ProjectUser>("Platform/ProjectUser", new { id = 2_000_000_000, enable = true });

        Assert.NotEqual(0, rs.Code);
        Assert.Contains("does not exist", rs.Message);
    }
}
