using System.Text.Json;
using NewLife;
using NewLife.Data;
using Stardust.Data.Deployment;
using XCode;

namespace Stardust.Web.Mcp.Actions.Deploy;

/// <summary>查询部署集列表。按Token授权项目过滤，支持关键字/分类/分页</summary>
public class DeployListAction : McpActionBase
{
    public override String Name => "deploy_list";
    public override String Description => "查询应用部署集列表，按Token授权的项目范围过滤。LLM可通过此动作发现可编译/部署的资源。";
    public override String Module => "deploy";

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "project_id": {"type": "integer", "description": "可选，按项目过滤"},
                "keyword": {"type": "string", "description": "可选，匹配部署集名称/仓库名"},
                "category": {"type": "string", "description": "可选，按分类过滤"},
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
        var projectId = GetInt32(@params, "project_id");
        var keyword = GetString(@params, "keyword");
        var category = GetString(@params, "category");
        Boolean? enable = null;
        if (@params.TryGetProperty("enable", out var e) && e.ValueKind == JsonValueKind.False) enable = false;
        else if (e.ValueKind == JsonValueKind.True) enable = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var authorizedProjectIds = Stardust.Data.Platform.McpTokenResource.GetAuthorizedProjectIds(context.TokenId);
        if (projectId > 0 && authorizedProjectIds != null && !authorizedProjectIds.Contains(projectId))
            throw new McpException(-32003, $"Forbidden: project_id={projectId} is not authorized for this token");

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var start = DateTime.MinValue;
        var end = DateTime.Now;

        IList<AppDeploy> list;
        if (projectId > 0)
        {
            list = AppDeploy.Search(projectId, 0, category, enable, start, end, keyword, pageParam);
        }
        else if (authorizedProjectIds == null)
        {
            list = AppDeploy.Search(-1, 0, category, enable, start, end, keyword, pageParam);
        }
        else
        {
            list = new List<AppDeploy>();
            foreach (var pid in authorizedProjectIds)
            {
                var sub = AppDeploy.Search(pid, 0, category, enable, start, end, keyword, new PageParameter { PageIndex = 1, PageSize = 100 });
                foreach (var n in sub)
                {
                    if (list.Count >= pageSize * page) break;
                    list.Add(n);
                }
            }
            list = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            pageParam.TotalCount = list.Count;
        }

