using System.Text.Json;
using NewLife;
using Stardust.Data.Platform;

namespace Stardust.Web.Mcp;

/// <summary>MCP动作接口。实现该接口的类会被McpService反射扫描自动注册，暴露为invoke_action的可用动作</summary>
public interface IMcpAction
{
    /// <summary>动作名（snake_case，如 node_send_command）</summary>
    String Name { get; }

    /// <summary>动作描述</summary>
    String Description { get; }

    /// <summary>所属模块（node/app/config/deploy/gateway/monitor/system）</summary>
    McpModuleType Module { get; }

    /// <summary>输入参数JSON Schema</summary>
    JsonElement InputSchema { get; }

    /// <summary>所需资源授权声明。null 表示无资源依赖（如纯查询）</summary>
    ResourceRequirement? RequiredResource { get; }

    /// <summary>调用动作</summary>
    /// <param name="params">输入参数</param>
    /// <param name="context">调用上下文</param>
    /// <returns>结果对象，将被序列化为JSON返回给客户端</returns>
    Task<Object> InvokeAsync(JsonElement @params, McpContext context);
}

/// <summary>资源授权声明</summary>
public sealed class ResourceRequirement
{
    /// <summary>资源类型（project/node/app）</summary>
    public String Type { get; init; }

    /// <summary>参数中携带资源ID的字段名（如 node_id / app_id / deploy_id）</summary>
    public String Field { get; init; }

    /// <summary>是否间接资源。true时需要通过IndirectEntity反查实际ProjectId/AppId</summary>
    public Boolean Indirect { get; init; }

    /// <summary>间接反查的实体类型名（如 AppDeploy/AppPipeline/AppPipelineRun），仅Indirect=true时有效</summary>
    public String IndirectEntity { get; init; }

    /// <summary>是否可选。true时若参数未携带该字段则跳过资源校验</summary>
    public Boolean Optional { get; init; }
}

/// <summary>MCP动作基类。提供通用实现，子类只需重写Name/Description/Module/InvokeAsync</summary>
public abstract class McpActionBase : IMcpAction
{
    /// <summary>动作名（snake_case）</summary>
    public abstract String Name { get; }

    /// <summary>动作描述</summary>
    public abstract String Description { get; }

    /// <summary>所属模块</summary>
    public abstract McpModuleType Module { get; }

    /// <summary>输入参数JSON Schema。默认空对象，子类可重写</summary>
    public virtual JsonElement InputSchema => default;

    /// <summary>所需资源授权声明。默认null，子类可重写</summary>
    public virtual ResourceRequirement? RequiredResource => null;

    /// <summary>调用动作</summary>
    public abstract Task<Object> InvokeAsync(JsonElement @params, McpContext context);

    /// <summary>从JsonElement提取参数值</summary>
    protected static String GetString(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    protected static Int32 GetInt32(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    protected static Int64 GetInt64(JsonElement element, String name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

    protected static Boolean HasProperty(JsonElement element, String name)
        => element.TryGetProperty(name, out _) && element.GetProperty(name).ValueKind != JsonValueKind.Null;
}
