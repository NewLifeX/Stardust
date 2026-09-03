using System.Text.Json;
using NewLife;
using Stardust;

using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Actions.Apps;

/// <summary>向应用下发命令。通过StarFactory.SendAppCommandAsync异步下发，返回执行结果</summary>
public class AppSendCommandAction : McpActionBase
{
    private readonly StarFactory _starFactory;

    /// <summary>构造函数注入StarFactory</summary>
    public AppSendCommandAction(StarFactory starFactory) => _starFactory = starFactory;

    /// <summary>动作名</summary>
    public override String Name => "app_send_command";

    /// <summary>动作描述</summary>
    public override String Description => "向指定应用下发命令（异步执行）。需要Token已授权该应用。支持指定客户端实例（clientId）或广播给应用所有实例。";

    /// <summary>所属模块</summary>
    public override McpModuleType Module => McpModuleType.App;

    /// <summary>所需资源授权。框架层校验app_id在授权范围</summary>
    public override ResourceRequirement? RequiredResource => new()
    {
        Type = McpResourceType.App.ToWireName(),
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
                "client_id": {"type": "string", "description": "可选，目标客户端实例标识（IP@进程）。不传则广播给应用所有实例"},
                "command": {"type": "string", "description": "命令名，如 app/start / app/stop / app/restart / 截屏 等"},
                "args": {"type": "string", "description": "可选，命令参数"},
                "expire": {"type": "integer", "description": "可选，命令过期时间（秒），默认3600"},
                "timeout": {"type": "integer", "description": "可选，等待回复超时（秒），默认5"}
              },
              "required": ["app_id", "command"]
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

        var command = GetString(@params, "command");
        if (command.IsNullOrEmpty()) throw new McpException(-32602, "Invalid params: command is empty");

        var clientId = GetString(@params, "client_id");
        var args = GetString(@params, "args");
        var expire = GetInt32(@params, "expire"); if (expire <= 0) expire = 3600;
        var timeout = GetInt32(@params, "timeout"); if (timeout <= 0) timeout = 5;

        // 根据app_id查找应用（SendAppCommandAsync的appId参数实际是应用Name/Code）
        var app = Stardust.Data.App.FindById(appId);
        if (app == null) throw new McpException(-32601, $"App not found: id={appId}");
        if (app.Name.IsNullOrEmpty()) throw new McpException(-32603, $"App has no Name: id={appId}");
        if (!app.Enable) throw new McpException(-32603, $"App is disabled: id={appId}");

        // 下发命令：SendAppCommandAsync 的第一个参数是应用名（code），不是数字ID
        var reply = await _starFactory.SendAppCommandAsync(app.Name, clientId, command, args, 0, expire, timeout);

        return new
        {
            app_id = appId,
            app_name = app.Name,
            display_name = app.DisplayName,
            client_id = clientId,
            command,
            args,
            expire,
            timeout,
            reply = reply?.Data,
            status = reply?.Status.ToString(),
        };
    }
}
