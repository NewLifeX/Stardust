using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data.Nodes;
using XCode;

namespace Stardust.Web.Mcp.Actions.Nodes;

/// <summary>搜索节点。支持按名称/编码/IP/机器名搜索，按Token授权项目过滤</summary>
public class NodeSearchAction : McpActionBase
{
    /// <summary>动作名</summary>
    public override String Name => "node_search";

    /// <summary>动作描述</summary>
    public override String Description => "按关键字搜索节点（匹配名称/编码/IP/机器名），返回Token授权项目范围内的匹配节点。";

    /// <summary>所属模块</summary>
    public override String Module => "node";

    /// <summary>输入参数JSON Schema</summary>
    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "keyword": {"type": "string", "description": "搜索关键字（匹配名称/编码/IP/机器名）"},
                "project_id": {"type": "integer", "description": "可选，按项目过滤"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
              },
              "required": ["keyword"]
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
        if (keyword.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: keyword is empty");

        var projectId = GetInt32(@params, "project_id");
        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var authorizedProjectIds = Stardust.Data.Platform.McpTokenResource.GetAuthorizedProjectIds(context.TokenId);
        if (projectId > 0 && authorizedProjectIds != null && !authorizedProjectIds.Contains(projectId))
            throw new McpException(-32003, $"Forbidden: project_id={projectId} is not authorized for this token");

        // 构造查询表达式：关键字匹配 Code/Name/IP/MachineName
        var exp = new WhereExpression();
        exp &= Node._.Code.Contains(keyword) |
               Node._.Name.Contains(keyword) |
               Node._.IP.Contains(keyword) |
               Node._.MachineName.Contains(keyword);

        // 项目过滤
        if (projectId > 0)
            exp &= Node._.ProjectId == projectId;
        else if (authorizedProjectIds != null)
            exp &= Node._.ProjectId.In(authorizedProjectIds);

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var list = Node.FindAll(exp, pageParam);

        var records = list.Select(n => new
        {
            id = n.ID,
            project_id = n.ProjectId,
            name = n.Name,
            code = n.Code,
            ip = n.IP,
            category = n.Category,
            enable = n.Enable,
            version = n.Version,
            machine_name = n.MachineName,
            last_active = n.LastActive,
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
