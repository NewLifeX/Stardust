using System.Text.Json;
using NewLife;
using Stardust.Data.Deployment;
using Stardust.Web.Services;

namespace Stardust.Web.Mcp.Actions.Deploy;

/// <summary>触发部署集编译。通过DeployService.Compile向编译节点下发deploy/compile命令</summary>
public class DeployCompileAction : McpActionBase
{
    private readonly DeployService _deployService;

    /// <summary>构造函数注入DeployService</summary>
    public DeployCompileAction(DeployService deployService) => _deployService = deployService;

    public override String Name => "deploy_compile";
    public override String Description => "触发指定部署集的编译（拉代码→编译→打包→上传）。需要Token已授权该部署集所属项目，并指定启用的编译节点。";
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
                "build_node_id": {"type": "integer", "description": "编译节点ID。不传则取部署集关联的第一个启用编译节点"},
                "action": {"type": "string", "description": "可选，编译动作（Build-Upload=完整编译并上传，Package-Upload=仅打包上传），默认Build-Upload"}
              },
              "required": ["deploy_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override async Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var deployId = GetInt32(@params, "deploy_id");
        if (deployId <= 0) throw new McpException(-32602, "Invalid params: deploy_id must be a positive integer");

        var buildNodeId = GetInt32(@params, "build_node_id");
        var action = GetString(@params, "action"); if (action.IsNullOrEmpty()) action = "Build-Upload";

        var app = AppDeploy.FindById(deployId);
        if (app == null) throw new McpException(-32601, $"AppDeploy not found: id={deployId}");
        if (!app.Enable) throw new McpException(-32603, $"AppDeploy is disabled: id={deployId}");

        // 查找编译节点
        AppBuildNode buildNode = null;
        if (buildNodeId > 0)
        {
            buildNode = AppBuildNode.FindById(buildNodeId);
            if (buildNode == null) throw new McpException(-32601, $"AppBuildNode not found: id={buildNodeId}");
            if (!buildNode.Enable) throw new McpException(-32603, $"AppBuildNode is disabled: id={buildNodeId}");
            if (buildNode.DeployId != deployId) throw new McpException(-32603, $"AppBuildNode[{buildNodeId}] does not belong to deploy[{deployId}]");
        }
        else
        {
            // 自动选择：该部署集关联的第一个启用编译节点
            var nodes = AppBuildNode.FindAllByDeployId(deployId);
            buildNode = nodes.FirstOrDefault(n => n.Enable);
            if (buildNode == null) throw new McpException(-32603, $"No enabled AppBuildNode found for deploy_id={deployId}");
        }

        // 触发编译
        await _deployService.Compile(app, buildNode, action, context.CallerIp, default);

        return new
        {
            deploy_id = deployId,
            deploy_name = app.Name,
            build_node_id = buildNode.Id,
            build_node_name = buildNode.Node?.Name,
            action,
            status = "submitted",
        };
    }
}

/// <summary>触发部署安装。通过DeployService.Control(action="install")向指定节点下发deploy/install命令</summary>
public class DeployInstallAction : McpActionBase
{
    private readonly DeployService _deployService;

    /// <summary>构造函数注入DeployService</summary>
    public DeployInstallAction(DeployService deployService) => _deployService = deployService;

    public override String Name => "deploy_install";
    public override String Description => "向指定部署节点下发安装命令（部署最新版本到节点）。需要Token已授权该部署集所属项目。";
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
                "node_id": {"type": "integer", "description": "目标部署节点ID（AppDeployNode.Id）"},
                "timeout": {"type": "integer", "description": "可选，等待回复超时（秒），默认0（不等待）"}
              },
              "required": ["deploy_id", "node_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public override async Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var deployId = GetInt32(@params, "deploy_id");
        if (deployId <= 0) throw new McpException(-32602, "Invalid params: deploy_id must be a positive integer");

        var nodeId = GetInt32(@params, "node_id");
        if (nodeId <= 0) throw new McpException(-32602, "Invalid params: node_id must be a positive integer");

        var timeout = GetInt32(@params, "timeout");

        var app = AppDeploy.FindById(deployId);
        if (app == null) throw new McpException(-32601, $"AppDeploy not found: id={deployId}");
        if (!app.Enable) throw new McpException(-32603, $"AppDeploy is disabled: id={deployId}");

        // 注意：node_id 参数是 AppDeployNode.Id（不是 Node.Id）
        var deployNode = AppDeployNode.FindById(nodeId);
        if (deployNode == null) throw new McpException(-32601, $"AppDeployNode not found: id={nodeId}");
        if (!deployNode.Enable) throw new McpException(-32603, $"AppDeployNode is disabled: id={nodeId}");
        if (deployNode.DeployId != deployId) throw new McpException(-32603, $"AppDeployNode[{nodeId}] does not belong to deploy[{deployId}]");

        // 下发安装命令
        await _deployService.Control(app, deployNode, "install", context.CallerIp, 0, timeout, default);

        return new
        {
            deploy_id = deployId,
            deploy_name = app.Name,
            deploy_node_id = deployNode.Id,
            node_id = deployNode.NodeId,
            node_ip = deployNode.IP,
            version = app.Version,
            action = "install",
            timeout,
            status = "submitted",
        };
    }
}
