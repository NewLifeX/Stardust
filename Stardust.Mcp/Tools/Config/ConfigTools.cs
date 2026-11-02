using System.Text.Json;
using NewLife;
using NewLife.Serialization;
using Stardust.Data.Configs;

namespace Stardust.Mcp.Tools.Config;

public class ConfigListTool : McpToolBase
{
    public override String Name => "stardust_config_list";
    public override String Description => "List configuration items of one application. Returns keys, versions and enable state, never values.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "config";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            app_id = new { type = "integer", description = "应用编号" },
        },
        required = new[] { "app_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var app = McpGuard.App(ctx, GetInt32(args, "app_id"));
        var cfg = AppConfig.FindAll(AppConfig._.AppId == app.Id & AppConfig._.ProjectId == ctx.ProjectId).FirstOrDefault();
        if (cfg == null) return Task.FromResult<Object>(McpResult.Ok($"{app.Name} 还没有配置", new { records = Array.Empty<Object>() }));
        var rows = ConfigData.FindAllByApp(cfg.Id).Select(e => new { e.Id, e.Key, e.Version, e.Enable, secret = McpGuard.Secret(e.Key) }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{app.Name} 配置 {rows.Length} 项", new { config_id = cfg.Id, version = cfg.Version, records = rows }));
    }
}

public class ConfigGetTool : McpToolBase
{
    public override String Name => "stardust_config_get";
    public override String Description => "Read one configuration value. Sensitive keys are automatically masked as *** based on StarServerSetting.SensitiveKeywords.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "config";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            config_id = new { type = "integer", description = "配置编号" },
            key = new { type = "string", description = "配置键，可空表示查全部" },
        },
        required = new[] { "config_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var cfg = McpGuard.Config(ctx, GetInt32(args, "config_id"));
        var key = GetString(args, "key");
        var rows = ConfigData.FindAllByApp(cfg.Id).Where(e => key.IsNullOrEmpty() || e.Key.EqualIgnoreCase(key))
            .Select(e => new
            {
                e.Key,
                value = McpGuard.Secret(e.Key) ? "***" : e.Value,
                masked = McpGuard.Secret(e.Key),
                e.Version,
            }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"配置 {cfg.Name} 读取 {rows.Length} 项（敏感值已脱敏）", new { records = rows }));
    }
}

public class ConfigSetTool : McpToolBase
{
    public override String Name => "stardust_config_set";
    public override String Description => "Write configuration draft keys. Does not publish; publishing must be done manually in the web console.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "config";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            config_id = new { type = "integer", description = "配置编号" },
            configs = new { type = "object", additionalProperties = new { type = "string" }, description = "键值对草稿" },
        },
        required = new[] { "config_id", "configs" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var cfg = McpGuard.Config(ctx, GetInt32(args, "config_id"));
        if (!args.TryGetProperty("configs", out var configs) || configs.ValueKind != JsonValueKind.Object)
            throw new McpException(-32602, "无效参数：configs 必须是对象。");
        var map = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in configs.EnumerateObject()) map[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations
            ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        var count = await ops.SetConfigAsync(cfg.Id, map);
        return McpResult.Ok($"已写入 {count} 项草稿，尚未发布。发布需在管理后台手动操作", new { config_id = cfg.Id, written = count },
            null, "草稿不会自动生效；如需撤销，直接覆盖原值或在管理后台放弃草稿");
    }
}

public class ConfigRollbackTool : McpToolBase
{
    public override String Name => "stardust_config_rollback";
    public override String Description => "Rollback configuration to a publish history id. Only publish history can be rolled back.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "config";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            history_id = new { type = "integer", description = "发布历史编号" },
        },
        required = new[] { "history_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var history = ConfigHistory.FindById(GetInt32(args, "history_id"));
        if (history == null) throw new McpException(-32602, "配置历史不存在。");
        var cfg = McpGuard.Config(ctx, history.ConfigId);
        if (!history.Action.EqualIgnoreCase("Publish")) throw new McpException(-32602, "只有发布历史可以回滚。");
        var snap = history.Remark.ToJsonEntity<AppConfig>() ?? throw new McpException(-32602, "历史快照无法解析。");
        var before = cfg.Version;
        cfg.Version = snap.NextVersion > 0 ? snap.NextVersion : snap.Version;
        cfg.NextVersion = snap.NextVersion;
        cfg.Update();
        ConfigHistory.Add(cfg.Id, "Rollback", true, cfg.ToJson());
        var count = ConfigData.Publish(ConfigData.FindAllByApp(cfg.Id), cfg.Version);
        return Task.FromResult<Object>(McpResult.Ok($"配置已从 v{before} 回滚到 v{cfg.Version}，重发布 {count} 项", new { before, version = cfg.Version, republished = count },
            null, "此操作本身即回滚；如需再回滚到更早版本，调用 stardust_config_rollback 选择对应历史记录"));
    }
}
