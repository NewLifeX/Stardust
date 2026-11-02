using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>边界测试：参数缺失、非法值、未知工具、L2 无 confirm、敏感值脱敏、分页截断、未知方法。
/// 面向真实 WebApi(6681) /mcp 端点。错误码断言基于探针实测：SDK 远程错误保留在 ErrorCode；
/// McpResult 的 records/pagination 位于 data 属性下（探针实测 config_get/audit_list/whoami 返回结构）。</summary>
[Collection(nameof(McpApiTestCollection))]
public class McpBoundaryTests : McpSdkTestBase
{
    public McpBoundaryTests(ITestOutputHelper output) : base(output) { }

    private String FullToken => McpTestDataInitializer.Tokens["mcpapi-full"];

    /// <summary>L2 工具 node_command 危险命令无 confirm → 拒绝（-32003 危险规则）</summary>
    [Fact(DisplayName = "边界-1：node_command 危险命令缺 confirm 被拒")]
    public async Task NodeCommand_Dangerous_NoConfirm_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_node_command", new Dictionary<String, Object?>
                {
                    ["node_id"] = McpTestDataInitializer.NodeId,
                    ["command"] = "rm -rf /tmp/x",
                }), -32003);
            Output.WriteLine($"✅ 危险命令缺confirm被拒：{mpe.Message}");
        });
    }

    /// <summary>L2 工具 app_command 缺 confirm → 拒绝（-32003 高危操作需要确认）</summary>
    [Fact(DisplayName = "边界-2：app_command 缺 confirm 被拒")]
    public async Task AppCommand_NoConfirm_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_app_command", new Dictionary<String, Object?>
                {
                    ["app_id"] = McpTestDataInitializer.AppId,
                    ["command"] = "app/restart",
                }), -32003);
            Output.WriteLine($"✅ app_command 缺confirm被拒：{mpe.Message}");
        });
    }

    /// <summary>L1 工具 deploy_node_operate stop 无 confirm → 拒绝（-32003 stop 必须传 confirm）</summary>
    [Fact(DisplayName = "边界-3：deploy_node_operate stop 缺 confirm 被拒")]
    public async Task DeployNodeOperate_Stop_NoConfirm_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_deploy_node_operate", new Dictionary<String, Object?>
                {
                    ["deploy_node_id"] = McpTestDataInitializer.DeployNodeId,
                    ["action"] = "stop",
                }), -32003);
            Output.WriteLine($"✅ stop缺confirm被拒：{mpe.Message}");
        });
    }

    /// <summary>缺必填参数：node_detail 空参 → 节点 0 不存在（-32003，探针实测 GetInt32 缺省=0 走 McpGuard.Node）</summary>
    [Fact(DisplayName = "边界-4：缺必填参数 node_detail 空参被拒")]
    public async Task MissingRequiredParam_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_node_detail", new Dictionary<String, Object?>()), -32003);
            Output.WriteLine($"✅ 缺参被拒：{mpe.Message}");
        });
    }

    /// <summary>未知工具名 → 被拒（-32601 MethodNotFound）</summary>
    [Fact(DisplayName = "边界-5：未知工具名被拒")]
    public async Task UnknownTool_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_no_such_tool", new Dictionary<String, Object?>()), -32601);
            Output.WriteLine($"✅ 未知工具被拒：{mpe.Message}");
        });
    }

    /// <summary>零 scope 令牌：whoami 可用（唯一无 scope 要求工具），业务工具被拒（-32003 缺少授权范围）</summary>
    [Fact(DisplayName = "边界-6：零 scope 令牌 only whoami，业务工具被拒")]
    public async Task ZeroScope_Token_OnlyWhoAmI()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-zeroscope"];
        await RunSdkAsync(token, async client =>
        {
            // whoami 成功
            var whoami = await client.CallToolAsync("stardust_token_whoami", new Dictionary<String, Object?>());
            AssertOk(whoami);

            // 业务工具被拒（缺 scope）
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_app_list", new Dictionary<String, Object?>()), -32003);
            Output.WriteLine($"✅ 零scope业务工具被拒：{mpe.Message}");
        });
    }

    /// <summary>未绑定项目令牌（ProjectId=0）：whoami 可用，业务工具 -32003（需项目）</summary>
    [Fact(DisplayName = "边界-7：未绑定项目令牌业务工具被拒")]
    public async Task UnboundProject_Token_BusinessRejected()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-unbound"];
        await RunSdkAsync(token, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_app_list", new Dictionary<String, Object?>()), -32003);
            Output.WriteLine($"✅ 未绑定项目业务工具被拒：{mpe.Message}");
        });
    }

    /// <summary>config_get 敏感值脱敏：Password 键返回 ***，普通键返回明文（records 在 data 下）</summary>
    [Fact(DisplayName = "边界-8：config_get 敏感键值脱敏为 ***")]
    public async Task ConfigGet_SensitiveValue_Masked()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var result = await client.CallToolAsync("stardust_config_get", new Dictionary<String, Object?>
            {
                ["config_id"] = McpTestDataInitializer.ConfigId,
                ["key"] = "Password",
            });
            var content = AssertOk(result);
            var data = content.GetProperty("data");
            var records = data.GetProperty("records");
            Assert.True(records.GetArrayLength() > 0, "应有记录");
            var first = records[0];
            Assert.Equal("***", first.GetProperty("value").GetString());
            Assert.True(first.GetProperty("masked").GetBoolean(), "敏感键应标记 masked");
            Output.WriteLine("✅ 敏感键 Password 值已脱敏为 ***");
        });
    }

    /// <summary>config_get 普通键返回明文</summary>
    [Fact(DisplayName = "边界-9：config_get 普通键返回明文")]
    public async Task ConfigGet_NormalValue_NotMasked()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var result = await client.CallToolAsync("stardust_config_get", new Dictionary<String, Object?>
            {
                ["config_id"] = McpTestDataInitializer.ConfigId,
                ["key"] = "ServerName",
            });
            var content = AssertOk(result);
            var records = content.GetProperty("data").GetProperty("records");
            Assert.True(records.GetArrayLength() > 0, "应有记录");
            var first = records[0];
            Assert.Equal("mcp-api-value", first.GetProperty("value").GetString());
            Assert.False(first.GetProperty("masked").GetBoolean(), "普通键不应 masked");
            Output.WriteLine("✅ 普通键 ServerName 返回明文");
        });
    }

    /// <summary>分页截断：page_size>100 被截断到上限（返回条数 ≤100）</summary>
    [Fact(DisplayName = "边界-10：page_size 超过上限被截断")]
    public async Task PageSize_OverLimit_Truncated()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var result = await client.CallToolAsync("stardust_audit_list", new Dictionary<String, Object?>
            {
                ["page_size"] = 500,
            });
            var content = AssertOk(result);
            var records = content.GetProperty("data").GetProperty("records");
            Assert.True(records.GetArrayLength() <= 100, $"page_size 应被截断到 ≤100，实际 {records.GetArrayLength()}");
            Output.WriteLine($"✅ page_size=500 被截断：返回 {records.GetArrayLength()} 条");
        });
    }

    /// <summary>config_rollback 用不存在的历史 → 拒绝（-32602 配置历史不存在，探针实测）</summary>
    [Fact(DisplayName = "边界-11：config_rollback 用不存在历史被拒")]
    public async Task ConfigRollback_UnknownHistory_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_config_rollback", new Dictionary<String, Object?>
                {
                    ["history_id"] = 99999999, // 不存在的配置历史
                }), -32602);
            Output.WriteLine($"✅ 不存在历史被拒：{mpe.Message}");
        });
    }

    /// <summary>不存在的资源 ID：node_detail 99999999 → -32003（节点不存在或不属于当前项目，探针实测非 -32602）</summary>
    [Fact(DisplayName = "边界-12：不存在的资源 ID 被拒")]
    public async Task MissingResource_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_node_detail", new Dictionary<String, Object?>
                {
                    ["node_id"] = 99999999,
                }), -32003);
            Output.WriteLine($"✅ 不存在资源被拒：{mpe.Message}");
        });
    }

    /// <summary>whoami 返回令牌绑定信息（data 为对象：token_name/project_id/scopes/risk_level/server）</summary>
    [Fact(DisplayName = "边界-13：whoami 返回完整令牌信息")]
    public async Task WhoAmI_ReturnsFullInfo()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var result = await client.CallToolAsync("stardust_token_whoami", new Dictionary<String, Object?>());
            var content = AssertOk(result);
            var data = content.GetProperty("data");
            Assert.True(data.TryGetProperty("token_name", out _), "应含 token_name");
            Assert.True(data.TryGetProperty("project_id", out var pid) && pid.GetInt32() == McpTestDataInitializer.Project.Id, "应含绑定的项目Id");
            Assert.True(data.TryGetProperty("scopes", out var scopes) && scopes.GetArrayLength() > 0, "应含 scopes");
            Assert.True(data.TryGetProperty("risk_level", out _), "应含 risk_level");
            Assert.True(data.TryGetProperty("server", out _), "应含 server");
            Output.WriteLine($"✅ whoami 返回完整信息：project_id={pid.GetInt32()}");
        });
    }
}
