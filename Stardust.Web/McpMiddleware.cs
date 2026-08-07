using System.Text;
using Microsoft.AspNetCore.Http;
using NewLife;
using Stardust.Extensions;
using Stardust.Server;
using Stardust.Web.Services;

namespace Stardust.Web;

/// <summary>MCP 协议中间件。在 Cube Web 中间件之前短路 /mcp 请求，
/// 避免设备ID Cookie 解析等 Web 中间件逻辑影响这个机器API，并提升性能。</summary>
public class McpMiddleware
{
    private readonly RequestDelegate _next;

    public McpMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, McpService mcpService, StarServerSetting setting)
    {
        // 仅处理 /mcp 路径，其余请求照常走后续管道
        if (!context.Request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // MCP 总开关关闭时返回 404
        if (!setting.EnableMcp)
        {
            context.Response.StatusCode = 404;
            return;
        }

        // GET/DELETE 本服务为无状态，按 Streamable HTTP 规范返回 405
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
        {
            context.Response.StatusCode = 405;
            return;
        }

        // 读取请求体
        var body = String.Empty;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        if (body.IsNullOrEmpty())
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(McpService.BuildError(null, -32700, "Parse error: empty body"));
            return;
        }

        // 提取调用方信息
        var ip = context.GetUserHost();
        var ua = context.Request.Headers.UserAgent.ToString();
        var authorization = context.Request.Headers.Authorization.ToString();

        // 客户端是否接受 SSE 流式响应
        var accept = context.Request.Headers.Accept.ToString();
        var acceptSse = accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

        var result = await mcpService.HandleAsync(body, ip, ua, authorization, acceptSse);
        await McpService.WriteResponseAsync(context, result);
    }
}
