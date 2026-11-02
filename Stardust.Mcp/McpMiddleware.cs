using System.Text;
using Microsoft.AspNetCore.Http;
using NewLife;

namespace Stardust.Mcp;

/// <summary>MCP协议中间件。在宿主自身中间件之前短路 /mcp 请求，避免Web中间件逻辑影响机器API。</summary>
public class McpMiddleware
{
    private readonly RequestDelegate _next;

    public McpMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, McpEngine engine, StardustMcpOptions options)
    {
        // 仅处理 /mcp 路径，其余请求照常走后续管道
        if (!context.Request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // MCP 总开关关闭时返回 404
        if (!options.Enabled())
        {
            context.Response.StatusCode = 404;
            return;
        }

        // 本服务为无状态，按 Streamable HTTP 规范对 GET/DELETE 返回 405
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
        {
            context.Response.StatusCode = 405;
            return;
        }

        // 读取请求体
        String body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        if (body.IsNullOrEmpty())
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(McpEngine.BuildError(null, -32700, "解析错误：请求体为空"));
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "";
        var ua = context.Request.Headers.UserAgent.ToString();
        var authorization = context.Request.Headers.Authorization.ToString();

        var reply = await engine.HandleAsync(body, ip, ua, authorization, context.RequestServices);

        context.Response.StatusCode = reply.StatusCode;
        if (!reply.Body.IsNullOrEmpty())
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(reply.Body);
        }
    }
}
