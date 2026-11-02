namespace Stardust.Mcp;

/// <summary>工具返回统一信封（设计 §6.2）：summary 给人与模型结论句，data 结构化结果，next_hints 引导多步排障</summary>
public static class McpResult
{
    public static Object Ok(String summary, Object data, String[]? nextHints = null, String? rollbackHint = null, Object? pagination = null)
        => new { summary, data, next_hints = nextHints ?? Array.Empty<String>(), rollback_hint = rollbackHint, pagination };

    /// <summary>构造分页信息，可作为 pagination 参数传给 <see cref="Ok"/>。</summary>
    /// <param name="total">总记录数</param>
    /// <param name="page">当前页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    public static Object Paged(Int32 total, Int32 page, Int32 pageSize) => new { total, page, page_size = pageSize };
}
