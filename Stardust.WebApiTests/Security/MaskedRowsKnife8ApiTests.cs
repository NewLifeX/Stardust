using System.Text.Json;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Security;

/// <summary>
/// task-95 二期刀8/N2（2026-11-12，lead 放行令）HTTP 集成式迁移（2026-11-14 Q2 裁决）：
/// AppDeployNode.UserName 行级脱敏三件套。①Search 行值级置 null（键不删）；②Detail 掩码回显 "*****"
/// （Vue 编辑抽屉走 Detail 取数，防 F1 静默清库）；③PUT 守卫——提交 "*****" 从库恢复原值并清脏标记，
/// 空串放行=显式清空。与原 ServerTests 反射版差异：Valid GET 回显非 API 面不再断言；守卫走真实 PUT 管道；
/// 读回走实体 Find 直连查询。父行（AppDeploy/Node）真插保证 Valid 外键校验通过。
/// </summary>
[Collection(WebApiCollection.Name)]
public class MaskedRowsKnife8ApiTests
{
    private const String Mask = "*****";

    private readonly ApiClient _api;

    public MaskedRowsKnife8ApiTests(WebApiTestFactory factory, ITestOutputHelper output) =>
        _api = new(factory, output);

    [Fact(DisplayName = "task-95 刀8/N2：AppDeployNode 列表行 UserName 键在值 null；Detail 掩码回显 '*****'")]
    public async Task AppDeployNode_Row_Masked_And_Detail_Echoes()
    {
        var tag = "mw8a-" + Guid.NewGuid().ToString("N")[..8];
        var (deploy, node, e) = Seed(tag, userName: "u-" + tag);
        try
        {
            var rows = await ApiRowAssert.GetRowsAsync(_api, "Deployment/AppDeployNode?pageIndex=0&pageSize=200");
            var mine = ApiRowAssert.Mine(rows, "deployName", tag);
            Assert.True(mine.Count >= 1, $"应能查到造数行 {tag}*，实际行数={rows.Count}");

            ApiRowAssert.Masked(mine, "userName", "AppDeployNode");

            // Detail 掩码回显 "*****"（Vue 编辑抽屉取数侧）
            var api = await _api.GetJsonAsync($"Deployment/AppDeployNode/{e.Id}");
            Assert.Equal(Mask, api.Data.GetProperty("userName").GetString());
        }
        finally { e.Delete(); deploy.Delete(); node.Delete(); }
    }

    [Fact(DisplayName = "task-95 刀8/N2：AppDeployNode PUT 守卫——'*****' 恢复原值真写读回不变；空串放行=显式清空")]
    public async Task AppDeployNode_Put_Guard_Restores_And_Empty_Passes()
    {
        var tag = "mw8b-" + Guid.NewGuid().ToString("N")[..8];
        var (deploy, node, e) = Seed(tag, userName: "real-" + tag);
        try
        {
            // F1 提交体=Detail 全量展开（含 DeployId/NodeId，Valid 强校验必填）：userName 原样回传 '*****'
            var put = new
            {
                id = e.Id,
                deployId = e.DeployId,
                nodeId = e.NodeId,
                deployName = e.DeployName,
                userName = Mask,
                enable = true,
            };
            var api = await _api.PutApiAsync<JsonElement>($"Deployment/AppDeployNode/{e.Id}", put);

            var back = AppDeployNode.FindById(e.Id);
            Assert.NotNull(back);
            Assert.Equal("real-" + tag, back.UserName);

            // 空串放行：显式清空（空值不走哨兵分支直通写库）
            var clear = new
            {
                id = e.Id,
                deployId = e.DeployId,
                nodeId = e.NodeId,
                deployName = e.DeployName,
                userName = "",
                enable = true,
            };
            var api2 = await _api.PutApiAsync<JsonElement>($"Deployment/AppDeployNode/{e.Id}", clear);

            var cleared = AppDeployNode.FindById(e.Id);
            Assert.NotNull(cleared);
            Assert.True(String.IsNullOrEmpty(cleared.UserName),
                $"空串 PUT 应显式清空 UserName，实际='{cleared.UserName}'");
        }
        finally { e.Delete(); deploy.Delete(); node.Delete(); }
    }

    /// <summary>造数：父 AppDeploy + Node 真插（Valid 外键校验需要），再插 AppDeployNode。</summary>
    private static (AppDeploy deploy, Node node, AppDeployNode node2) Seed(String tag, String userName)
    {
        var deploy = new AppDeploy
        {
            Name = tag + "-dp", AppId = 0, ProjectId = 0, Category = "unit", Enable = true,
        };
        deploy.Insert();
        var node = new Node
        {
            Name = tag + "-nd", Code = tag + "-code", Enable = true,
        };
        node.Insert();
        var e = new AppDeployNode
        {
            DeployId = deploy.Id, NodeId = node.ID, DeployName = tag + "-dn", UserName = userName, Enable = true,
        };
        e.Insert();
        return (deploy, node, e);
    }
}
