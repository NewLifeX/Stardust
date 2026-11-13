using System.Linq;
using System.Text.Json;
using Moq;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Mcp;
using Stardust.Mcp.Tools.Config;
using Stardust.Mcp.Tools.Registry;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// MCP 工具返回信封特征测试。覆盖本轮修复：
/// <list type="bullet">
///   <item>M1a: 写工具返回 rollback_hint（McpResult.Ok 第4参数）</item>
///   <item>M1b: 列表工具返回 pagination（McpResult.Paged）</item>
/// </list>
/// 部分测试连真实 SQLite，用 sentinel 数据。
/// </summary>
public class McpToolResultTests
{
    /// <summary>简易 IServiceProvider，返回注册的单例</summary>
    private sealed class SimpleProvider : IServiceProvider
    {
        private readonly Dictionary<Type, Object> _services;
        public SimpleProvider(Dictionary<Type, Object> services) => _services = services;
        public Object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var v) ? v : null;
    }

    private static JsonElement ToArgs(String json) => JsonSerializer.Deserialize<JsonElement>(json);

    /// <summary>哨兵项目编号。共享测试库里的应用多为 ProjectId=0，MCP 工具要求令牌绑定项目，需自建数据</summary>
    private const Int32 SentinelProjectId = 900001;

    /// <summary>创建哨兵应用（归属哨兵项目）</summary>
    private static App CreateSentinelApp() 
    {
        var app = new App
        {
            Name = "mcpt_" + Guid.NewGuid().ToString("N")[..8],
            Secret = "mcpt-secret",
            Enable = true,
            ProjectId = SentinelProjectId,
        };
        app.Insert();

        return app;
    }

    /// <summary>创建哨兵应用对应的配置记录</summary>
    private static AppConfig CreateSentinelConfig(Int32 appId)
    {
        var cfg = new AppConfig
        {
            Name = "mcpc_" + Guid.NewGuid().ToString("N")[..8],
            AppId = appId,
            ProjectId = SentinelProjectId,
            Version = 1,
            Enable = true,
        };
        cfg.Insert();

        return cfg;
    }

    [Fact(DisplayName = "M1a: McpResult.Ok 带 rollbackHint 时序列化含 rollback_hint 字段")]
    public void Ok_WithRollbackHint_SerializesField()
    {
        var result = McpResult.Ok("done", new { a = 1 }, null, "用旧配置覆盖即可回滚");
        var node = JsonSerializer.SerializeToNode(result)!;
        Assert.Equal("用旧配置覆盖即可回滚", node["rollback_hint"]!.GetValue<String>());
    }

    [Fact(DisplayName = "M1a: McpResult.Ok 不带 rollbackHint 时字段为 null")]
    public void Ok_WithoutRollbackHint_FieldIsNull()
    {
        var result = McpResult.Ok("done", new { a = 1 });
        var node = JsonSerializer.SerializeToNode(result)!;
        Assert.Null(node["rollback_hint"]);
    }

    [Fact(DisplayName = "M1b: McpResult.Paged 含 total/page/page_size")]
    public void Paged_SerializesFields()
    {
        var paged = McpResult.Paged(42, 2, 20);
        var node = JsonSerializer.SerializeToNode(paged)!;
        Assert.Equal(42, node["total"]!.GetValue<Int32>());
        Assert.Equal(2, node["page"]!.GetValue<Int32>());
        Assert.Equal(20, node["page_size"]!.GetValue<Int32>());
    }

    [Fact(DisplayName = "M1b: app_list 返回含 pagination.total")]
    public async Task AppList_ReturnsPagination()
    {
        // 哨兵项目内放一个应用，保证列表非空且项目匹配
        var app = CreateSentinelApp();
        try
        {
            var ctx = new McpContext
            {
                TokenId = 999997,
                ProjectId = app.ProjectId,
                UserId = 0,
                Scopes = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "apps:read" },
            };

            var tool = new AppListTool();
            var result = await tool.InvokeAsync(ToArgs("{}"), ctx);
            var node = JsonSerializer.SerializeToNode(result)!;

            var pagination = node["pagination"];
            Assert.NotNull(pagination);
            Assert.True(pagination["total"]!.GetValue<Int32>() >= 1);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "M1a: config_set 返回含 rollback_hint")]
    public async Task ConfigSet_ReturnsRollbackHint()
    {
        // 自建哨兵应用与配置，避免依赖共享测试库中哪个应用先落库
        var app = CreateSentinelApp();
        var cfg = CreateSentinelConfig(app.Id);
        try
        {
            var mockOps = new Mock<IMcpOperations>();
            mockOps.Setup(o => o.SetConfigAsync(It.IsAny<Int32>(), It.IsAny<IDictionary<String, Object>>()))
                  .ReturnsAsync(1);

            var provider = new SimpleProvider(new Dictionary<Type, Object>
            {
                [typeof(IMcpOperations)] = mockOps.Object,
            });

            var ctx = new McpContext
            {
                TokenId = 999997,
                ProjectId = cfg.ProjectId,
                UserId = 0,
                Scopes = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "config:write" },
                Services = provider,
            };

            var args = ToArgs("{\"config_id\":" + cfg.Id + ",\"configs\":{\"TestKey\":\"TestValue\"}}");
            var tool = new ConfigSetTool();
            var result = await tool.InvokeAsync(args, ctx);
            var node = JsonSerializer.SerializeToNode(result)!;

            var rollbackHint = node["rollback_hint"];
            Assert.NotNull(rollbackHint);
            Assert.False(String.IsNullOrEmpty(rollbackHint!.GetValue<String>()));
        }
        finally
        {
            cfg.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "M1a: 写工具未注册 IMcpOperations 时抛 32000")]
    public async Task ConfigSet_NoOperations_Throws32000()
    {
        var app = CreateSentinelApp();
        var cfg = CreateSentinelConfig(app.Id);
        try
        {
            var provider = new SimpleProvider(new Dictionary<Type, Object>());

            var ctx = new McpContext
            {
                TokenId = 999997,
                ProjectId = cfg.ProjectId,
                UserId = 0,
                Scopes = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "config:write" },
                Services = provider,
            };

            var args = ToArgs("{\"config_id\":" + cfg.Id + ",\"configs\":{\"K\":\"V\"}}");
            var tool = new ConfigSetTool();
            var ex = await Assert.ThrowsAsync<McpException>(() => tool.InvokeAsync(args, ctx));
            Assert.Equal(-32000, ex.Code);
        }
        finally
        {
            cfg.Delete();
            app.Delete();
        }
    }
}
