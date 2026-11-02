using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;
using Stardust.Data.Platform;

namespace Stardust.Mcp;

/// <summary>审计写入器。读操作 best-effort（引擎内联），写操作 fail-closed（预写+回写）</summary>
public static class McpAuditWriter
{
    /// <summary>写操作预写审计（fail-closed）：持久化失败即抛出，引擎据此拒绝执行。
    /// BeforeValue 取调用前快照（脱敏入参），供与 AfterValue 对照审计变更。</summary>
    public static McpAudit Begin(McpContext ctx, IMcpTool tool, String arguments)
    {
        var audit = new McpAudit
        {
            TokenId = ctx.TokenId,
            TokenName = ctx.TokenName,
            ToolName = tool.Name,
            CallerIp = ctx.CallerIp,
            CallerUserAgent = ctx.UserAgent,
            Arguments = SanitizeArguments(arguments),
            Success = false,
            ErrorMessage = "(执行中)",
            Duration = 0,
            TraceId = ctx.TraceId,
            CreateTime = DateTime.Now,
            RiskLevel = (Int32)tool.Risk,
            ProjectId = ctx.ProjectId,
            ScopesUsed = String.Join(',', ctx.Scopes),
            BeforeValue = SanitizeArguments(arguments),
        };
        audit.Insert();
        return audit;
    }

    /// <summary>回写执行结果。失败仅告警：操作已执行且已有审计行，不再中断调用。
    /// afterValue 为工具执行后的结果快照（脱敏），与 BeforeValue 对照；无结果传 null。</summary>
    public static void Finish(McpAudit audit, Boolean success, String error, Int32 duration, String? afterValue = null)
    {
        try
        {
            audit.Success = success;
            audit.ErrorMessage = error?.Cut(500);
            audit.Duration = duration;
            if (!afterValue.IsNullOrEmpty()) audit.AfterValue = SanitizeArguments(afterValue);
            audit.Update();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpAuditWriter] 回写审计结果失败：{0}", ex.Message);
        }
    }

    /// <summary>敏感 key 关键词正则片段。写路径与读路径词表保持一致，兼容 apiKey/api_key、accessKey/access_key、connectionString 等变体</summary>
    private const String SensitiveKeyPattern = "(?:password|secret|token|authorization|connectionstring|conn|apikey|access_?key)";

    /// <summary>
    /// 写路径脱敏：在落盘前把 JSON 字符串值里敏感 key 的值替换为 ***。处理两种形状：
    /// 1. 常规形状 "password":"明文" —— key 名含敏感词即打码其值；
    /// 2. config_set 形状 {"key":"Db_Password","value":"明文"} —— key 的值是敏感名即打码 value 的值。
    /// 与读路径（Stardust.Data/Platform/MCP审计日志.Biz.cs）行为对齐，但本地复制一份以避免跨层依赖。
    /// </summary>
    private static String? SanitizeArguments(String? json)
    {
        if (String.IsNullOrEmpty(json)) return json;

        // 与读路径行为一致：先截断 2000 字符
        if (json.Length > 2000) json = json.Substring(0, 2000) + "...";

        // 大小写不敏感；key 包含任一敏感子串即命中；只处理字符串值（"key":"value"），不动数字/对象值
        json = Regex.Replace(json,
            "(?i)(\"[^\"]*" + SensitiveKeyPattern + "[^\"]*\"\\s*:\\s*\")[^\"]*(\")",
            "$1***$2");

        // config_set 等工具的参数形状：{"key":"敏感名","value":"明文"} —— key 的值是敏感名时打码 value 的值
        json = Regex.Replace(json,
            "(?i)(\"key\"\\s*:\\s*\"[^\"]*" + SensitiveKeyPattern + "[^\"]*\"\\s*,\\s*\"value\"\\s*:\\s*\")[^\"]*(\")",
            "$1***$2");

        return json;
    }
}
