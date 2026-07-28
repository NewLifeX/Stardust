using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data.Gateway;
using XCode;

namespace Stardust.Web.Mcp.Actions.Gateway;

/// <summary>查询网关路由列表。支持关键字模糊匹配（名称/域名/路径/备注）</summary>
public class GatewayListRoutesAction : McpActionBase
{
    public override String Name => "gateway_list_routes";
    public override String Description => "查询网关路由列表，支持关键字模糊匹配（名称/域名/路径/备注）。公开查询，无需资源授权。";
    public override String Module => "gateway";

    public override ResourceRequirement? RequiredResource => null;

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "keyword": {"type": "string", "description": "可选，匹配名称/域名/路径/备注"},
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

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var keyword = GetString(@params, "keyword");
        Boolean? enable = null;
        if (@params.TryGetProperty("enable", out var e) && e.ValueKind == JsonValueKind.False) enable = false;
        else if (e.ValueKind == JsonValueKind.True) enable = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var start = DateTime.MinValue;
        var end = DateTime.Now;

        // 使用自动生成的 Search 重载（支持 enable 过滤）
        var list = GatewayRoute.Search(-1, 0, 0, null, null, null, null, null, null, enable, start, end, keyword, pageParam);

        var records = list.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            enable = r.Enable,
            priority = r.Priority,
            domain = r.Domain,
            path = r.Path,
            methods = r.Methods,
            cluster_id = r.ClusterId,
            strip_prefix = r.StripPrefix,
            web_socket = r.WebSocket,
            update_time = r.UpdateTime,
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

/// <summary>查询网关集群列表。支持关键字模糊匹配（名称/备注）</summary>
public class GatewayListClustersAction : McpActionBase
{
    public override String Name => "gateway_list_clusters";
    public override String Description => "查询网关集群列表，支持关键字模糊匹配（名称/备注）。公开查询，无需资源授权。";
    public override String Module => "gateway";

    public override ResourceRequirement? RequiredResource => null;

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "keyword": {"type": "string", "description": "可选，匹配名称/备注"},
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

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var keyword = GetString(@params, "keyword");
        Boolean? enable = null;
        if (@params.TryGetProperty("enable", out var e) && e.ValueKind == JsonValueKind.False) enable = false;
        else if (e.ValueKind == JsonValueKind.True) enable = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var start = DateTime.MinValue;
        var end = DateTime.Now;

        var list = GatewayCluster.Search(-1, null, enable, start, end, keyword, pageParam);

        var records = list.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            enable = c.Enable,
            load_balance = c.LoadBalance,
            health_path = c.HealthPath,
            health_interval = c.HealthInterval,
            session_sticky = c.SessionSticky,
            session_sticky_name = c.SessionStickyName,
            node_count = c.Nodes?.Count ?? 0,
            active_node_count = c.ActiveNodes,
            update_time = c.UpdateTime,
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
