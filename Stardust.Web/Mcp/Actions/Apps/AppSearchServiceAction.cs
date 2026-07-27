using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data;
using XCode;

namespace Stardust.Web.Mcp.Actions.Apps;

/// <summary>搜索应用服务。按服务名/客户端/应用过滤，返回服务提供者列表</summary>
public class AppSearchServiceAction : McpActionBase
{
    /// <summary>动作名</summary>
    public override String Name => "app_search_service";

    /// <summary>动作描述</summary>
    public override String Description => "搜索应用注册的服务提供者（按服务名/客户端/应用ID过滤），返回服务地址、版本、健康状态等。公开查询，无需资源授权。";

    /// <summary>所属模块</summary>
    public override String Module => "app";

    /// <summary>所需资源授权。null表示无资源依赖（公开查询）</summary>
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
                "keyword": {"type": "string", "description": "可选，匹配服务名/客户端"},
                "app_id": {"type": "integer", "description": "可选，按应用过滤"},
                "service_id": {"type": "integer", "description": "可选，按服务ID过滤"},
                "client": {"type": "string", "description": "可选，按客户端标识过滤"},
                "enable": {"type": "boolean", "description": "可选，按启用状态过滤"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
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
        var keyword = GetString(@params, "keyword");
        var appId = GetInt32(@params, "app_id");
        var serviceId = GetInt32(@params, "service_id");
        var client = GetString(@params, "client");
        Boolean? enable = null;
        if (@params.TryGetProperty("enable", out var e) && e.ValueKind == JsonValueKind.False) enable = false;
        else if (e.ValueKind == JsonValueKind.True) enable = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        // 资源授权过滤：若指定 app_id，校验其授权范围
        var authorizedProjectIds = Stardust.Data.Platform.McpTokenResource.GetAuthorizedProjectIds(context.TokenId);
        if (appId > 0 && authorizedProjectIds != null)
        {
            var app = App.FindById(appId);
            if (app == null || !authorizedProjectIds.Contains(app.ProjectId))
                throw new McpException(-32003, $"Forbidden: app_id={appId} is not authorized for this token");
        }

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var list = AppService.Search(appId, serviceId, client, enable, keyword, pageParam);

        var records = list.Select(s => new
        {
            id = s.Id,
            app_id = s.AppId,
            service_id = s.ServiceId,
            service_name = s.ServiceName,
            client = s.Client,
            node_id = s.NodeId,
            enable = s.Enable,
            healthy = s.Healthy,
            version = s.Version,
            address = s.Address,
            weight = s.Weight,
            scope = s.Scope,
            tag = s.Tag,
            update_time = s.UpdateTime,
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
