using System.Text.Json;
using NewLife;
using Stardust;
using Stardust.Data.Nodes;

namespace Stardust.Web.Mcp.Actions.Nodes;

/// <summary>升级指定节点上的StarAgent。通过StarFactory下发node/upgrade命令</summary>
public class NodeUpgradeAction : McpActionBase
{
    private readonly StarFactory _starFactory;

    /// <summary>构造函数注入StarFactory</summary>
    public NodeUpgradeAction(StarFactory starFactory) => _starFactory = starFactory;

    /// <summary>动作名</summary>
    public override String Name => "node_upgrade";

    /// <summary>动作描述</summary>
    public override String Description => "升级指定节点上的StarAgent到最新版本（异步执行）。需要Token已授权该节点。";

    /// <summary>所属模块</summary>
    public override String Module => "node";

    /// <summary>所需资源授权。框架层校验node_id在授权范围</summary>
    public override ResourceRequirement? RequiredResource => new()
    {
        Type = "node",
        Field = "node_id",
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
                "node_id": {"type": "integer", "description": "节点ID"},
                "channel": {"type": "string", "description": "可选，升级通道（如 stable/preview），不传使用节点默认通道"},
                "expire": {"type": "integer", "description": "可选，命令过期时间（秒），默认600"},
                "timeout": {"type": "integer", "description": "可选，等待回复超时（秒），默认0（不等待）"}
              },
              "required": ["node_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    /// <summary>调用动作</summary>
    public override async Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var nodeId = GetInt32(@params, "node_id");
        if (nodeId <= 0) throw new McpException(-32602, "Invalid params: node_id must be a positive integer");

        var channel = GetString(@params, "channel");
        var expire = GetInt32(@params, "expire"); if (expire <= 0) expire = 600;
        var timeout = GetInt32(@params, "timeout"); // 0表示不等待回复

        // 根据node_id查找节点Code
        var node = Node.FindByID(nodeId);
        if (node == null) throw new McpException(-32601, $"Node not found: id={nodeId}");
        if (node.Code.IsNullOrEmpty()) throw new McpException(-32603, $"Node has no Code: id={nodeId}");
        if (!node.Enable) throw new McpException(-32603, $"Node is disabled: id={nodeId}");

        // 下发升级命令：node/upgrade，channel作为argument传入
        var reply = await _starFactory.SendNodeCommandAsync(node.Code, "node/upgrade", channel, 0, expire, timeout);

        return new
        {
            node_id = nodeId,
            node_code = node.Code,
            node_name = node.Name,
            current_version = node.Version,
            channel = channel,
            expire,
            timeout,
            reply = reply?.Data,
            status = reply?.Status.ToString(),
        };
    }
}
