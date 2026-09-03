using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data;
using XCode;
using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Actions.Apps;

/// <summary>查询在线应用。按Token授权项目过滤，支持关键字/分类/分页</summary>
public class AppListOnlineAction : McpActionBase
{
    /// <summary>动作名</summary>
    public override String Name => "app_list_online";

    /// <summary>动作描述</summary>
    public override String Description => "查询当前在线的应用列表，按Token授权的项目范围过滤。LLM可通过此动作发现可操作的应用。";

    /// <summary>所属模块</summary>
    public override McpModuleType Module => McpModuleType.App;

    /// <summary>输入参数JSON Schema</summary>
    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "project_id": {"type": "integer", "description": "可选，按项目过滤"},
                "keyword": {"type": "string", "description": "可选，匹配应用名称/客户端/IP"},
                "category": {"type": "string", "description": "可选，按分类过滤"},
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
        var projectId = GetInt32(@params, "project_id");
        var keyword = GetString(@params, "keyword");
        var category = GetString(@params, "category");
        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var authorizedProjectIds = Stardust.Data.Platform.McpTokenResource.GetAuthorizedProjectIds(context.TokenId);
        if (projectId > 0 && authorizedProjectIds != null && !authorizedProjectIds.Contains(projectId))
            throw new McpException(-32003, $"Forbidden: project_id={projectId} is not authorized for this token");

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var start = DateTime.MinValue;
        var end = DateTime.Now;

        IList<AppOnline> list;
        if (projectId > 0)
        {
            list = AppOnline.Search(projectId, 0, 0, category, start, end, keyword, pageParam);
        }
        else if (authorizedProjectIds == null)
        {
            list = AppOnline.Search(-1, 0, 0, category, start, end, keyword, pageParam);
        }
        else
        {
            // 跨多个授权项目查询，合并结果
            list = new List<AppOnline>();
            foreach (var pid in authorizedProjectIds)
            {
                var sub = AppOnline.Search(pid, 0, 0, category, start, end, keyword, new PageParameter { PageIndex = 1, PageSize = 100 });
                foreach (var n in sub)
                {
                    if (list.Count >= pageSize * page) break;
                    list.Add(n);
                }
            }
            list = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            pageParam.TotalCount = list.Count;
        }

        var records = list.Select(a => new
        {
            id = a.Id,
            app_id = a.AppId,
            project_id = a.ProjectId,
            name = a.Name,
            client = a.Client,
            ip = a.IP,
            category = a.Category,
            node_id = a.NodeId,
            process_id = a.ProcessId,
            web_socket = a.LongLink,
            update_time = a.UpdateTime,
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
