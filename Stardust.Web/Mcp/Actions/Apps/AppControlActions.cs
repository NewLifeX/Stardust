using System.Text.Json;
using NewLife;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Web.Services;

namespace Stardust.Web.Mcp.Actions.Apps;

/// <summary>应用生命周期控制基类。封装App→AppDeploy→AppDeployNode查找 + DeployService.Control调用</summary>
public abstract class AppControlActionBase : McpActionBase
{
    private readonly DeployService _deployService;

    /// <summary>构造函数注入DeployService</summary>
    protected AppControlActionBase(DeployService deployService) => _deployService = deployService;

    /// <summary>控制动作名（restart/stop/start）</summary>
    protected abstract String ControlAction { get; }

    /// <summary>所需资源授权。框架层校验app_id在授权范围</summary>
    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "app",
        Field = "app_id",
    };

    /// <summary>输入参数JSON Schema</summary>
    public override JsonElement InputSchema
    {
        get
        {
            var json = """
            {
              "type": "object",
              "properties": {
                "app_id": {"type": "integer", "description": "应用ID"},
                "node_id": {"type": "integer", "description": "可选，目标节点ID。不传则自动选择第一个启用的部署节点"},
                "timeout": {"type": "integer", "description": "可选，等待回复超时（秒），默认0（不等待）"}
              },
              "required": ["app_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    /// <summary>调用动作</summary>
    public override async Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var appId = GetInt32(@params, "app_id");
        if (appId <= 0) throw new McpException(-32602, "Invalid params: app_id must be a positive integer");

        var nodeId = GetInt32(@params, "node_id");
        var timeout = GetInt32(@params, "timeout");

        // 查找应用
        var app = App.FindById(appId);
        if (app == null) throw new McpException(-32601, $"App not found: id={appId}");
        if (!app.Enable) throw new McpException(-32603, $"App is disabled: id={appId}");

        // 查找应用的部署集（AppDeploy.FindAllByAppId 的参数是真正的 App.Id）
        var deploys = AppDeploy.FindAllByAppId(app.Id);
        if (deploys.Count == 0) throw new McpException(-32603, $"No AppDeploy found for app_id={appId}");

        // 选择目标部署节点
        AppDeployNode? deployNode = null;
        AppDeploy? deploy = null;
        foreach (var d in deploys.Where(e => e.Enable))
        {
            // AppDeployNode.FindAllByDeployId 返回该部署集的所有节点
            var nodes = AppDeployNode.FindAllByDeployId(d.Id);
            if (nodeId > 0)
            {
                deployNode = nodes.FirstOrDefault(n => n.NodeId == nodeId && n.Enable);
            }
            else
            {
                deployNode = nodes.FirstOrDefault(n => n.Enable);
            }

            if (deployNode != null)
            {
                deploy = d;
                break;
            }
        }

        if (deployNode == null)
            throw new McpException(-32603, nodeId > 0
                ? $"No enabled AppDeployNode found: app_id={appId}, node_id={nodeId}"
                : $"No enabled AppDeployNode found for app_id={appId}");

        // 调用 DeployService.Control 执行生命周期控制
        await _deployService.Control(deploy, deployNode, ControlAction, context.CallerIp, 0, timeout, null, default);

        return new
        {
            app_id = appId,
            app_name = app.Name,
            display_name = app.DisplayName,
            deploy_id = deploy.Id,
            deploy_name = deploy.Name,
            node_id = deployNode.NodeId,
            node_ip = deployNode.IP,
            action = ControlAction,
            timeout,
            status = "submitted",
        };
    }
}

/// <summary>重启应用。通过DeployService.Control(action="restart")走部署节点路径</summary>
public class AppRestartAction : AppControlActionBase
{
    /// <summary>构造函数</summary>
    public AppRestartAction(DeployService deployService) : base(deployService) { }

    public override String Name => "app_restart";
    public override String Description => "重启指定应用（通过部署节点下发 deploy/restart 命令）。需要Token已授权该应用。";
    public override String Module => "app";
    protected override String ControlAction => "restart";
}

/// <summary>停止应用。通过DeployService.Control(action="stop")走部署节点路径</summary>
public class AppStopAction : AppControlActionBase
{
    /// <summary>构造函数</summary>
    public AppStopAction(DeployService deployService) : base(deployService) { }

    public override String Name => "app_stop";
    public override String Description => "停止指定应用（通过部署节点下发 deploy/stop 命令）。需要Token已授权该应用。";
    public override String Module => "app";
    protected override String ControlAction => "stop";
}

/// <summary>启动应用。通过DeployService.Control(action="start")走部署节点路径</summary>
public class AppStartAction : AppControlActionBase
{
    /// <summary>构造函数</summary>
    public AppStartAction(DeployService deployService) : base(deployService) { }

    public override String Name => "app_start";
    public override String Description => "启动指定应用（通过部署节点下发 deploy/start 命令）。需要Token已授权该应用。";
    public override String Module => "app";
    protected override String ControlAction => "start";
}
