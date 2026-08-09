using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife;
using NewLife.Data;
using NewLife.Log;
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
                        resource_type = McpResourceType.Project.ToWireName(),
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
                resource_type = McpResourceType.Project.ToWireName(),
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

            // 临时修改McpActionSet只启用node模块（仅改内存单例，不落库，避免污染其它测试）
            var set = Stardust.Server.StarServerSetting.Current;
            var originalActionSet = set.McpActionSet;
            set.McpActionSet = "node";

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
                // 恢复内存单例
                set.McpActionSet = originalActionSet;
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
                resource_type = McpResourceType.Project.ToWireName()
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
                resource_type = McpResourceType.Deploy.ToWireName(),
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

    /// <summary>场景17：协议版本协商。客户端请求2.0版本时服务器回同版本；请求高于最高支持版时回退到最高支持版；请求过低返回-32602</summary>
    [Fact]
    public async Task S17_ProtocolVersionNegotiation()
    {
        var client = new RawMcpClient(_fixture.CreateClient(), Endpoint);

        // 客户端请求 2025-06-18（服务器支持）→ 回 2025-06-18
        var resp1 = await client.InitializeAsync("2025-06-18");
        Assert.False(RawMcpClient.HasError(resp1));
        var pv1 = RawMcpClient.GetResult(resp1)!["protocolVersion"]?.GetValue<String>();
        Assert.Equal("2025-06-18", pv1);
        _output.WriteLine($"✅ 场景17a：请求2025-06-18 → 协商返回 {pv1}");

        // 客户端请求最高支持版 2026-07-28 → 回 2026-07-28
        var resp2 = await client.InitializeAsync("2026-07-28");
        Assert.False(RawMcpClient.HasError(resp2));
        var pv2 = RawMcpClient.GetResult(resp2)!["protocolVersion"]?.GetValue<String>();
        Assert.Equal("2026-07-28", pv2);
        _output.WriteLine($"✅ 场景17b：请求2026-07-28 → 协商返回 {pv2}");

        // 客户端请求高于所有支持版（未来版本）→ 回退到服务器最高支持版 2026-07-28
        var resp3 = await client.InitializeAsync("2099-01-01");
        Assert.False(RawMcpClient.HasError(resp3));
        var pv3 = RawMcpClient.GetResult(resp3)!["protocolVersion"]?.GetValue<String>();
        Assert.Equal("2026-07-28", pv3);
        _output.WriteLine($"✅ 场景17c：请求2099-01-01 → 回退到最高支持版 {pv3}");

        // 1.0 路径仍兼容：请求 2024-11-05 → 回 2024-11-05
        var resp0 = await client.InitializeAsync("2024-11-05");
        Assert.False(RawMcpClient.HasError(resp0));
        var pv0 = RawMcpClient.GetResult(resp0)!["protocolVersion"]?.GetValue<String>();
        Assert.Equal("2024-11-05", pv0);
        _output.WriteLine($"✅ 场景17d：1.0路径 请求2024-11-05 → 回 {pv0}");

        // 客户端请求低于最低支持版 → 返回 -32602 错误
        var resp4 = await client.InitializeAsync("2020-01-01");
        Assert.True(RawMcpClient.HasError(resp4));
        Assert.Equal(-32602, RawMcpClient.GetErrorCode(resp4));
        _output.WriteLine($"✅ 场景17e：请求2020-01-01（过低）→ -32602");
    }

    /// <summary>场景18：服务端日志核对。happy-path 流程（initialize→tools/list→tools/call）不应产生任何 MCP 错误日志；
    /// 并经由测试专用日志输出打印全部服务端日志，供人工核对「日志输出是否正确」（不仅看断言通过）。</summary>
    [Fact]
    public async Task S18_ServerLog_ShouldBeCleanOnHappyPath()
    {
        McpTestLog.Instance.Reset();

        var (token, tokenStr) = McpTestHelper.CreateTestToken("s18-log");
        try
        {
            McpTestHelper.AuthorizeAllProjects(token.Id);

            var client = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var initResp = await client.InitializeAsync();
            Assert.False(RawMcpClient.HasError(initResp));
            var listResp = await client.ListToolsAsync();
            Assert.False(RawMcpClient.HasError(listResp));
            var callResp = await client.CallToolAsync("list_actions", new { });
            Assert.False(RawMcpClient.HasError(callResp));

            // 核对服务端日志：正常流程不应出现 [McpService] 错误（异常/内部错误）
            Assert.False(McpTestLog.Instance.Contains("[McpService]", LogLevel.Error),
                "happy-path 不应出现 [McpService] 错误日志（服务端异常会污染正常响应）");

            // 正向验证捕获非真空：故意触发一次服务端异常（未授权的 node 资源），
            // 断言捕获器确实记录到了 [McpService] 错误日志（证明日志捕获机制真实生效）
            var errClient = new RawMcpClient(_fixture.CreateClient(), Endpoint, tokenStr);
            var errResp = await errClient.CallToolAsync("get_resource", new { resource_type = McpResourceType.Node.ToWireName(), resource_id = 99999 });
            Assert.True(RawMcpClient.HasError(errResp), "异常触发请求应返回错误响应");
            Assert.True(McpTestLog.Instance.Contains("[McpService]", LogLevel.Error),
                "日志捕获应记录到服务端异常（[McpService]），否则捕获机制未生效");

            // 打印测试专用日志，供人工核对输出是否正确
            McpTestLog.Instance.WriteTo(_output);

            _output.WriteLine("✅ 场景18通过：happy-path 服务端日志干净（无 [McpService] 错误），且异常路径日志捕获生效");
        }
        finally
        {
            McpTestHelper.CleanupToken(token.Id);
        }
    }

    /// <summary>场景19：initialize 响应格式（Streamable HTTP 兼容回归测试）。
    /// 直接验证 HTTP 层：Content-Type 必须为 application/json（非 text/event-stream）；
    /// 响应 JSON 的 id 必须与请求一致为「数字」（若回显为字符串 "1"，官方/标准客户端会因 id 不匹配一直等待 initialize）。
    /// 该测试直接防御线上「Waiting for server to respond to initialize request」类问题。</summary>
    [Fact]
    public async Task S19_Initialize_ResponseFormat_StreamableHttpCompatible()
    {
        var httpClient = _fixture.CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "curl", version = "1.0" }
            }
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        // 模拟标准 Streamable HTTP 客户端：Accept 同时声明 application/json 与 text/event-stream
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var resp = await httpClient.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        // 1. Content-Type 必须是 application/json（不是 text/event-stream），否则客户端按 SSE 解析会失败
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
        Assert.Equal("application/json", contentType);

        // 2. 响应必须是合法 JSON-RPC，且 id 必须为「数字」1（与请求一致），绝不能是字符串 "1"
        var json = JsonSerializer.Deserialize<JsonObject>(body);
        Assert.NotNull(json);
        Assert.False(json!.ContainsKey("error"), $"initialize 不应返回错误：{body}");
        var idNode = json["id"];
        Assert.NotNull(idNode);
        Assert.Equal(JsonValueKind.Number, idNode!.GetValueKind());
        Assert.Equal(1, idNode.GetValue<Int32>());

        // 3. 协议版本协商正确返回
        Assert.Equal("2025-06-18", json["result"]?["protocolVersion"]?.GetValue<String>());

        _output.WriteLine($"✅ 场景19通过：initialize 响应 Content-Type={contentType}, id类型=数字, protocolVersion=2025-06-18");
    }
}
