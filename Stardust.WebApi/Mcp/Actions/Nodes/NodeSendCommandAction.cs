using System.Text.Json;
using NewLife;
using Stardust;
using Stardust.Data.Nodes;

using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Actions.Nodes;

/// <summary>向节点下发命令。通过StarFactory.SendNodeCommandAsync异步下发，返回执行结果</summary>
public class NodeSendCommandAction : McpActionBase
{
    private readonly StarFactory _starFactory;

    /// <summary>构造函数注入StarFactory</summary>
    public NodeSendCommandAction(StarFactory starFactory) => _starFactory = starFactory;

    /// <summary>动作名</summary>
    public override String Name => "node_send_command";

    /// <summary>动作描述</summary>
    public override String Description => "向指定节点下发命令（异步执行）。需要Token已授权该节点（直接Node授权或所属Project授权）。";

    /// <summary>所属模块</summary>
    public override McpModuleType Module => McpModuleType.Node;

    /// <summary>所需资源授权。框架层校验node_id在授权范围</summary>
    public override ResourceRequirement? RequiredResource => new()
    {
        Type = McpResourceType.Node.ToWireName(),
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
                "command": {"type": "string", "description": "命令名，如 ping / node/upgrade / 截屏 等"},
                "args": {"type": "string", "description": "可选，命令参数"},
                "expire": {"type": "integer", "description": "可选，命令过期时间（秒），默认3600"},
                "timeout": {"type": "integer", "description": "可选，等待回复超时（秒），默认5"}
              },
              "required": ["node_id", "command"]
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

        var command = GetString(@params, "command");
        if (command.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: command is empty");

        var args = GetString(@params, "args");
        var expire = GetInt32(@params, "expire"); if (expire <= 0) expire = 3600;
        var timeout = GetInt32(@params, "timeout"); if (timeout <= 0) timeout = 5;

        // 根据node_id查找节点Code
        var node = Node.FindByID(nodeId);
        if (node == null) throw new McpException(-32601, $"Node not found: id={nodeId}");
        if (node.Code.IsNullOrEmpty()) throw new McpException(-32603, $"Node has no Code: id={nodeId}");
        if (!node.Enable) throw new McpException(-32603, $"Node is disabled: id={nodeId}");

        // 下发命令
        var reply = await _starFactory.SendNodeCommandAsync(node.Code, command, args, 0, expire, timeout);

        return new
        {
            node_id = nodeId,
            node_code = node.Code,
            node_name = node.Name,
            command,
            args,
            expire,
            timeout,
            reply = reply?.Data,
            status = reply?.Status.ToString(),
        };
    }
}
