using System.Text.Json;
using NewLife;
using Stardust.Data;
using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Actions.Apps;

/// <summary>解析服务地址。查询指定服务的所有健康提供者，返回地址/版本/权重/标签</summary>
public class AppResolveServiceAction : McpActionBase
{
    /// <summary>动作名</summary>
    public override String Name => "app_resolve_service";

    /// <summary>动作描述</summary>
    public override String Description => "按服务名解析服务地址，返回所有健康的服务提供者（含地址/版本/权重/标签）。公开服务发现，无需资源授权。";

    /// <summary>所属模块</summary>
    public override McpModuleType Module => McpModuleType.App;

    /// <summary>所需资源授权。null表示无资源依赖（公开服务发现）</summary>
    public override ResourceRequirement? RequiredResource => null;

    /// <summary>输入参数JSON Schema</summary>
    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "service_name": {"type": "string", "description": "服务名"},
                "min_version": {"type": "string", "description": "可选，最低版本要求"},
                "tag": {"type": "string", "description": "可选，标签过滤（逗号分隔）"},
                "scope": {"type": "string", "description": "可选，作用域过滤"}
              },
              "required": ["service_name"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    /// <summary>调用动作</summary>
    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var serviceName = GetString(@params, "service_name");
        if (serviceName.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: service_name is empty");

        var minVersion = GetString(@params, "min_version");
        var tag = GetString(@params, "tag");
        var scope = GetString(@params, "scope");

        // 查找服务实体
        var service = Service.FindByName(serviceName);
        if (service == null) throw new McpException(-32601, $"Service not found: {serviceName}");

        // 该服务的所有提供者
        var all = AppService.FindAllByService(service.Id);
        var tags = tag?.Split(",", StringSplitOptions.RemoveEmptyEntries);

        // 过滤：启用 + 健康 + 匹配规则（与 RegistryService.ResolveService 逻辑一致）
        var providers = all
            .Where(s => s.Enable && s.Healthy && s.Match(minVersion, scope, tags))
            .Select(s => s.ToModel())
            .ToList();

        var records = providers.Select(p => new
        {
            service_name = p.ServiceName,
            display_name = p.DisplayName,
            client = p.Client,
            version = p.Version,
            address = p.Address,
            external_address = p.Address2,
            scope = p.Scope,
            tag = p.Tag,
            weight = p.Weight,
            update_time = p.UpdateTime,
        }).ToList();

        return Task.FromResult<Object>(new
        {
            service_name = serviceName,
            scope,
            min_version = minVersion,
            tag,
            total = records.Count,
            records,
        });
    }
}
