using System.Text.Json;
using NewLife;
using Stardust.Data;
using Stardust.Data.Nodes;

namespace Stardust.Mcp.Tools.Nodes;

public class NodeListTool : McpToolBase
{
    public override String Name => "stardust_node_list";
    public override String Description => "List nodes in the token project. Optional keyword and online filter.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "nodes";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            keyword = new { type = "string", description = "名称/IP/编码关键字" },
            online_only = new { type = "boolean", description = "只看在线" },
            page_size = new { type = "integer", description = "每页条数" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        McpGuard.NeedProject(ctx);
        var key = GetString(args, "keyword");
        var exp = Node._.ProjectId == ctx.ProjectId;
        if (!key.IsNullOrEmpty()) exp &= Node._.Name.Contains(key) | Node._.IP.Contains(key) | Node._.Code.Contains(key);
        var onlineIds = NodeOnline.FindAll(NodeOnline._.ProjectId == ctx.ProjectId).Select(e => e.NodeID).ToHashSet();
        var all = Node.FindAll(exp).Where(e => !GetBoolean(args, "online_only") || onlineIds.Contains(e.ID)).ToArray();
        var total = all.Length;
        var rows = all.Skip((Page(args) - 1) * PageSize(args)).Take(PageSize(args))
            .Select(e => new { id = e.ID, e.Name, e.IP, e.Version, e.Category, online = onlineIds.Contains(e.ID) }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"节点 {rows.Length}/{total} 台", new { records = rows },
            null, null, McpResult.Paged(total, Page(args), PageSize(args))));
    }
}

public class NodeDetailTool : McpToolBase
{
    public override String Name => "stardust_node_detail";
    public override String Description => "Read one node: online state, version, resource watermark and recent commands.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "nodes";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            node_id = new { type = "integer", description = "节点编号" },
        },
        required = new[] { "node_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var node = McpGuard.Node(ctx, GetInt32(args, "node_id"));
        var online = NodeOnline.FindAll(NodeOnline._.NodeID == node.ID).FirstOrDefault();
        var commands = NodeCommand.FindAll(NodeCommand._.NodeID == node.ID).OrderByDescending(e => e.Id).Take(10)
            .Select(e => new { e.Command, e.Result, e.StartTime }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{node.Name} {(online == null ? "离线" : "在线")}", new
        {
            id = node.ID,
            node.Name,
            node.IP,
            node.Version,
            cpu = online?.CpuRate,
            memory = online?.MemoryUsed,
            commands,
        }));
    }
}

public class NodeAppsTool : McpToolBase
{
    public override String Name => "stardust_node_apps";
    public override String Description => "List application instances running on one node.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "nodes";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            node_id = new { type = "integer", description = "节点编号" },
        },
        required = new[] { "node_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var node = McpGuard.Node(ctx, GetInt32(args, "node_id"));
        var rows = AppOnline.FindAll(AppOnline._.NodeId == node.ID & AppOnline._.ProjectId == ctx.ProjectId)
            .Select(e => new { e.AppId, e.Name, e.Version, e.ProcessId, e.IP }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{node.Name} 上 {rows.Length} 个应用实例", new { records = rows }));
    }
}

public class NodeCommandTool : McpToolBase
{
    public override String Name => "stardust_node_command";
    public override String Description => "Execute a command on one node. Dangerous commands and any non-readonly command require confirm=true. The operator is the MCP token.";
    public override McpRiskLevel Risk => McpRiskLevel.L2;
    public override String ScopeDomain => "nodes";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            node_id = new { type = "integer", description = "节点编号" },
            command = new { type = "string", description = "要执行的命令" },
            args = new { type = "string", description = "命令附加参数" },
            confirm = new { type = "boolean", description = "危险命令或非只读命令必须传 true" },
        },
        required = new[] { "node_id", "command" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var node = McpGuard.Node(ctx, GetInt32(args, "node_id"));
        var command = GetString(args, "command");
        if (command.IsNullOrEmpty()) throw new McpException(-32602, "无效参数：缺少 command。");
        if (Danger(command) && !GetBoolean(args, "confirm"))
            throw new McpException(-32003, "该命令命中危险规则，必须传 confirm=true。");
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        var reply = await ops.SendNodeCommandAsync(node.Code, command, GetString(args, "args"));
        return McpResult.Ok($"已向节点 {node.Name} 下发命令 {command}", new { node_id = node.ID, command, reply },
            null, "命令已在节点上执行，无法自动回滚；如需恢复请手动执行反向命令");
    }

    private static Boolean Danger(String command)
    {
        var c = command.ToLowerInvariant();
        return c.Contains("rm ") || c.Contains("mkfs") || c.Contains("dd ") || c.Contains("shutdown") || c.Contains("reboot");
    }
}

public class AppCommandTool : McpToolBase
{
    public override String Name => "stardust_app_command";
    public override String Description => "Send a command to one application instance. Requires confirm=true.";
    public override McpRiskLevel Risk => McpRiskLevel.L2;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
            command = new { type = "string", description = "要执行的命令" },
            client_id = new { type = "string", description = "客户端实例Id，可空" },
            args = new { type = "string", description = "命令附加参数" },
            confirm = new { type = "boolean", description = "高危操作必须传 true" },
        },
        required = new[] { "app_id", "command" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var app = McpGuard.App(ctx, GetInt32(args, "app_id"));
        var command = GetString(args, "command");
        if (command.IsNullOrEmpty()) throw new McpException(-32602, "无效参数：缺少 command。");
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        var reply = await ops.SendAppCommandAsync(app.Name, GetString(args, "client_id"), command, GetString(args, "args"));
        return McpResult.Ok($"已向应用 {app.Name} 下发命令 {command}", new { app_id = app.Id, command, reply },
            null, "命令已在应用实例上执行，无法自动回滚；如需恢复请手动执行反向命令");
    }
}

public class AppProcessTool : McpToolBase
{
    public override String Name => "stardust_app_process";
    public override String Description => "Start, restart or stop an application process. stop requires confirm=true.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "apps";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
            action = new { type = "string", description = "操作类型", @enum = new[] { "start", "restart", "stop" } },
            args = new { type = "string", description = "命令附加参数" },
            confirm = new { type = "boolean", description = "stop 必须传 true" },
        },
        required = new[] { "app_id", "action" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var action = GetString(args, "action");
        if (!new[] { "start", "restart", "stop" }.Contains(action)) throw new McpException(-32602, "action 只能是 start/restart/stop。");
        if (action == "stop" && !GetBoolean(args, "confirm")) throw new McpException(-32003, "stop 必须传 confirm=true。");
        args = JsonSerializer.SerializeToElement(new { app_id = GetInt32(args, "app_id"), command = "app/" + action, args = GetString(args, "args"), confirm = true });
        return new AppCommandTool().InvokeAsync(args, ctx);
    }
}
