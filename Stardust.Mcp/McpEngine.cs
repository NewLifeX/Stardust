using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using NewLife;
using NewLife.Log;
using Stardust.Data.Platform;

namespace Stardust.Mcp;

/// <summary>MCP协议引擎。工具直出 tools/list（按scope与域白名单裁剪）、令牌鉴权、风险分级、审计双策略。
/// 设计见 Doc/MCP-4-平台MCP重新设计.md（v1.2）。仅依赖 Stardust.Data；宿主配置经 StardustMcpOptions 委托注入。</summary>
public class McpEngine
{
    private readonly StardustMcpOptions _options;
    private readonly Dictionary<String, IMcpTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>支持的MCP协议版本（从新到旧）。initialize时按客户端请求协商</summary>
    internal static readonly String[] SupportedProtocolVersions =
    {
        "2026-07-28",
        "2025-06-18",
        "2025-03-26",
        "2024-11-05",
    };

    /// <summary>实例化引擎，扫描 Stardust.Mcp 程序集中的工具</summary>
    public McpEngine(StardustMcpOptions options) : this(options, typeof(McpEngine).Assembly) { }

    /// <summary>实例化引擎并扫描指定程序集中的工具</summary>
    public McpEngine(StardustMcpOptions options, params Assembly[] toolAssemblies)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // 注入敏感 key 关键词列表到 McpGuard（运行时委托读取，宿主从 StarServerSetting 注入）
        var kws = options.SensitiveKeyKeywords?.Invoke();
        if (kws != null && kws.Length > 0)
            McpGuard.SensitiveKeyKeywords = kws;
        foreach (var asm in toolAssemblies.Length == 0 ? new[] { typeof(McpEngine).Assembly } : toolAssemblies)
            RegisterTools(asm);
        XTrace.WriteLine("MCP engine initialized with {0} tool(s)", _tools.Count);
    }

    private void RegisterTools(Assembly asm)
    {
        var iface = typeof(IMcpTool);
        foreach (var type in asm.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !iface.IsAssignableFrom(type)) continue;
            var tool = (IMcpTool)Activator.CreateInstance(type)!;
            if (tool.Name.IsNullOrEmpty()) continue;
            _tools[tool.Name] = tool;
            XTrace.WriteLine("MCP tool registered: {0} ({1}, {2})", tool.Name, tool.Risk, tool.ScopeDomain);
        }
    }

    /// <summary>已注册工具（测试与自检用）</summary>
    public IReadOnlyCollection<IMcpTool> Tools => _tools.Values.ToArray();

    /// <summary>主入口。解析JSON-RPC请求、校验令牌、路由。通知类消息返回202无响应体</summary>
    public async Task<McpReply> HandleAsync(String body, String ip, String ua, String authorization, IServiceProvider? requestServices = null)
    {
        var sw = Stopwatch.StartNew();
        Object? id = null;
        String methodName = String.Empty;

        try
        {
            if (body.IsNullOrEmpty()) return Error(null, -32700, "解析错误：请求体为空");

            JsonElement request;
            try
            {
                request = JsonSerializer.Deserialize<JsonElement>(body);
            }
            catch (Exception ex)
            {
                return Error(null, -32700, "解析错误：" + ex.Message);
            }

            if (request.ValueKind != JsonValueKind.Object) return Error(null, -32600, "无效请求：不是JSON对象");
            if (!request.TryGetProperty("jsonrpc", out var v) || v.GetString() != "2.0")
                return Error(id, -32600, "无效请求：缺少或非法的 jsonrpc 字段");

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
                return Error(id, -32600, "无效请求：缺少 method");

            // 通知类消息不需要响应
            if (methodName.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase))
                return new McpReply { StatusCode = 202 };

            // initialize 不需要鉴权
            if (methodName == "initialize") return Ok(id, HandleInitialize(request));
            if (methodName == "ping") return Ok(id, new { });

            // 其余方法都需要令牌鉴权
            var ctx = Authorize(authorization, ip, ua, requestServices);
            switch (methodName)
            {
                case "tools/list":
                    return Ok(id, HandleToolsList(ctx));
                case "tools/call":
                    if (!request.TryGetProperty("params", out var paramsEl))
                        return Error(id, -32602, "无效参数：缺少 params");
                    return Ok(id, await HandleToolsCallAsync(paramsEl, ctx, sw));
                default:
                    return Error(id, -32601, $"未知方法：{methodName}");
            }
        }
        catch (McpException ex)
        {
            XTrace.Log.Warn("[McpEngine] method={0} code={1} {2}", methodName, ex.Code, ex.Message);
            return Error(id, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpEngine] method={0} error={1}", methodName, ex);
            return Error(id, -32603, "内部错误：" + ex.Message);
        }
    }

    /// <summary>从Authorization头提取Bearer令牌</summary>
    private static String ExtractToken(String? authorization)
    {
        if (authorization.IsNullOrEmpty()) return String.Empty;
        var prefix = "Bearer ";
        if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return authorization[prefix.Length..].Trim();
        return authorization.Trim();
    }

    /// <summary>令牌鉴权并构造调用上下文（含scope解析）</summary>
    private McpContext Authorize(String? authorization, String ip, String ua, IServiceProvider? requestServices)
    {
        var tokenStr = ExtractToken(authorization);
        if (tokenStr.IsNullOrEmpty()) throw new McpException(-32001, "未经授权：缺少 Bearer 令牌");

        var token = McpToken.FindByToken(tokenStr);
        if (token == null || !McpToken.SafeEquals(token.Token, tokenStr)) throw new McpException(-32001, "未经授权：令牌不存在");
        if (!token.IsValid()) throw new McpException(-32001, "未经授权：令牌已禁用或已过期");

        // 更新调用统计（失败不影响主响应）
        try
        {
            token.RecordCall(ip);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpEngine] 更新令牌调用统计失败（忽略）：{0}", ex.Message);
        }

        var scopes = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        if (!token.Scopes.IsNullOrEmpty())
        {
            foreach (var s in token.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!s.Trim().IsNullOrEmpty()) scopes.Add(s.Trim());
            }
        }

        return new McpContext
        {
            TokenId = token.Id,
            TokenName = token.Name,
            UserId = token.UserId,
            ProjectId = token.ProjectId,
            Scopes = scopes,
            CallerIp = ip,
            UserAgent = ua,
            TraceId = DefaultSpan.Current?.TraceId,
            Services = requestServices,
        };
    }

    /// <summary>处理 initialize。按客户端请求的protocolVersion协商返回</summary>
    private static Object HandleInitialize(JsonElement request)
    {
        var requested = request.TryGetProperty("params", out var p) && p.TryGetProperty("protocolVersion", out var pv)
            ? pv.GetString() : null;
        var negotiated = NegotiateProtocolVersion(requested);
        if (negotiated == null)
            throw new McpException(-32602, $"不支持的协议版本：{requested}。支持：{String.Join(", ", SupportedProtocolVersions)}");

        return new
        {
            protocolVersion = negotiated,
            serverInfo = new { name = "Stardust", version = "2.0.0" },
            instructions = "Stardust distributed platform (nodes, apps, config, deployment, monitoring). Tools are journey-oriented: descriptions are in English; summaries and error messages are in Chinese.",
            capabilities = new { tools = new { } },
        };
    }

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

    /// <summary>处理 tools/list。按域白名单与令牌scope裁剪，客户端看不到无权工具</summary>
    private Object HandleToolsList(McpContext ctx)
    {
        var domains = ParseDomains();
        var tools = _tools.Values
            .Where(t => domains == null || domains.Contains(t.ScopeDomain))
            .Where(t => !t.RequireScope || HasScope(ctx, t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = SchemaOrEmpty(t),
            })
            .ToArray();
        return new { tools };
    }

    /// <summary>处理 tools/call。域白名单→scope→L2确认→预写审计→执行→回写审计</summary>
    private async Task<Object> HandleToolsCallAsync(JsonElement @params, McpContext ctx, Stopwatch sw)
    {
        if (!@params.TryGetProperty("name", out var nameEl))
            throw new McpException(-32602, "无效参数：缺少工具名 name");
        var name = nameEl.GetString();

        if (!_tools.TryGetValue(name, out var tool))
            throw new McpException(-32601, $"未知工具：{name}");

        var domains = ParseDomains();
        if (domains != null && !domains.Contains(tool.ScopeDomain))
            throw new McpException(-32601, $"工具 {name} 已被服务端域白名单禁用");

        if (tool.RequireScope && !HasScope(ctx, tool))
            throw new McpException(-32003, $"禁止访问：缺少授权范围 {tool.ScopeDomain}{RequiredScope(tool)}。请联系管理员在「MCP令牌」管理页为该令牌增加对应 scope");

        var arguments = @params.TryGetProperty("arguments", out var argEl) ? argEl : default;

        // L2 高危操作必须显式确认（设计 §4.2）
        if (tool.Risk == McpRiskLevel.L2 &&
            !(arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("confirm", out var c) && c.ValueKind == JsonValueKind.True))
            throw new McpException(-32003, $"高危操作需要确认：{name} 必须在参数中显式传入 confirm:true。{tool.Description}");

        var success = true;
        String? error = null;
        Object content = null;
        McpAudit? audit = null;
        try
        {
            // 写操作审计 fail-closed（设计 §7.1）：预写审计行，持久化失败即拒绝执行
            if (tool.Risk != McpRiskLevel.L0)
            {
                try
                {
                    audit = McpAuditWriter.Begin(ctx, tool, arguments.ToString());
                }
                catch (Exception ex)
                {
                    // 拒绝本身尽力留痕（best-effort，此时未执行任何操作）
                    try
                    {
                        McpAudit.WriteAsync(ctx.TokenId, ctx.TokenName, name, null, ctx.CallerIp, ctx.UserAgent,
                            arguments.ToString(), false, "审计写入失败，操作已拒绝：" + ex.Message, 0, ctx.TraceId);
                    }
                    catch { }
                    throw new McpException(-32000, "审计写入失败，已拒绝执行该操作：" + ex.Message);
                }
            }

            content = await tool.InvokeAsync(arguments, ctx);
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
            throw new McpException(-32000, "工具执行失败：" + ex.Message);
        }
        finally
        {
            if (audit != null)
            {
                // 写操作：回写预写的审计行（成功时带上工具返回的结果快照 AfterValue）
                var after = success && content != null ? JsonSerializer.Serialize(content) : null;
                McpAuditWriter.Finish(audit, success, error, (Int32)sw.ElapsedMilliseconds, after);
            }
            else
            {
                // 只读：best-effort 异步审计
                try
                {
                    McpAudit.WriteAsync(ctx.TokenId, ctx.TokenName, name, null, ctx.CallerIp, ctx.UserAgent,
                        arguments.ToString(), success, error, (Int32)sw.ElapsedMilliseconds, ctx.TraceId);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[McpEngine] 写入审计日志失败（忽略）：{0}", ex.Message);
                }
            }
        }

        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(content) } } };
    }

    /// <summary>工具所需scope后缀</summary>
    internal static String RequiredScope(IMcpTool tool) => tool.Risk switch
    {
        McpRiskLevel.L0 => ":read",
        McpRiskLevel.L1 => ":write",
        _ => ":admin",
    };

    /// <summary>令牌是否具备工具所需scope</summary>
    internal static Boolean HasScope(McpContext ctx, IMcpTool tool)
        => !tool.RequireScope || ctx.Scopes.Contains(tool.ScopeDomain + RequiredScope(tool));

    private HashSet<String>? ParseDomains()
    {
        var raw = _options.Domains();
        if (raw.IsNullOrEmpty() || raw.Trim() == "*") return null;
        var set = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!s.Trim().IsNullOrEmpty()) set.Add(s.Trim());
        }
        return set.Count == 0 ? null : set;
    }

    private static Object SchemaOrEmpty(IMcpTool tool)
        => tool.InputSchema.ValueKind == JsonValueKind.Undefined || tool.InputSchema.ValueKind == JsonValueKind.Null
            ? (Object)new { type = "object" }
            : tool.InputSchema;

    #region JSON-RPC 构造

    private static String BuildResult(Object? id, Object? result)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });

    /// <summary>构造JSON-RPC错误响应（中间件空体场景也复用）</summary>
    public static String BuildError(Object? id, Int32 code, String message)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } });

    private static McpReply Ok(Object? id, Object? result) => new() { StatusCode = 200, Body = BuildResult(id, result) };

    private static McpReply Error(Object? id, Int32 code, String message) => new() { StatusCode = 200, Body = BuildError(id, code, message) };
    #endregion
}

/// <summary>MCP处理结果承载对象。StatusCode=202表示通知类无响应体</summary>
public sealed class McpReply
{
    /// <summary>HTTP状态码</summary>
    public Int32 StatusCode { get; init; } = 200;

    /// <summary>响应体JSON</summary>
    public String Body { get; init; }
}
