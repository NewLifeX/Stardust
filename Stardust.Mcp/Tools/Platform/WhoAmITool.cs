using System.Text.Json;

namespace Stardust.Mcp.Tools.Platform;

/// <summary>自检工具。无scope要求，任何有效令牌可调用；零scope存量旧令牌的唯一可用工具（Q2决策）</summary>
public class WhoAmITool : McpToolBase
{
    public override String Name => "stardust_token_whoami";

    public override String Description =>
        "Inspect the current MCP token: bound user, bound project, granted scopes and available risk levels. " +
        "Call this first after connecting to learn what this token is allowed to do. Takes no arguments.";

    public override McpRiskLevel Risk => McpRiskLevel.L0;

    public override String ScopeDomain => "platform";

    public override Boolean RequireScope => false;

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object" });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var scopes = ctx.Scopes.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var risk = scopes.Any(s => s.EndsWith(":admin", StringComparison.OrdinalIgnoreCase)) ? "L2（高危，需confirm）"
            : scopes.Any(s => s.EndsWith(":write", StringComparison.OrdinalIgnoreCase)) ? "L1（可逆写）"
            : scopes.Length > 0 ? "L0（只读）" : "无（零授权）";

        var summary = $"当前令牌「{ctx.TokenName}」：用户 {(ctx.UserId > 0 ? $"Id {ctx.UserId}" : "未绑定")}，" +
            $"项目 {(ctx.ProjectId > 0 ? $"Id {ctx.ProjectId}" : "未绑定")}，" +
            $"授权范围 {(scopes.Length > 0 ? String.Join(", ", scopes) : "无（仅可自检）")}，风险档位 {risk}";

        var data = new
        {
            token_name = ctx.TokenName,
            user_id = ctx.UserId,
            project_id = ctx.ProjectId,
            scopes,
            risk_level = risk,
            server = new { name = "Stardust", protocol_versions = McpEngine.SupportedProtocolVersions },
        };

        var hints = scopes.Length == 0
            ? new[] { "当前令牌没有任何授权范围：请联系管理员在「MCP令牌」管理页绑定用户与项目并配置 scope，或重建令牌" }
            : Array.Empty<String>();

        return Task.FromResult<Object>(McpResult.Ok(summary, data, hints));
    }
}
