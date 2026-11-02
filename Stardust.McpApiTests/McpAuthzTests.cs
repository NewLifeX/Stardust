using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>越权测试：跨项目资源访问、未授权资源、禁用/过期令牌、非成员用户。
/// 面向真实 WebApi(6681) /mcp 端点，断言 McpGuard 的 -32003/-32001 语义（SDK 远程错误码保留在 ErrorCode）。</summary>
[Collection(nameof(McpApiTestCollection))]
public class McpAuthzTests : McpSdkTestBase
{
    public McpAuthzTests(ITestOutputHelper output) : base(output) { }

    private String FullToken => McpTestDataInitializer.Tokens["mcpapi-full"];

    /// <summary>伪造 Token：tools/list 被拒（-32001 未经授权）</summary>
    [Fact(DisplayName = "越权-1：伪造 Token 被拒")]
    public async Task FakeToken_Rejected()
    {
        await RunSdkAsync("sdmcp_fake_token_00000000000000000000000000000000", async client =>
        {
            var mpe = await AssertRejectedAsync(async () => await client.ListToolsAsync(), -32001);
            Output.WriteLine($"✅ 伪造Token被拒：{mpe.Message}");
        });
    }

    /// <summary>禁用令牌：被拒（-32001 令牌已禁用或已过期）</summary>
    [Fact(DisplayName = "越权-2：禁用令牌被拒")]
    public async Task DisabledToken_Rejected()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-disabled"];
        await RunSdkAsync(token, async client =>
        {
            var mpe = await AssertRejectedAsync(async () => await client.ListToolsAsync(), -32001);
            Output.WriteLine($"✅ 禁用令牌被拒：{mpe.Message}");
        });
    }

    /// <summary>过期令牌：被拒（-32001 令牌已禁用或已过期）</summary>
    [Fact(DisplayName = "越权-3：过期令牌被拒")]
    public async Task ExpiredToken_Rejected()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-expired"];
        await RunSdkAsync(token, async client =>
        {
            var mpe = await AssertRejectedAsync(async () => await client.ListToolsAsync(), -32001);
            Output.WriteLine($"✅ 过期令牌被拒：{mpe.Message}");
        });
    }

    /// <summary>非成员用户令牌（UserId>0 且非 ProjectUser 成员）：业务工具被拒（-32003 未绑定项目成员）</summary>
    [Fact(DisplayName = "越权-4：非项目成员用户令牌业务工具被拒")]
    public async Task NonMemberUserToken_BusinessRejected()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-nonmember"];
        await RunSdkAsync(token, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_app_list", new Dictionary<String, Object?>()), -32003);
            Output.WriteLine($"✅ 非成员令牌业务工具被拒：{mpe.Message}");
        });
    }

    /// <summary>未授权资源令牌（只授权了项目，未授权应用/节点）：访问应用被拒（-32003 CheckResource）</summary>
    [Fact(DisplayName = "越权-5：未授权应用资源的令牌访问应用被拒")]
    public async Task UnauthorizedAppResource_Rejected()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-unauth"];
        await RunSdkAsync(token, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_app_health", new Dictionary<String, Object?>
                {
                    ["app_id"] = McpTestDataInitializer.AppId,
                }), -32003);
            Output.WriteLine($"✅ 未授权应用被拒：{mpe.Message}");
        });
    }

    /// <summary>跨项目资源访问：主令牌绑项目4，访问项目1的应用（StarServer Id=1）应被拒（-32003）</summary>
    [Fact(DisplayName = "越权-6：跨项目访问他人应用被拒")]
    public async Task CrossProject_App_Rejected()
    {
        // Id=1 是默认项目(项目1)下的 StarServer 应用（现有种子数据，不属于测试项目）
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_app_health", new Dictionary<String, Object?>
                {
                    ["app_id"] = 1,
                }), -32003);
            Output.WriteLine($"✅ 跨项目应用被拒：{mpe.Message}");
        });
    }

    /// <summary>跨项目节点访问：访问初始化器建的「他人项目节点」(ProjectId=1) 应被拒（-32003）</summary>
    [Fact(DisplayName = "越权-7：跨项目访问他人节点被拒")]
    public async Task CrossProject_Node_Rejected()
    {
        var crossNodeId = McpTestDataInitializer.CrossNodeId;
        Assert.True(crossNodeId > 0, "初始化器应已建他人项目节点（ProjectId=1）作为越权靶");
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_node_detail", new Dictionary<String, Object?>
                {
                    ["node_id"] = crossNodeId,
                }), -32003);
            Output.WriteLine($"✅ 跨项目节点被拒：{mpe.Message}");
        });
    }

    /// <summary>跨项目配置：访问项目1的配置（Common Id=1）应被拒</summary>
    [Fact(DisplayName = "越权-8：跨项目访问他人配置被拒")]
    public async Task CrossProject_Config_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_config_get", new Dictionary<String, Object?>
                {
                    ["config_id"] = 1,
                }), -32003);
            Output.WriteLine($"✅ 跨项目配置被拒：{mpe.Message}");
        });
    }

    /// <summary>跨项目部署：访问项目1的部署集（StarServer Id=1）应被拒</summary>
    [Fact(DisplayName = "越权-9：跨项目访问他人部署被拒")]
    public async Task CrossProject_Deploy_Rejected()
    {
        await RunSdkAsync(FullToken, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_deploy_status", new Dictionary<String, Object?>
                {
                    ["deploy_id"] = 1,
                }), -32003);
            Output.WriteLine($"✅ 跨项目部署被拒：{mpe.Message}");
        });
    }

    /// <summary>越权写操作：未授权资源的令牌调写工具也应被拒（-32003）</summary>
    [Fact(DisplayName = "越权-10：未授权资源令牌写工具被拒")]
    public async Task UnauthorizedResource_Write_Rejected()
    {
        var token = McpTestDataInitializer.Tokens["mcpapi-unauth"];
        await RunSdkAsync(token, async client =>
        {
            var mpe = await AssertRejectedAsync(async () =>
                await client.CallToolAsync("stardust_config_set", new Dictionary<String, Object?>
                {
                    ["config_id"] = McpTestDataInitializer.ConfigId,
                    ["configs"] = new Dictionary<String, Object?> { ["x"] = "y" },
                }), -32003);
            Output.WriteLine($"✅ 未授权写工具被拒：{mpe.Message}");
        });
    }
}
