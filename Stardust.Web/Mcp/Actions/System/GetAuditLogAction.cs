using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;
using XCode;

namespace Stardust.Web.Mcp.Actions.System;

/// <summary>查询审计日志。LLM自省用，按当前Token查最近的调用记录</summary>
public class GetAuditLogAction : McpActionBase
{
    /// <summary>动作名</summary>
    public override String Name => "get_audit_log";

    /// <summary>动作描述</summary>
    public override String Description => "查询当前Token最近的MCP调用记录（审计日志），便于LLM在多轮对话中回顾上下文";

    /// <summary>所属模块</summary>
    public override String Module => "system";

    /// <summary>输入参数JSON Schema</summary>
    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "page": {"type": "integer", "description": "页码，从1开始，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"},
                "success": {"type": "boolean", "description": "可选过滤，true只看成功，false只看失败"},
                "key": {"type": "string", "description": "可选关键字，匹配ActionName/ToolName/ErrorMessage"}
              }
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    /// <summary>调用动作</summary>
    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var page = @params.TryGetProperty("page", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 1;
        var pageSize = @params.TryGetProperty("page_size", out var ps) && ps.ValueKind == JsonValueKind.Number ? ps.GetInt32() : 20;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;
        if (page <= 0) page = 1;

        Boolean? success = null;
        if (@params.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False) success = false;
        else if (s.ValueKind == JsonValueKind.True) success = true;

        var key = @params.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;

        // 默认查询最近 7 天
        var end = DateTime.Now;
        var start = end.AddDays(-7);

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize, Sort = McpAudit._.Id.Desc() };
        var list = McpAudit.Search(context.TokenId, success, start, end, key, pageParam);

        var records = list.Select(a => new
        {
            id = a.Id,
            tool = a.ToolName,
            action = a.ActionName,
            success = a.Success,
            error = a.ErrorMessage,
            duration_ms = a.Duration,
            time = a.CreateTime,
            caller_ip = a.CallerIp,
        }).ToList();

        return Task.FromResult<Object>(new
        {
            total = pageParam.TotalCount,
            page,
            page_size = pageSize,
            records,
        });
    }
}