        var records = list.Select(d => new
        {
            id = d.Id,
            project_id = d.ProjectId,
            app_id = d.AppId,
            name = d.Name,
            category = d.Category,
            repository = d.Repository,
            branch = d.Branch,
            enable = d.Enable,
            version = d.Version,
            project_kind = d.ProjectKind,
            package_name = d.PackageName,
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

/// <summary>查询部署版本列表。按 deploy_id 过滤（间接资源授权：deploy_id → AppDeploy.ProjectId）</summary>
public class DeployListVersionsAction : McpActionBase
{
    public override String Name => "deploy_list_versions";
    public override String Description => "查询指定部署集的版本列表（编译产物）。需要Token已授权该部署集所属项目。";
    public override String Module => "deploy";

    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "project",
        Field = "deploy_id",
        Indirect = true,
        IndirectEntity = "AppDeploy",
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "deploy_id": {"type": "integer", "description": "部署集ID"},
                "version": {"type": "string", "description": "可选，按版本号过滤"},
                "enable": {"type": "boolean", "description": "可选，按启用状态过滤"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
              },
              "required": ["deploy_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var deployId = GetInt32(@params, "deploy_id");
        if (deployId <= 0) throw new McpException(-32602, "Invalid params: deploy_id must be a positive integer");

        var version = GetString(@params, "version");
        Boolean? enable = null;
        if (@params.TryGetProperty("enable", out var e) && e.ValueKind == JsonValueKind.False) enable = false;
        else if (e.ValueKind == JsonValueKind.True) enable = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var start = DateTime.MinValue;
        var end = DateTime.Now;

        var list = AppDeployVersion.Search(deployId, version, enable, start, end, null, pageParam);

        var records = list.Select(v => new
        {
            id = v.Id,
            deploy_id = v.DeployId,
            version = v.Version,
            enable = v.Enable,
            url = v.Url,
            size = v.Size,
            hash = v.Hash,
            commit_id = v.CommitId,
            commit_log = v.CommitLog,
            commit_time = v.CommitTime,
            mode = v.Mode.ToString(),
            create_time = v.CreateTime,
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

/// <summary>查询部署历史。按 deploy_id 过滤（间接资源授权）</summary>
public class DeployListHistoryAction : McpActionBase
{
    public override String Name => "deploy_list_history";
    public override String Description => "查询指定部署集的部署历史记录（编译/部署操作日志）。需要Token已授权该部署集所属项目。";
    public override String Module => "deploy";

    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "project",
        Field = "deploy_id",
        Indirect = true,
        IndirectEntity = "AppDeploy",
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "deploy_id": {"type": "integer", "description": "部署集ID"},
                "node_id": {"type": "integer", "description": "可选，按节点过滤"},
                "action": {"type": "string", "description": "可选，按操作类型过滤（如 deploy/compile/Build-Upload、deploy/install）"},
                "success": {"type": "boolean", "description": "可选，按成功/失败过滤"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
              },
              "required": ["deploy_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var deployId = GetInt32(@params, "deploy_id");
        if (deployId <= 0) throw new McpException(-32602, "Invalid params: deploy_id must be a positive integer");

        var nodeId = GetInt32(@params, "node_id");
        var action = GetString(@params, "action");
        Boolean? success = null;
        if (@params.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False) success = false;
        else if (s.ValueKind == JsonValueKind.True) success = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        var start = DateTime.MinValue;
        var end = DateTime.Now;

        var list = AppDeployHistory.Search(deployId, nodeId, action, success, start, end, null, pageParam);

        var records = list.Select(h => new
        {
            id = h.Id,
            deploy_id = h.DeployId,
            node_id = h.NodeId,
            action = h.Action,
            success = h.Success,
            remark = h.Remark,
            trace_id = h.TraceId,
            create_user_id = h.CreateUserId,
            create_time = h.CreateTime,
            create_ip = h.CreateIP,
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

/// <summary>查询部署节点列表。按 deploy_id 过滤（间接资源授权）</summary>
public class DeployListNodesAction : McpActionBase
{
    public override String Name => "deploy_list_nodes";
    public override String Description => "查询指定部署集的部署节点列表（关联哪些节点可部署）。需要Token已授权该部署集所属项目。";
    public override String Module => "deploy";

    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "project",
        Field = "deploy_id",
        Indirect = true,
        IndirectEntity = "AppDeploy",
    };

    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "deploy_id": {"type": "integer", "description": "部署集ID"},
                "node_id": {"type": "integer", "description": "可选，按节点过滤"},
                "enable": {"type": "boolean", "description": "可选，按启用状态过滤"},
                "page": {"type": "integer", "description": "页码，默认1"},
                "page_size": {"type": "integer", "description": "每页条数，默认20，最大100"}
              },
              "required": ["deploy_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var deployId = GetInt32(@params, "deploy_id");
        if (deployId <= 0) throw new McpException(-32602, "Invalid params: deploy_id must be a positive integer");

        var nodeId = GetInt32(@params, "node_id");
        Boolean? enable = null;
        if (@params.TryGetProperty("enable", out var e) && e.ValueKind == JsonValueKind.False) enable = false;
        else if (e.ValueKind == JsonValueKind.True) enable = true;

        var page = GetInt32(@params, "page"); if (page <= 0) page = 1;
        var pageSize = GetInt32(@params, "page_size"); if (pageSize <= 0) pageSize = 20; if (pageSize > 100) pageSize = 100;

        var pageParam = new PageParameter { PageIndex = page, PageSize = pageSize };
        // 注意：Search 的 appId 参数实际过滤的是 DeployId 字段
        var list = AppDeployNode.Search(deployId, nodeId, enable, null, pageParam);

        var records = list.Select(n => new
        {
            id = n.Id,
            deploy_id = n.DeployId,
            deploy_name = n.DeployName,
            node_id = n.NodeId,
            ip = n.IP,
            enable = n.Enable,
            version = n.Version,
            port = n.Port,
            priority = n.Priority.ToString(),
            mode = n.Mode.ToString(),
            delay = n.Delay,
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
