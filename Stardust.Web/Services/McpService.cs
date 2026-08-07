using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using Stardust.Data.Platform;
using Stardust.Server;
using Stardust.Web.Mcp;
using XCode;

namespace Stardust.Web.Services;

/// <summary>MCP服务。实现JSON-RPC 2.0 over HTTP，提供Token鉴权、资源授权校验、动作注册与路由、审计日志</summary>
public class McpService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StarServerSetting _setting;
    private readonly ITracer _tracer;

    /// <summary>已注册的MCP动作（按Name索引）</summary>
    private readonly ConcurrentDictionary<String, IMcpAction> _actions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>间接资源解析器字典。Key=IndirectEntity名称，Value=反查ProjectId的函数</summary>
    private readonly Dictionary<String, Func<Int32, Int32>> _indirectResolvers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已注册的资源Provider（按ResourceType索引）</summary>
    private readonly Dictionary<String, IResourceProvider> _resourceProviders = new(StringComparer.OrdinalIgnoreCase);

    public McpService(IServiceProvider serviceProvider, StarServerSetting setting, ITracer tracer)
    {
        _serviceProvider = serviceProvider;
        _setting = setting;
        _tracer = tracer;

        RegisterActions();
        RegisterIndirectResolvers();
        RegisterResourceProviders();
    }

    /// <summary>反射扫描所有IMcpAction实现类并注册</summary>
    private void RegisterActions()
    {
        var asm = Assembly.GetExecutingAssembly();
        var actionType = typeof(IMcpAction);
        foreach (var type in asm.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;
            if (!actionType.IsAssignableFrom(type)) continue;

            IMcpAction action;
            try
            {
                // 优先通过DI容器解析（支持构造函数注入），失败时反射创建
                action = (IMcpAction)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, type);
            }
            catch
            {
                action = (IMcpAction)Activator.CreateInstance(type)!;
            }

            if (!action.Name.IsNullOrEmpty())
            {
                _actions[action.Name] = action;
                XTrace.WriteLine("MCP动作注册：{0} ({1})", action.Name, type.Name);
            }
        }
        XTrace.Log.Info("MCP服务共注册 {0} 个动作", _actions.Count);
    }

    /// <summary>注册间接资源解析器（AppDeploy/AppPipeline/AppPipelineRun → ProjectId）</summary>
    private void RegisterIndirectResolvers()
    {
        // AppDeploy.deploy_id → AppDeploy.ProjectId
        _indirectResolvers["AppDeploy"] = id =>
        {
            var e = Stardust.Data.Deployment.AppDeploy.FindById(id);
            return e?.ProjectId ?? 0;
        };

        // AppPipeline.pipeline_id → AppPipeline.ProjectId
        _indirectResolvers["AppPipeline"] = id =>
        {
            var e = Stardust.Data.Deployment.AppPipeline.FindById(id);
            return e?.ProjectId ?? 0;
        };

        // AppPipelineRun.run_id → AppPipelineRun.PipelineId → AppPipeline.ProjectId（二级查找）
        _indirectResolvers["AppPipelineRun"] = id =>
        {
            var run = Stardust.Data.Deployment.AppPipelineRun.FindById(id);
            if (run == null) return 0;
            var pipeline = Stardust.Data.Deployment.AppPipeline.FindById(run.PipelineId);
            return pipeline?.ProjectId ?? 0;
        };

        // AppService.service_id → AppService.AppId → App.ProjectId（二级查找）
        _indirectResolvers["AppService"] = id =>
        {
            var s = Stardust.Data.AppService.FindById(id);
            if (s == null) return 0;
            var app = Stardust.Data.App.FindById(s.AppId);
            return app?.ProjectId ?? 0;
        };
    }

    /// <summary>反射扫描所有IResourceProvider实现类并注册</summary>
    private void RegisterResourceProviders()
    {
        var asm = Assembly.GetExecutingAssembly();
        var providerType = typeof(IResourceProvider);
        foreach (var type in asm.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;
            if (!providerType.IsAssignableFrom(type)) continue;

            IResourceProvider provider;
            try
            {
                provider = (IResourceProvider)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, type);
            }
            catch
            {
                provider = (IResourceProvider)Activator.CreateInstance(type)!;
            }

            if (!provider.ResourceType.IsNullOrEmpty())
            {
                _resourceProviders[provider.ResourceType] = provider;
                XTrace.WriteLine("MCP资源Provider注册：{0} ({1})", provider.ResourceType, type.Name);
            }
        }
        XTrace.Log.Info("MCP服务共注册 {0} 个资源Provider", _resourceProviders.Count);
    }

    /// <summary>获取已注册的所有动作</summary>
    public IDictionary<String, IMcpAction> GetActions() => _actions;

    /// <summary>主入口。解析JSON-RPC请求、校验Token、路由到对应方法。
    /// acceptSse：客户端Accept是否包含text/event-stream，决定是否用SSE格式包装响应体</summary>
    /// <param name="body">JSON-RPC请求体</param>
    /// <param name="ip">调用方IP</param>
    /// <param name="ua">客户端User-Agent</param>
    /// <param name="authorization">Authorization头（Bearer sdmcp_xxx）</param>
    /// <param name="acceptSse">客户端是否接受SSE流式响应</param>
    public async Task<McpHandleResult> HandleAsync(String body, String ip, String ua, String authorization, Boolean acceptSse = false)
    {
        var sw = Stopwatch.StartNew();
        String? traceId = null;
        Object? id = null;
        String methodName = String.Empty;
        Int32 tokenId = 0;
        String tokenName = String.Empty;
        String? actionName = null;

        try
        {
            // 解析JSON
            if (body.IsNullOrEmpty()) return ErrorResult(null, -32700, "Parse error: empty body", acceptSse);
            JsonElement request;
            try
            {
                request = JsonSerializer.Deserialize<JsonElement>(body);
            }
            catch (Exception ex)
            {
                return ErrorResult(null, -32700, "Parse error: " + ex.Message, acceptSse);
            }

            if (request.ValueKind != JsonValueKind.Object) return ErrorResult(null, -32600, "Invalid Request: not an object", acceptSse);

            // 校验JSON-RPC 2.0
            if (!request.TryGetProperty("jsonrpc", out var v) || v.GetString() != "2.0")
                return ErrorResult(id, -32600, "Invalid Request: missing or invalid jsonrpc field", acceptSse);
            // 回显客户端请求的 id，必须保持原始类型（数字/字符串），否则严格客户端按 id 匹配会失败
            if (request.TryGetProperty("id", out var idEl))
            {
                id = idEl.ValueKind switch
                {
                    JsonValueKind.Number => idEl.GetInt64(),
                    JsonValueKind.String => idEl.GetString(),
                    _ => null
                };
            }
            if (!request.TryGetProperty("method", out var methodEl) || (methodName = methodEl.GetString()).IsNullOrEmpty())
                return ErrorResult(id, -32600, "Invalid Request: missing method", acceptSse);

            // 通知类消息（notifications/*）不需要响应，HTTP层回202
            if (methodName.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase))
                return new McpHandleResult { StatusCode = 202 };

            // initialize 不需要鉴权
            if (methodName == "initialize")
            {
                var initResult = HandleInitialize(request);
                return Result(id, initResult, acceptSse);
            }

            // ping 心跳
            if (methodName == "ping")
                return Result(id, new { }, acceptSse);

            // 其他方法都需要Token鉴权
            McpToken? token = null;
            var tokenStr = ExtractToken(authorization);
            if (tokenStr.IsNullOrEmpty()) return ErrorResult(id, -32001, "Unauthorized: missing Bearer token", acceptSse);

            token = McpToken.FindByToken(tokenStr);
            if (token == null || !McpToken.SafeEquals(token.Token, tokenStr))
                return ErrorResult(id, -32001, "Unauthorized: token not found", acceptSse);
            if (!token.IsValid())
                return ErrorResult(id, -32001, "Unauthorized: token disabled or expired", acceptSse);

            tokenId = token.Id;
            tokenName = token.Name;
            traceId = DefaultSpan.Current?.TraceId;

            // 更新调用统计（审计类写入，失败不影响主响应）
            try
            {
                token.RecordCall(ip);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[McpService] 更新Token调用统计失败（已忽略）：{0}", ex.Message);
            }

            // 构造上下文
            var context = new McpContext
            {
                TokenId = tokenId,
                TokenName = tokenName,
                CallerIp = ip,
                UserAgent = ua,
                TraceId = traceId,
                ServiceProvider = _serviceProvider,
            };

            Object? result;
            switch (methodName)
            {
                case "tools/list":
                    result = HandleToolsList();
                    break;
                case "tools/call":
                    if (!request.TryGetProperty("params", out var paramsEl))
                        return ErrorResult(id, -32602, "Invalid params: missing params", acceptSse);
                    result = await HandleToolsCall(paramsEl, context, sw, actionName);
                    break;
                default:
                    return ErrorResult(id, -32601, $"Method not found: {methodName}", acceptSse);
            }

            return Result(id, result, acceptSse);
        }
        catch (McpException ex)
        {
            XTrace.Log.Error("[McpService] MCP异常 method={0} action={1} code={2} err={3}", methodName, actionName, ex.Code, ex.Message);
            return ErrorResult(id, ex.Code, ex.Message, acceptSse);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpService] 异常 method={0} action={1} err={2}", methodName, actionName, ex);
            return ErrorResult(id, -32603, "Internal error: " + ex.Message, acceptSse);
        }
    }

    /// <summary>从Authorization头提取Bearer Token</summary>
    private static String ExtractToken(String? authorization)
    {
        if (authorization.IsNullOrEmpty()) return String.Empty;
        var prefix = "Bearer ";
        if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return authorization[prefix.Length..].Trim();
        return authorization.Trim();
    }

    /// <summary>处理 initialize 方法。按客户端请求的protocolVersion协商返回</summary>
    private Object HandleInitialize(JsonElement request)
    {
        var requested = request.TryGetProperty("params", out var p) && p.TryGetProperty("protocolVersion", out var pv)
            ? pv.GetString() : null;
        var negotiated = NegotiateProtocolVersion(requested);
        if (negotiated == null)
            throw new McpException(-32602, $"Unsupported protocol version: {requested}. Supported: {String.Join(", ", SupportedProtocolVersions)}");

        return new
        {
            protocolVersion = negotiated,
            serverInfo = new { name = "Stardust", version = "1.0.0" },
            capabilities = new { tools = new { } },
        };
    }

    /// <summary>处理 tools/list 方法。返回5个固定工具的清单</summary>
    private Object HandleToolsList()
    {
        Object[] tools =
        {
            new
            {
                name = "list_authorized_resources",
                description = "查询当前Token授权了哪些资源（项目/节点/应用），返回资源标识符和说明。LLM首次接入时调用此工具建立上下文。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        resource_type = new { type = "string", @enum = new[] { "project", "node", "app" }, description = "可选过滤，不传则返回全部三类" }
                    }
                }
            },
            new
            {
                name = "search_resources",
                description = "按关键字跨类型搜索资源，返回匹配的资源标识符和说明。与get_resource形成搜索→获取的查询原语对。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        keyword = new { type = "string", description = "搜索关键字（匹配资源名称/编码/IP等）" },
                        resource_type = new { type = "string", @enum = new[] { "project", "node", "app", "deploy", "pipeline", "service" }, description = "可选过滤，不传则全搜" }
                    },
                    required = new[] { "keyword" }
                }
            },
            new
            {
                name = "get_resource",
                description = "按资源类型+ID获取单个资源详情。与search_resources配对使用。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        resource_type = new { type = "string", @enum = new[] { "project", "node", "app", "deploy", "pipeline", "service" }, description = "资源类型" },
                        resource_id = new { type = "integer", description = "资源ID" }
                    },
                    required = new[] { "resource_type", "resource_id" }
                }
            },
            new
            {
                name = "list_actions",
                description = "返回当前可调用的动作清单（含name/description/inputSchema/requiredResource）。LLM在执行操作前调用此工具发现能做什么、需要什么参数。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        module = new { type = "string", @enum = new[] { "node", "app", "config", "deploy", "gateway", "monitor", "system" }, description = "可选过滤，不传则返回全部" }
                    }
                }
            },
            new
            {
                name = "invoke_action",
                description = "调用指定动作。这是LLM执行操作的唯一入口，通过action_name+params路由到具体实现。",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        action_name = new { type = "string", description = "动作名（snake_case），如node_send_command。可通过list_actions查询可用动作。" },
                        @params = new { type = "object", description = "动作参数，结构由对应action的inputSchema定义", additionalProperties = true }
                    },
                    required = new[] { "action_name", "params" }
                }
            }
        };
        return new { tools };
    }

    /// <summary>处理 tools/call 方法。根据name路由到5个工具处理方法</summary>
    private async Task<Object> HandleToolsCall(JsonElement @params, McpContext context, Stopwatch sw, String? actionName)
    {
        if (!@params.TryGetProperty("name", out var nameEl))
            throw new InvalidOperationException("Invalid params: missing tool name");
        var name = nameEl.GetString();
        var arguments = @params.TryGetProperty("arguments", out var argEl) ? argEl : default;

        Object content;
        var success = true;
        String? error = null;
        var resolvedActionName = name == "invoke_action" && arguments.TryGetProperty("action_name", out var an) ? an.GetString() : null;
        if (!resolvedActionName.IsNullOrEmpty()) actionName = resolvedActionName;

        try
        {
            switch (name)
            {
                case "list_authorized_resources":
                    content = HandleListAuthorizedResources(arguments, context);
                    break;
                case "search_resources":
                    content = await HandleSearchResources(arguments, context);
                    break;
                case "get_resource":
                    content = await HandleGetResource(arguments, context);
                    break;
                case "list_actions":
                    content = HandleListActions(arguments, context);
                    break;
                case "invoke_action":
                    content = await HandleInvokeAction(arguments, context);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown tool: {name}");
            }
        }
        catch (McpException ex)
        {
            success = false;
            error = ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            success = false;
            error = ex.Message;
            throw;
        }
        finally
        {
            // 写审计日志（best-effort：审计失败绝不能影响主响应，否则并发写锁会污染正常结果）
            try
            {
                McpAudit.WriteAsync(
                    context.TokenId, context.TokenName, name, actionName,
                    context.CallerIp, context.UserAgent,
                    arguments.ToString(), success, error, (Int32)sw.ElapsedMilliseconds, context.TraceId);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[McpService] 写MCP审计日志失败（已忽略）：{0}", ex.Message);
            }
        }

        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(content) } } };
    }

    /// <summary>list_authorized_resources 工具。查Token授权的资源</summary>
    private Object HandleListAuthorizedResources(JsonElement arguments, McpContext context)
    {
        var resourceType = arguments.TryGetProperty("resource_type", out var rt) ? rt.GetString() : null;

        var list = McpTokenResource.FindAllByToken(context.TokenId);
        var projects = new List<Object>();
        var nodes = new List<Object>();
        var apps = new List<Object>();

        foreach (var r in list)
        {
            if (!r.Enable) continue;
            switch (r.ResourceType)
            {
                case "Project" when resourceType.IsNullOrEmpty() || resourceType == "project":
                    if (r.IsAll)
                    {
                        projects.Add(new { id = 0, name = "全部项目", description = "IsAll授权，可访问所有项目" });
                    }
                    else
                    {
                        var p = Stardust.Data.Platform.GalaxyProject.FindById(r.ResourceId);
                        if (p != null) projects.Add(new { id = p.Id, name = p.Name, description = p.Remark });
                    }
                    break;
                case "Node" when resourceType.IsNullOrEmpty() || resourceType == "node":
                    if (r.IsAll)
                    {
                        nodes.Add(new { id = 0, name = "全部节点", description = "IsAll授权，可访问所有节点" });
                    }
                    else
                    {
                        var n = Stardust.Data.Nodes.Node.FindByID(r.ResourceId);
                        if (n != null) nodes.Add(new { id = n.ID, name = n.Name, description = n.Remark, ip = n.IP });
                    }
                    break;
                case "App" when resourceType.IsNullOrEmpty() || resourceType == "app":
                    if (r.IsAll)
                    {
                        apps.Add(new { id = 0, name = "全部应用", description = "IsAll授权，可访问所有应用" });
                    }
                    else
                    {
                        var a = Stardust.Data.App.FindById(r.ResourceId);
                        if (a != null) apps.Add(new { id = a.Id, name = a.Name, description = a.Remark });
                    }
                    break;
            }
        }

        return new { projects, nodes, apps };
    }

    /// <summary>list_actions 工具。返回动作清单</summary>
    private Object HandleListActions(JsonElement arguments, McpContext context)
    {
        var module = arguments.TryGetProperty("module", out var m) ? m.GetString() : null;
        var actionSet = _setting.McpActionSet;
        var enabledModules = actionSet == "*" ? null : actionSet.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

        var actions = new List<Object>();
        foreach (var kv in _actions.OrderBy(e => e.Key))
        {
            var action = kv.Value;
            // 模块过滤
            if (!module.IsNullOrEmpty() && !String.Equals(action.Module, module, StringComparison.OrdinalIgnoreCase)) continue;
            // McpActionSet 过滤
            if (enabledModules != null && !enabledModules.Contains(action.Module, StringComparer.OrdinalIgnoreCase)) continue;

            actions.Add(new
            {
                name = action.Name,
                description = action.Description,
                module = action.Module,
                input_schema = (Object)(action.InputSchema.ValueKind == JsonValueKind.Undefined ? new { type = "object" } : (Object)action.InputSchema),
                required_resource = action.RequiredResource,
            });
        }
        return new { actions };
    }

    /// <summary>search_resources 工具。跨6类资源搜索，按Token授权范围过滤</summary>
    private Task<Object> HandleSearchResources(JsonElement arguments, McpContext context)
    {
        if (!arguments.TryGetProperty("keyword", out var kwEl) || kwEl.ValueKind != JsonValueKind.String)
            throw new McpException(-32602, "Invalid params: missing or invalid keyword");
        var keyword = kwEl.GetString();
        if (keyword.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: keyword is empty");

        var resourceType = arguments.TryGetProperty("resource_type", out var rt) ? rt.GetString() : null;

        // 获取Token授权的项目ID列表（null表示全部项目授权）
        var authorizedProjectIds = McpTokenResource.GetAuthorizedProjectIds(context.TokenId);

        var projects = new List<Object>();
        var nodes = new List<Object>();
        var apps = new List<Object>();
        var deploys = new List<Object>();
        var pipelines = new List<Object>();
        var services = new List<Object>();

        var page = new PageParameter { PageIndex = 1, PageSize = 50 };

        // 1. 搜索项目（GalaxyProject）
        if (resourceType.IsNullOrEmpty() || resourceType == "project")
        {
            var exp = new WhereExpression();
            exp &= Stardust.Data.Platform.GalaxyProject._.Name.Contains(keyword) |
                   Stardust.Data.Platform.GalaxyProject._.Remark.Contains(keyword);
            if (authorizedProjectIds != null)
                exp &= Stardust.Data.Platform.GalaxyProject._.Id.In(authorizedProjectIds);
            foreach (var p in Stardust.Data.Platform.GalaxyProject.FindAll(exp, page))
            {
                projects.Add(new { id = p.Id, name = p.Name, enable = p.Enable, remark = p.Remark });
            }
        }

        // 2. 搜索节点（Node）
        if (resourceType.IsNullOrEmpty() || resourceType == "node")
        {
            var exp = new WhereExpression();
            exp &= Stardust.Data.Nodes.Node._.Code.Contains(keyword) |
                   Stardust.Data.Nodes.Node._.Name.Contains(keyword) |
                   Stardust.Data.Nodes.Node._.IP.Contains(keyword) |
                   Stardust.Data.Nodes.Node._.MachineName.Contains(keyword);
            if (authorizedProjectIds != null)
                exp &= Stardust.Data.Nodes.Node._.ProjectId.In(authorizedProjectIds);
            foreach (var n in Stardust.Data.Nodes.Node.FindAll(exp, page))
            {
                nodes.Add(new { id = n.ID, project_id = n.ProjectId, name = n.Name, code = n.Code, ip = n.IP, enable = n.Enable });
            }
        }

        // 3. 搜索应用（App）
        if (resourceType.IsNullOrEmpty() || resourceType == "app")
        {
            var exp = new WhereExpression();
            exp &= Stardust.Data.App._.Name.Contains(keyword) |
                   Stardust.Data.App._.DisplayName.Contains(keyword);
            if (authorizedProjectIds != null)
                exp &= Stardust.Data.App._.ProjectId.In(authorizedProjectIds);
            foreach (var a in Stardust.Data.App.FindAll(exp, page))
            {
                apps.Add(new { id = a.Id, project_id = a.ProjectId, name = a.Name, display_name = a.DisplayName, enable = a.Enable });
            }
        }

        // 4. 搜索部署集（AppDeploy）
        if (resourceType.IsNullOrEmpty() || resourceType == "deploy")
        {
            var exp = new WhereExpression();
            exp &= Stardust.Data.Deployment.AppDeploy._.Name.Contains(keyword) |
                   Stardust.Data.Deployment.AppDeploy._.Repository.Contains(keyword) |
                   Stardust.Data.Deployment.AppDeploy._.Remark.Contains(keyword);
            if (authorizedProjectIds != null)
                exp &= Stardust.Data.Deployment.AppDeploy._.ProjectId.In(authorizedProjectIds);
            foreach (var d in Stardust.Data.Deployment.AppDeploy.FindAll(exp, page))
            {
                deploys.Add(new { id = d.Id, project_id = d.ProjectId, app_id = d.AppId, name = d.Name, enable = d.Enable, version = d.Version });
            }
        }

        // 5. 搜索流水线（AppPipeline）
        if (resourceType.IsNullOrEmpty() || resourceType == "pipeline")
        {
            var exp = new WhereExpression();
            exp &= Stardust.Data.Deployment.AppPipeline._.Name.Contains(keyword) |
                   Stardust.Data.Deployment.AppPipeline._.Branch.Contains(keyword) |
                   Stardust.Data.Deployment.AppPipeline._.Remark.Contains(keyword);
            if (authorizedProjectIds != null)
                exp &= Stardust.Data.Deployment.AppPipeline._.ProjectId.In(authorizedProjectIds);
            foreach (var p in Stardust.Data.Deployment.AppPipeline.FindAll(exp, page))
            {
                pipelines.Add(new { id = p.Id, project_id = p.ProjectId, deploy_id = p.DeployId, name = p.Name, enable = p.Enable, branch = p.Branch });
            }
        }

        // 6. 搜索服务（AppService，无ProjectId字段，通过App关联过滤）
        if (resourceType.IsNullOrEmpty() || resourceType == "service")
        {
            var exp = new WhereExpression();
            exp &= Stardust.Data.AppService._.ServiceName.Contains(keyword) |
                   Stardust.Data.AppService._.Client.Contains(keyword) |
                   Stardust.Data.AppService._.Address.Contains(keyword) |
                   Stardust.Data.AppService._.Tag.Contains(keyword);
            foreach (var s in Stardust.Data.AppService.FindAll(exp, page))
            {
                // 若Token非全部项目授权，需通过App.ProjectId过滤
                if (authorizedProjectIds != null)
                {
                    var app = Stardust.Data.App.FindById(s.AppId);
                    if (app == null || !authorizedProjectIds.Contains(app.ProjectId)) continue;
                }
                services.Add(new { id = s.Id, app_id = s.AppId, service_name = s.ServiceName, client = s.Client, address = s.Address, enable = s.Enable, healthy = s.Healthy });
            }
        }

        return Task.FromResult<Object>(new
        {
            keyword,
            projects,
            nodes,
            apps,
            deploys,
            pipelines,
            services,
            total = projects.Count + nodes.Count + apps.Count + deploys.Count + pipelines.Count + services.Count,
        });
    }

    /// <summary>get_resource 工具。按资源类型+ID获取详情，框架层校验授权</summary>
    private async Task<Object> HandleGetResource(JsonElement arguments, McpContext context)
    {
        if (!arguments.TryGetProperty("resource_type", out var rtEl) || rtEl.ValueKind != JsonValueKind.String)
            throw new McpException(-32602, "Invalid params: missing or invalid resource_type");
        var resourceType = rtEl.GetString();
        if (resourceType.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: resource_type is empty");

        if (!arguments.TryGetProperty("resource_id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
            throw new McpException(-32602, "Invalid params: missing or invalid resource_id");
        var resourceId = idEl.GetInt32();
        if (resourceId <= 0) throw new McpException(-32602, "Invalid params: resource_id must be a positive integer");

        // 查找Provider
        if (!_resourceProviders.TryGetValue(resourceType, out var provider))
            throw new McpException(-32601, $"Method not found: no provider for resource_type={resourceType}");

        // 框架层授权校验
        var authError = ValidateResourceAccessForGet(resourceType, resourceId, context.TokenId);
        if (!authError.IsNullOrEmpty()) throw new McpException(-32003, authError);

        // 调用Provider
        var result = await provider.GetAsync(resourceId);
        if (result == null) throw new McpException(-32601, $"Resource not found: {resourceType}/{resourceId}");

        return result;
    }

    /// <summary>get_resource 专用的资源授权校验。project/node/app直接校验，deploy/pipeline/service间接校验（通过_indirectResolvers反查ProjectId）</summary>
    private String ValidateResourceAccessForGet(String resourceType, Int32 resourceId, Int32 tokenId)
    {
        // 直接资源：project/node/app
        var directType = resourceType switch
        {
            "project" => "Project",
            "node" => "Node",
            "app" => "App",
            _ => null,
        };
        if (directType != null)
        {
            if (!McpTokenResource.IsAuthorized(tokenId, directType, resourceId))
                return $"Forbidden: {resourceType}/{resourceId} is not authorized for this token";
            return String.Empty;
        }

        // 间接资源：deploy/pipeline/service → 反查ProjectId
        var indirectEntity = resourceType switch
        {
            "deploy" => "AppDeploy",
            "pipeline" => "AppPipeline",
            "service" => "AppService",
            _ => null,
        };
        if (indirectEntity == null) return $"Forbidden: unknown resource_type={resourceType}";

        if (!_indirectResolvers.TryGetValue(indirectEntity, out var resolver))
            return $"Forbidden: indirect resolver not found for entity {indirectEntity}";

        var projectId = resolver(resourceId);
        if (projectId <= 0) return $"Forbidden: cannot resolve ProjectId from {indirectEntity}.Id={resourceId}";

        if (!McpTokenResource.IsAuthorized(tokenId, "Project", projectId))
            return $"Forbidden: project_id={projectId} (resolved from {resourceType}/{resourceId}) is not authorized for this token";

        return String.Empty;
    }

    /// <summary>invoke_action 工具。调用指定动作</summary>
    private async Task<Object> HandleInvokeAction(JsonElement arguments, McpContext context)
    {
        if (!arguments.TryGetProperty("action_name", out var nameEl))
            throw new McpException(-32602, "Invalid params: missing action_name");
        var actionName = nameEl.GetString();
        if (actionName.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: empty action_name");

        if (!_actions.TryGetValue(actionName, out var action))
            throw new McpException(-32601, $"Method not found: action {actionName} not registered");

        // McpActionSet 过滤
        var actionSet = _setting.McpActionSet;
        if (actionSet != "*")
        {
            var enabledModules = actionSet.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            if (!enabledModules.Contains(action.Module, StringComparer.OrdinalIgnoreCase))
                throw new McpException(-32601, $"Method not found: action {actionName} disabled by McpActionSet");
        }

        var @params = arguments.TryGetProperty("params", out var p) ? p : default;

        // 框架层资源授权校验
        if (action.RequiredResource != null)
        {
            var error = ValidateResourceAccess(context.TokenId, action.RequiredResource, @params);
            if (!error.IsNullOrEmpty()) throw new McpException(-32003, error);
        }

        // 调用动作
        return action.InvokeAsync(@params, context);
    }

    /// <summary>框架层资源授权校验。支持直接校验和间接校验</summary>
    private String ValidateResourceAccess(Int32 tokenId, ResourceRequirement req, JsonElement @params)
    {
        if (req.Field.IsNullOrEmpty()) return String.Empty;

        // 提取资源ID
        if (!@params.TryGetProperty(req.Field, out var idEl))
        {
            // 可选字段缺失则跳过校验
            if (req.Optional) return String.Empty;
            return $"Invalid params: missing required field {req.Field}";
        }
        var resourceId = idEl.GetInt32();
        if (resourceId <= 0)
        {
            if (req.Optional) return String.Empty;
            return $"Invalid params: {req.Field} must be a positive integer";
        }

        if (req.Indirect)
        {
            // 间接资源校验：通过IndirectEntity反查ProjectId
            if (req.IndirectEntity.IsNullOrEmpty() || !_indirectResolvers.TryGetValue(req.IndirectEntity, out var resolver))
                return $"Forbidden: indirect resolver not found for entity {req.IndirectEntity}";

            var projectId = resolver(resourceId);
            if (projectId <= 0) return $"Forbidden: cannot resolve ProjectId from {req.IndirectEntity}.Id={resourceId}";

            // 校验项目授权
            if (!McpTokenResource.IsAuthorized(tokenId, "Project", projectId))
                return $"Forbidden: project_id={projectId} (resolved from {req.Field}={resourceId}) is not authorized for this token";
        }
        else
        {
            // 直接资源校验
            var resourceType = req.Type switch
            {
                "project" => "Project",
                "node" => "Node",
                "app" => "App",
                _ => req.Type,
            };
            if (!McpTokenResource.IsAuthorized(tokenId, resourceType, resourceId))
                return $"Forbidden: {req.Field}={resourceId} ({resourceType}) is not authorized for this token";
        }

        return String.Empty;
    }

    #region JSON-RPC 构造

    private static String BuildResult(Object? id, Object? result)
    {
        var resp = new
        {
            jsonrpc = "2.0",
            id,
            result,
        };
        return JsonSerializer.Serialize(resp);
    }

    public static String BuildError(Object? id, Int32 code, String message)
    {
        var resp = new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message },
        };
        return JsonSerializer.Serialize(resp);
    }

    /// <summary>将处理结果写入 HTTP 响应。202=通知无响应体；IsSse=text/event-stream；否则 application/json</summary>
    public static async Task WriteResponseAsync(HttpContext context, McpHandleResult result)
    {
        context.Response.StatusCode = result.StatusCode;
        if (result.StatusCode == 202) return;
        context.Response.ContentType = result.IsSse ? "text/event-stream" : "application/json";
        if (!result.Body.IsNullOrEmpty())
            await context.Response.WriteAsync(result.Body);
    }
    #endregion

    #region 协议版本协商 / 通知 / SSE

    /// <summary>服务器支持的MCP协议版本（从新到旧）。initialize时按客户端请求协商</summary>
    private static readonly String[] SupportedProtocolVersions =
    {
        "2026-07-28",
        "2025-06-18",
        "2025-03-26",
        "2024-11-05",
    };

    /// <summary>协议版本协商：返回≤客户端请求的最高支持版本；请求为空返回最低支持版；比最低还旧返回null</summary>
    private static String? NegotiateProtocolVersion(String? requested)
    {
        if (requested.IsNullOrEmpty()) return SupportedProtocolVersions[^1];
        foreach (var v in SupportedProtocolVersions)
        {
            if (String.CompareOrdinal(v, requested) <= 0) return v;
        }
        return null;
    }

    /// <summary>MCP处理结果的承载对象。StatusCode=202表示通知类无响应体；IsSse表示用SSE格式返回</summary>
    public sealed class McpHandleResult
    {
        public Int32 StatusCode { get; init; } = 200;
        public String? Body { get; init; }
        public Boolean IsSse { get; init; }
    }

    private static McpHandleResult Result(Object? id, Object? result, Boolean acceptSse)
    {
        var json = BuildResult(id, result);
        return Wrap(json, acceptSse);
    }

    private static McpHandleResult ErrorResult(Object? id, Int32 code, String message, Boolean acceptSse = false)
    {
        var json = BuildError(id, code, message);
        return Wrap(json, acceptSse);
    }

    private static McpHandleResult Wrap(String json, Boolean acceptSse)
    {
        // Streamable HTTP 规范允许服务端以 application/json 或 text/event-stream 返回。
        // 本服务为无状态请求-响应模型，统一以 application/json 返回（官方 SDK 2.0 的可靠路径）；
        // SSE 仅用于服务端主动推送流（本服务暂不需要）。acceptSse 参数保留以备将来流场景。
        return new McpHandleResult { StatusCode = 200, Body = json };
    }

    /// <summary>将JSON-RPC响应包装为SSE事件（event: message / data:）</summary>
    private static String ToSse(String json) => $"event: message\ndata: {json}\n\n";
    #endregion
}
