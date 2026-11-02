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
        // 找一个有应用的项目
        var app = Stardust.Data.App.FindAll().FirstOrDefault();
        Assert.NotNull(app);

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
        Assert.True(pagination["total"]!.GetValue<Int32>() >= 0);
    }

    [Fact(DisplayName = "M1a: config_set 返回含 rollback_hint")]
    public async Task ConfigSet_ReturnsRollbackHint()
    {
        // 找一个真实 AppConfig
        var cfg = AppConfig.FindAll().FirstOrDefault();
        Assert.NotNull(cfg);

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

    [Fact(DisplayName = "M1a: 写工具未注册 IMcpOperations 时抛 32000")]
    public async Task ConfigSet_NoOperations_Throws32000()
    {
        var cfg = AppConfig.FindAll().FirstOrDefault();
        Assert.NotNull(cfg);

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
}
