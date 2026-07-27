using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Extensions;
using Stardust.Server;
using Stardust.Web.Services;

namespace Stardust.Web.Controllers;

/// <summary>MCP协议端点。实现JSON-RPC 2.0 over HTTP，对接LLM/智能体</summary>
[ApiController]
[Route("/mcp")]
[AllowAnonymous]
public class McpController : ControllerBase
{
    private readonly McpService _mcpService;
    private readonly StarServerSetting _setting;

    public McpController(McpService mcpService, StarServerSetting setting)
    {
        _mcpService = mcpService;
        _setting = setting;
    }

    /// <summary>MCP协议端点。处理 initialize / tools/list / tools/call</summary>
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        // 检查MCP开关
        if (!_setting.EnableMcp) return NotFound();

        // 读取请求体
        var body = String.Empty;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        if (body.IsNullOrEmpty()) return Content(McpService.BuildError(null, -32700, "Parse error: empty body"), "application/json");

        // 提取调用方信息
        var ip = HttpContext.GetUserHost();
        var ua = Request.Headers.UserAgent.ToString();
        var authorization = Request.Headers.Authorization.ToString();

        // 调用McpService处理
        var response = await _mcpService.HandleAsync(body, ip, ua, authorization);
        return Content(response, "application/json");
    }
}
