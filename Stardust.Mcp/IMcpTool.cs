using System.Text.Json;

namespace Stardust.Mcp;

/// <summary>操作风险级别。写入工具元数据，由引擎在调用前强制执行（设计 §4.2）</summary>
public enum McpRiskLevel
{
    /// <summary>只读，无副作用。需要 scope 域:read</summary>
    L0,

    /// <summary>可逆写，平台内可回滚。需要 scope 域:write，审计 fail-closed</summary>
    L1,

    /// <summary>高危，不可逆或影响运行时。需要 scope 域:admin，且参数必须显式 confirm:true</summary>
    L2,
}

/// <summary>MCP工具契约。实现类由引擎反射扫描注册，直出 tools/list（废弃旧版二段式 invoke_action）</summary>
public interface IMcpTool
{
    /// <summary>工具名（stardust_域_动作，snake_case）</summary>
    String Name { get; }

    /// <summary>描述（英文，Q4 决策：利于模型选工具）</summary>
    String Description { get; }

    /// <summary>风险级别</summary>
    McpRiskLevel Risk { get; }

    /// <summary>scope域（apps/nodes/config/deploy/monitor/now/platform）</summary>
    String ScopeDomain { get; }

    /// <summary>是否要求scope。false=任何有效令牌可调用（如自检工具）</summary>
    Boolean RequireScope { get; }

    /// <summary>输入参数JSON Schema</summary>
    JsonElement InputSchema { get; }

    /// <summary>执行工具。返回对象将被序列化进 content[0].text</summary>
    /// <param name="args">输入参数</param>
    /// <param name="ctx">调用上下文</param>
    Task<Object> InvokeAsync(JsonElement args, McpContext ctx);
}

/// <summary>MCP工具基类。提供通用实现，子类重写元数据与 InvokeAsync</summary>
public abstract class McpToolBase : IMcpTool
{
    public abstract String Name { get; }

    public abstract String Description { get; }

    public abstract McpRiskLevel Risk { get; }

    public abstract String ScopeDomain { get; }

    /// <summary>默认要求scope；自检类工具重写为 false</summary>
    public virtual Boolean RequireScope => true;

    /// <summary>输入参数JSON Schema。默认空对象</summary>
    public virtual JsonElement InputSchema => default;

    public abstract Task<Object> InvokeAsync(JsonElement args, McpContext ctx);

    public static Int32 ReadInt(JsonElement element, String name) => GetInt32(element, name);

    public static String ReadString(JsonElement element, String name) => GetString(element, name);

    protected static String GetString(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    protected static Int32 GetInt32(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    protected static Int64 GetInt64(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

    protected static Boolean GetBoolean(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    protected static Int32 Page(JsonElement args)
    {
        var page = GetInt32(args, "page");
        return page <= 0 ? 1 : page;
    }

    protected static Int32 PageSize(JsonElement args)
    {
        var size = GetInt32(args, "page_size");
        if (size <= 0) size = 20;
        return size > 100 ? 100 : size;
    }

    protected static DateTime Since(JsonElement args, Int32 defaultMinutes = 1440)
    {
        var raw = GetString(args, "since");
        if (String.IsNullOrEmpty(raw)) return DateTime.Now.AddMinutes(-defaultMinutes);
        if (raw.StartsWith("-") && raw.EndsWith("m", StringComparison.OrdinalIgnoreCase) &&
            Int32.TryParse(raw[1..^1], out var minutes))
            return DateTime.Now.AddMinutes(-minutes);
        return DateTime.TryParse(raw, out var dt) ? dt : DateTime.Now.AddMinutes(-defaultMinutes);
    }

    protected void Need(JsonElement args, String name)
    {
        if (!args.TryGetProperty(name, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new McpException(-32602, $"无效参数：缺少 {name}");
    }
}

/// <summary>MCP调用上下文。一次会话的身份、授权与来源信息</summary>
public sealed class McpContext
{
    /// <summary>令牌编号</summary>
    public Int32 TokenId { get; set; }

    /// <summary>令牌名称（审计快照）</summary>
    public String TokenName { get; set; }

    /// <summary>绑定用户Id。0=未绑定</summary>
    public Int32 UserId { get; set; }

    /// <summary>绑定项目Id。0=未绑定</summary>
    public Int32 ProjectId { get; set; }

    /// <summary>授权范围集合（域:级别，忽略大小写）</summary>
    public HashSet<String> Scopes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>调用方IP</summary>
    public String CallerIp { get; set; }

    /// <summary>调用方User-Agent</summary>
    public String UserAgent { get; set; }

    /// <summary>链路追踪Id</summary>
    public String TraceId { get; set; }

    /// <summary>宿主服务容器。工具可按需解析宿主注册的业务服务</summary>
    public IServiceProvider Services { get; set; }
}
