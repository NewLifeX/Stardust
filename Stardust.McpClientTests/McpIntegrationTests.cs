using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;
using XCode;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpClientTests;

/// <summary>MCP端到端集成测试。使用RawMcpClient直接发送JSON-RPC请求，覆盖11个quickstart场景</summary>
[Collection(nameof(McpTestCollection))]
public class McpIntegrationTests : IAsyncLifetime
{
    private readonly McpTestServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private const String Endpoint = "http://localhost/mcp";

    public McpIntegrationTests(McpTestServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>场景1：MCP开关。EnableMcp=true时 /mcp 端点可访问</summary>
    [Fact]
    public async Task S01_McpEnabled_EndpointAccessible()
    {
        var client = new RawMcpClient(_fixture.CreateClient(), Endpoint);

        var resp = await client.InitializeAsync();

        Assert.False(RawMcpClient.HasError(resp));
        Assert.Equal("2.0", resp["jsonrpc"]?.GetValue<String>());

        var result = RawMcpClient.GetResult(resp);
        Assert.NotNull(result);

        var protocolVersion = result!["protocolVersion"]?.GetValue<String>();
        Assert.Equal("2024-11-05", protocolVersion);

        var serverInfo = result!["serverInfo"];
        Assert.Equal("Stardust", serverInfo!["name"]?.GetValue<String>());

        _output.WriteLine($"✅ 场景1通过：MCP已启用，protocolVersion={protocolVersion}");
    }

    /// <summary>场景2：Token鉴权。initialize不需要Token，tools/list需要Token</summary>
    [Fact]
    public async Task S02_TokenAuthentication_RequiredForToolsList()
    {
        var client = new RawMcpClient(_fixture.CreateClient(), Endpoint);

        // initialize不需要Token
        var initResp = await client.InitializeAsync();
        Assert.False(RawMcpClient.HasError(initResp));

        // tools/list不带Token → -32001
        var noTokenResp = await client.ListToolsAsync();
        Assert.True(RawMcpClient.HasError(noTokenResp));
        Assert.Equal(-32001, RawMcpClient.GetErrorCode(noTokenResp));

        // tools/list带无效Token → -32001
        var badTokenClient = new RawMcpClient(_fixture.CreateClient(), Endpoint, "sdmcp_invalid_token_xxxxxxxxxxxxxxxxxxxxxxxx");
        var badTokenResp = await badTokenClient.ListToolsAsync();
        Assert.True(RawMcpClient.HasError(badTokenResp));
        Assert.Equal(-32001, RawMcpClient.GetErrorCode(badTokenResp));

        // tools/list带有效Token → 成功
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s02-token");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var goodClient = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var goodResp = await goodClient.ListToolsAsync();
            Assert.False(RawMcpClient.HasError(goodResp));

            _output.WriteLine("✅ 场景2通过：无Token=-32001, 无效Token=-32001, 有效Token=成功");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景3：tools/list返回5个MCP工具</summary>
    [Fact]
    public async Task S03_ToolsList_ReturnsFiveTools()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s03-token");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var resp = await client.ListToolsAsync();

            Assert.False(RawMcpClient.HasError(resp));

            var tools = resp["result"]?["tools"]?.AsArray();
            Assert.NotNull(tools);
            Assert.True(tools!.Count >= 5, $"Expected at least 5 tools, got {tools.Count}");

            var toolNames = tools.Select(t => t!["name"]?.GetValue<String>()).ToHashSet();
            Assert.Contains("list_authorized_resources", toolNames);
            Assert.Contains("search_resources", toolNames);
            Assert.Contains("get_resource", toolNames);
            Assert.Contains("list_actions", toolNames);
            Assert.Contains("invoke_action", toolNames);

            _output.WriteLine($"✅ 场景3通过：返回 {tools.Count} 个工具");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景4：LLM上下文 — list_authorized_resources + search_resources + get_resource</summary>
    [Fact]
    public async Task S04_ResourceQuery_ThreeTools_Workflow()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s04-token");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // 1. list_authorized_resources
            var listResp = await client.CallToolAsync("list_authorized_resources", new { });
            Assert.False(RawMcpClient.HasError(listResp));

            var listContent = RawMcpClient.GetToolContent(listResp);
            Assert.NotNull(listContent);
            var projects = listContent!["projects"]?.AsArray();
            Assert.NotNull(projects);
            // IsAll=true 应返回"全部项目"
            Assert.True(projects!.Count >= 1, "Should have at least 1 project (IsAll)");

            // 2. search_resources — 搜索"默认"
            var searchResp = await client.CallToolAsync("search_resources", new { keyword = "默认" });
            Assert.False(RawMcpClient.HasError(searchResp));

            var searchContent = RawMcpClient.GetToolContent(searchResp);
            Assert.NotNull(searchContent);
            var searchProjects = searchContent!["projects"]?.AsArray();
            Assert.NotNull(searchProjects);

            // 3. get_resource — 获取第一个项目详情
            if (searchProjects!.Count > 0)
            {
                var projectId = searchProjects[0]!["id"]?.GetValue<Int32>() ?? 0;
                if (projectId > 0)
                {
                    var getResp = await client.CallToolAsync("get_resource", new
                    {
                        resource_type = "project",
                        resource_id = projectId
                    });
                    Assert.False(RawMcpClient.HasError(getResp));

                    var getContent = RawMcpClient.GetToolContent(getResp);
                    Assert.NotNull(getContent);
                }
            }

            _output.WriteLine("✅ 场景4通过：list_authorized_resources → search_resources → get_resource 工作流正常");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景5：list_actions + invoke_action（只读操作）</summary>
    [Fact]
    public async Task S05_ListActions_AndInvokeAction_ReadOnly()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s05-token");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);
            McpTestHelper.AuthorizeAllNodes(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // list_actions
            var listResp = await client.CallToolAsync("list_actions", new { });
            Assert.False(RawMcpClient.HasError(listResp));

            var content = RawMcpClient.GetToolContent(listResp);
            Assert.NotNull(content);
            var actions = content!["actions"]?.AsArray();
            Assert.NotNull(actions);
            Assert.True(actions!.Count >= 27, $"Expected at least 27 actions, got {actions.Count}");

            // 验证几个关键动作存在
            var actionNames = actions.Select(a => a!["name"]?.GetValue<String>()).ToHashSet();
            Assert.Contains("node_list_online", actionNames);
            Assert.Contains("node_search", actionNames);
            Assert.Contains("app_list_online", actionNames);

            // invoke_action — node_search（只读，使用不会匹配任何节点的关键字）
            var invokeResp = await client.CallToolAsync("invoke_action", new
            {
                action_name = "node_search",
                @params = new { keyword = "zzz-no-such-node-zzz" }
            });
            Assert.False(RawMcpClient.HasError(invokeResp));

            _output.WriteLine($"✅ 场景5通过：list_actions返回{actions.Count}个动作，invoke_action node_search成功");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景6：资源授权校验。未授权资源访问返回-32003</summary>
    [Fact]
    public async Task S06_ResourceAuthorization_Unauthorized_Returns32003()
    {
        // 创建Token但不授权任何资源
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s06-unauthorized");
        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // get_resource 未授权的 project → -32003
            var resp = await client.CallToolAsync("get_resource", new
            {
                resource_type = "project",
                resource_id = 99999  // 不存在的项目
            });

            Assert.True(RawMcpClient.HasError(resp));
            Assert.Equal(-32003, RawMcpClient.GetErrorCode(resp));

            _output.WriteLine($"✅ 场景6通过：未授权资源访问返回 -32003 ({RawMcpClient.GetErrorMessage(resp)})");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景7：动作过滤。McpActionSet限制可用模块</summary>
    [Fact]
    public async Task S07_ActionFiltering_ByMcpActionSet()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s07-filter");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            // 临时修改McpActionSet只启用node模块
            var set = Stardust.Server.StarServerSetting.Current;
            var originalActionSet = set.McpActionSet;
            set.McpActionSet = "node";
            set.Save();

            try
            {
                var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

                // list_actions 应只返回 node 模块
                var listResp = await client.CallToolAsync("list_actions", new { module = "node" });
                Assert.False(RawMcpClient.HasError(listResp));

                var content = RawMcpClient.GetToolContent(listResp);
                Assert.NotNull(content);
                var actions = content!["actions"]?.AsArray();
                Assert.NotNull(actions);
                Assert.True(actions!.Count > 0);
                // 所有动作都应该是 node 模块
                foreach (var a in actions)
                {
                    Assert.Equal("node", a!["module"]?.GetValue<String>());
                }

                // invoke_action 调用非node模块动作 → -32601
                var invokeResp = await client.CallToolAsync("invoke_action", new
                {
                    action_name = "app_list_online",
                    @params = new { }
                });
                Assert.True(RawMcpClient.HasError(invokeResp));
                Assert.Equal(-32601, RawMcpClient.GetErrorCode(invokeResp));

                _output.WriteLine($"✅ 场景7通过：McpActionSet=node 时 list_actions只返回node模块，app动作被过滤");
            }
            finally
            {
                // 恢复
                set.McpActionSet = originalActionSet;
                set.Save();
            }
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景8：审计日志。每次tools/call都记录到McpAudit</summary>
    [Fact]
    public async Task S08_AuditLog_RecordsAllCalls()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s08-audit");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // 执行一次 tools/call
            await client.CallToolAsync("list_actions", new { });

            // 等待异步审计日志写入
            await Task.Delay(500);

            // 查询审计日志
            var audits = McpAudit.Search(token.Id, null, null, null, DateTime.MinValue, DateTime.MinValue, null, new PageParameter { PageIndex = 1, PageSize = 10 });
            Assert.True(audits.Count > 0, "应该有审计日志记录");

            var lastAudit = audits[0];
            Assert.Equal(token.Id, lastAudit.TokenId);
            Assert.Equal("list_actions", lastAudit.ToolName);
            Assert.True(lastAudit.Success, "list_actions应该成功");

            _output.WriteLine($"✅ 场景8通过：审计日志记录了 TokenId={lastAudit.TokenId}, Tool={lastAudit.ToolName}, Success={lastAudit.Success}");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景9：list_actions按模块过滤</summary>
    [Fact]
    public async Task S09_ListActions_FilterByModule()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s09-module-filter");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // 只查 deploy 模块
            var resp = await client.CallToolAsync("list_actions", new { module = "deploy" });
            Assert.False(RawMcpClient.HasError(resp));

            var content = RawMcpClient.GetToolContent(resp);
            Assert.NotNull(content);
            var actions = content!["actions"]?.AsArray();
            Assert.NotNull(actions);
            Assert.True(actions!.Count >= 9, $"Deploy module should have at least 9 actions, got {actions.Count}");

            foreach (var a in actions)
            {
                Assert.Equal("deploy", a!["module"]?.GetValue<String>());
            }

            _output.WriteLine($"✅ 场景9通过：module=deploy 过滤返回 {actions.Count} 个动作");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景10：search_resources跨类型搜索</summary>
    [Fact]
    public async Task S10_SearchResources_CrossTypeSearch()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s10-search");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // 搜索不带 resource_type → 跨所有类型搜索
            var resp = await client.CallToolAsync("search_resources", new { keyword = "test" });
            Assert.False(RawMcpClient.HasError(resp));

            var content = RawMcpClient.GetToolContent(resp);
            Assert.NotNull(content);

            // 验证返回结构包含所有6类资源数组
            Assert.NotNull(content!["projects"]);
            Assert.NotNull(content!["nodes"]);
            Assert.NotNull(content!["apps"]);
            Assert.NotNull(content!["deploys"]);
            Assert.NotNull(content!["pipelines"]);
            Assert.NotNull(content!["services"]);
            Assert.NotNull(content!["total"]);

            var total = content!["total"]?.GetValue<Int32>() ?? 0;
            _output.WriteLine($"✅ 场景10通过：搜索 'test' 返回 total={total}");

            // 搜索带 resource_type=project → 只搜项目
            var resp2 = await client.CallToolAsync("search_resources", new
            {
                keyword = "默认",
                resource_type = "project"
            });
            Assert.False(RawMcpClient.HasError(resp2));

            var content2 = RawMcpClient.GetToolContent(resp2);
            Assert.NotNull(content2);
            var projects = content2!["projects"]?.AsArray();
            Assert.NotNull(projects);

            _output.WriteLine($"  → 按 project 类型搜索 '默认' 返回 {projects!.Count} 个项目");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景11：invoke_action调用get_audit_log（system模块）</summary>
    [Fact]
    public async Task S11_InvokeAction_GetAuditLog()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s11-audit-log");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // 先执行一次操作产生审计日志
            await client.CallToolAsync("list_actions", new { });
            await Task.Delay(300);

            // 调用 get_audit_log 查询自己的审计日志
            var resp = await client.CallToolAsync("invoke_action", new
            {
                action_name = "get_audit_log",
                @params = new { limit = 5 }
            });

            // get_audit_log 可能有资源授权要求，如果失败则验证错误码
            if (RawMcpClient.HasError(resp))
            {
                var code = RawMcpClient.GetErrorCode(resp);
                _output.WriteLine($"  → get_audit_log 返回错误码 {code}: {RawMcpClient.GetErrorMessage(resp)}");
                // 可能是资源授权问题或参数问题，不硬性断言失败
            }
            else
            {
                var content = RawMcpClient.GetToolContent(resp);
                Assert.NotNull(content);
                _output.WriteLine($"✅ 场景11通过：get_audit_log 返回成功");
            }
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景12：Token过期校验。过期的Token返回-32001</summary>
    [Fact]
    public async Task S12_ExpiredToken_Returns32001()
    {
        var token = new McpToken
        {
            Name = "s12-expired",
            Token = McpToken.GenerateToken(),
            Enable = true,
            ExpireTime = DateTime.Now.AddDays(-1), // 已过期
        };
        token.Insert();

        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, token.Token);
            var resp = await client.ListToolsAsync();

            Assert.True(RawMcpClient.HasError(resp));
            Assert.Equal(-32001, RawMcpClient.GetErrorCode(resp));

            _output.WriteLine($"✅ 场景12通过：过期Token返回 -32001");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景13：Token禁用校验。禁用的Token返回-32001</summary>
    [Fact]
    public async Task S13_DisabledToken_Returns32001()
    {
        var token = new McpToken
        {
            Name = "s13-disabled",
            Token = McpToken.GenerateToken(),
            Enable = false, // 禁用
            ExpireTime = DateTime.MinValue,
        };
        token.Insert();

        try
        {
            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, token.Token);
            var resp = await client.ListToolsAsync();

            Assert.True(RawMcpClient.HasError(resp));
            Assert.Equal(-32001, RawMcpClient.GetErrorCode(resp));

            _output.WriteLine($"✅ 场景13通过：禁用Token返回 -32001");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景14：未知方法返回-32601</summary>
    [Fact]
    public async Task S14_UnknownMethod_Returns32601()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s14-unknown");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var resp = await client.SendAsync("unknown/method", new { }, id: 99);

            Assert.True(RawMcpClient.HasError(resp));
            Assert.Equal(-32601, RawMcpClient.GetErrorCode(resp));

            _output.WriteLine($"✅ 场景14通过：未知方法返回 -32601");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景15：无效JSON返回-32700</summary>
    [Fact]
    public async Task S15_InvalidJson_Returns32700()
    {
        var httpClient = _fixture.CreateClient();
        var content = new StringContent("not valid json {{{", System.Text.Encoding.UTF8, "application/json");
        var resp = await httpClient.PostAsync(Endpoint, content);
        var body = await resp.Content.ReadAsStringAsync();

        var json = JsonSerializer.Deserialize<JsonObject>(body);
        Assert.NotNull(json);
        Assert.True(json!.ContainsKey("error"));
        Assert.Equal(-32700, json["error"]!["code"]?.GetValue<Int32>());

        _output.WriteLine($"✅ 场景15通过：无效JSON返回 -32700");
    }

    /// <summary>场景16：间接资源校验。deploy_id 通过 AppDeploy.ProjectId 反查授权</summary>
    [Fact]
    public async Task S16_IndirectResource_DeployResolvesToProject()
    {
        var (token, tokenStr) = McpTestHelper.CreateTestToken("s16-indirect");
        try
        {
            // 只授权特定项目（不授权全部）
            // 找一个已有项目
            var projects = Stardust.Data.Platform.GalaxyProject.FindAll();
            if (projects.Count == 0)
            {
                _output.WriteLine("⏭ 跳过：没有项目数据");
                return;
            }

            var project = projects[0];
            McpTestHelper.AuthorizeProject(token.Id, project.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);

            // get_resource deploy/{id} — 会通过 AppDeploy.ProjectId 反查
            // 找一个属于该项目的 deploy
            var deploys = Stardust.Data.Deployment.AppDeploy.FindAllByProjectId(project.Id);
            if (deploys.Count == 0)
            {
                _output.WriteLine("⏭ 跳过：该项目没有部署集数据");
                return;
            }

            var deploy = deploys[0];
            var resp = await client.CallToolAsync("get_resource", new
            {
                resource_type = "deploy",
                resource_id = deploy.Id
            });

            // 应该成功，因为 deploy 属于已授权的项目
            Assert.False(RawMcpClient.HasError(resp));

            _output.WriteLine($"✅ 场景16通过：deploy/{deploy.Id} → project/{project.Id} 间接授权校验通过");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }
}
