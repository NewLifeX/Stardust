using Stardust.Data.Platform;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>
/// 浏览器「新增 MCP 令牌 → 绑定用户/项目 → 调 stardust_app_list」与 McpGuard 校验的对照测试。
/// 与 <see cref="McpAuthzTests.NonMemberUserToken_BusinessRejected"/> 的区别：本类走 POST /api/Platform/McpToken（OnInsert 默认 scope），后者仅用初始化器 Insert 种子令牌。
/// </summary>
[Collection(nameof(McpApiTestCollection))]
public class McpTokenCreateFlowTests : McpSdkTestBase
{
    public McpTokenCreateFlowTests(ITestOutputHelper output) : base(output) { }

    /// <summary>绑定用户非 ProjectUser 成员：平台 API 创建成功，但 MCP 业务工具 -32003（复现浏览器建令牌后调用失败）。</summary>
    [Fact(DisplayName = "集成-建令牌(POST)：用户非项目成员 → stardust_app_list -32003")]
    public async Task PlatformCreateToken_NonMemberUser_AppListRejected()
    {
        var accessToken = await McpPlatformApiHelper.TryLoginAdminAsync();
        if (String.IsNullOrEmpty(accessToken))
        {
            Output.WriteLine("⚠ 跳过：6681 管理员登录未拿到 AccessToken（验证码/MFA 或环境未起）");
            return;
        }

        var projectId = McpTestDataInitializer.Project.Id;
        const Int32 userId = 1; // admin；在「MCP 测试」项目通常无 ProjectUser（与 mcpapi-nonmember 种子一致）
        Assert.False(
            ProjectUser.FindAll(ProjectUser._.UserId == userId & ProjectUser._.ProjectId == projectId & ProjectUser._.Enable == true).Any(),
            "前置条件：测试用户不应已是项目成员，否则无法复现 NeedProject 成员校验");

        var name = McpTestDataInitializer.Prefix + "flow-nonmember-" + Guid.NewGuid().ToString("N")[..8];
        var created = await McpPlatformApiHelper.CreateMcpTokenAsync(accessToken!, name, userId, projectId, scopesCsv: "");
        Assert.True(created.Ok, $"平台创建令牌应成功（与浏览器一致）：{created.Message}");

        try
        {
            Output.WriteLine($"✅ POST 创建令牌 Id={created.Id}，UserId={userId} ProjectId={projectId}，与调用侧 McpContext 一致");

            await RunSdkAsync(created.Token, async client =>
            {
                var mpe = await AssertRejectedAsync(async () =>
                    await client.CallToolAsync("stardust_app_list", new Dictionary<String, Object?>()), -32003);
                Assert.Contains("不是项目", mpe.Message);
                Output.WriteLine($"✅ stardust_app_list 被拒（预期）：{mpe.Message}");
            });
        }
        finally
        {
            await McpPlatformApiHelper.DeleteMcpTokenAsync(accessToken!, created.Id);
        }
    }

    /// <summary>绑定用户=0（不校验 ProjectUser）：同一路径 POST 创建后 app_list 应成功。</summary>
    [Fact(DisplayName = "集成-建令牌(POST)：UserId=0 → stardust_app_list 成功")]
    public async Task PlatformCreateToken_NoBoundUser_AppListOk()
    {
        var accessToken = await McpPlatformApiHelper.TryLoginAdminAsync();
        if (String.IsNullOrEmpty(accessToken))
        {
            Output.WriteLine("⚠ 跳过：6681 管理员登录未拿到 AccessToken");
            return;
        }

        var projectId = McpTestDataInitializer.Project.Id;
        var name = McpTestDataInitializer.Prefix + "flow-nouser-" + Guid.NewGuid().ToString("N")[..8];
        var created = await McpPlatformApiHelper.CreateMcpTokenAsync(accessToken!, name, userId: 0, projectId, scopesCsv: "");
        Assert.True(created.Ok, created.Message);

        try
        {
            await RunSdkAsync(created.Token, async client =>
            {
                var result = await client.CallToolAsync("stardust_app_list", new Dictionary<String, Object?>());
                AssertOk(result);
                Output.WriteLine("✅ UserId=0 令牌 stardust_app_list 成功（跳过 ProjectUser 成员校验）");
            });
        }
        finally
        {
            await McpPlatformApiHelper.DeleteMcpTokenAsync(accessToken!, created.Id);
        }
    }

    /// <summary>先 POST 成员，再为该用户建令牌，stardust_app_list 应成功。结束时删掉本次成员，避免破坏「非成员」用例。</summary>
    [Fact(DisplayName = "集成-加成员(POST)：成员用户令牌 → stardust_app_list 成功")]
    public async Task PlatformAddMember_ThenToken_AppListOk()
    {
        var accessToken = await McpPlatformApiHelper.TryLoginAdminAsync();
        if (String.IsNullOrEmpty(accessToken))
        {
            Output.WriteLine("⚠ 跳过：6681 管理员登录未拿到 AccessToken");
            return;
        }

        var projectId = McpTestDataInitializer.Project.Id;
        const Int32 userId = 1;
        var existed = ProjectUser.FindByProjectIdAndUserId(projectId, userId);
        if (existed != null)
        {
            Output.WriteLine($"⚠ 跳过：用户 {userId} 已是项目 {projectId} 的成员（Id={existed.Id}），不改这条关系");
            return;
        }

        var added = await McpPlatformApiHelper.CreateProjectUserAsync(accessToken!, projectId, userId);
        Assert.True(added.Ok, $"POST ProjectUser 应成功：{added.Message}");

        var tokenId = 0;
        try
        {
            var listed = await McpPlatformApiHelper.ListProjectUserIdsAsync(accessToken!, projectId);
            Assert.True(listed.Ok, listed.Message);
            Assert.Contains(added.Id, listed.Ids);

            var other = await McpPlatformApiHelper.ListProjectUserIdsAsync(accessToken!, 999999);
            Assert.True(other.Ok, other.Message);
            Assert.DoesNotContain(added.Id, other.Ids);

            var name = McpTestDataInitializer.Prefix + "flow-member-" + Guid.NewGuid().ToString("N")[..8];
            var created = await McpPlatformApiHelper.CreateMcpTokenAsync(accessToken!, name, userId, projectId, scopesCsv: "");
            Assert.True(created.Ok, created.Message);
            tokenId = created.Id;

            await RunSdkAsync(created.Token, async client =>
            {
                var result = await client.CallToolAsync("stardust_app_list", new Dictionary<String, Object?>());
                AssertOk(result);
                Output.WriteLine("✅ 成员用户令牌 stardust_app_list 成功");
            });
        }
        finally
        {
            if (tokenId > 0) await McpPlatformApiHelper.DeleteMcpTokenAsync(accessToken!, tokenId);
            await McpPlatformApiHelper.DeleteProjectUserAsync(accessToken!, added.Id);
            var left = ProjectUser.FindByProjectIdAndUserId(projectId, userId);
            left?.Delete();
        }
    }
}
