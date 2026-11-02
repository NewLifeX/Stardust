using NewLife.Log;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>测试集合 Fixture。执行一次幂等初始化（登录/开MCP/建项目/建令牌/建测试数据），供集合内所有测试类共享。
/// 注意：CollectionFixture 无法注入 ITestOutputHelper（xUnit 限制），输出走 XTrace。</summary>
public class McpApiTestFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        XTrace.WriteLine($"[McpApiTests] 初始化开始，服务 {McpTestDataInitializer.BaseUrl}");
        await McpTestDataInitializer.EnsureAllAsync();
        XTrace.WriteLine($"[McpApiTests] 初始化完成：项目Id={McpTestDataInitializer.Project?.Id}，令牌 {McpTestDataInitializer.Tokens.Count} 个");
    }

    public Task DisposeAsync()
    {
        // 测试数据幂等保留（可复用），不清理
        return Task.CompletedTask;
    }
}
