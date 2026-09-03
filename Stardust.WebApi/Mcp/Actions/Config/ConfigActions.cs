using System.Text.Json;
using NewLife;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Server.Services;

using Stardust.Data.Platform;

namespace Stardust.Web.Mcp.Actions.Config;

/// <summary>获取应用配置。通过ConfigService.GetConfigs合并三层配置（本应用+共享+全局），返回键值字典</summary>
public class ConfigGetAction : McpActionBase
{
    private readonly ConfigService _configService;

    /// <summary>构造函数注入ConfigService</summary>
    public ConfigGetAction(ConfigService configService) => _configService = configService;

    /// <summary>动作名</summary>
    public override String Name => "config_get";

    /// <summary>动作描述</summary>
    public override String Description => "获取指定应用的配置字典（合并本应用+共享应用+全局应用三层配置，解析内嵌引用）。需要Token已授权该应用。";

    /// <summary>所属模块</summary>
    public override McpModuleType Module => McpModuleType.Config;

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
                "scope": {"type": "string", "description": "可选，作用域（如 production/staging），不传返回所有作用域"}
              },
              "required": ["app_id"]
            }
            """;
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    /// <summary>调用动作</summary>
    public override Task<Object> InvokeAsync(JsonElement @params, McpContext context)
    {
        var appId = GetInt32(@params, "app_id");
        if (appId <= 0) throw new McpException(-32602, "Invalid params: app_id must be a positive integer");

        var scope = GetString(@params, "scope");

        // 查找应用配置（AppConfig.FindByAppId 按 App.Id 查找）
        var appConfig = AppConfig.FindByAppId(appId);
        if (appConfig == null) throw new McpException(-32601, $"AppConfig not found for app_id={appId}");
        if (!appConfig.Enable) throw new McpException(-32603, $"AppConfig is disabled: app_id={appId}");

        // 获取合并后的配置字典
        var configs = _configService.GetConfigs(appConfig, scope);

        // 转换为记录列表（便于序列化和分页）
        var records = configs
            .Where(kv => !kv.Key.IsNullOrEmpty() && !kv.Key.StartsWith("_"))
            .Select(kv => new { key = kv.Key, value = kv.Value })
            .OrderBy(r => r.key)
            .ToList();

        return Task.FromResult<Object>(new
        {
            app_id = appId,
            app_name = appConfig.Name,
            scope,
            version = appConfig.Version,
            total = records.Count,
            records,
        });
    }
}

/// <summary>设置应用配置。批量写入配置键值对（不会自动发布，需另行调用config_publish或等待定时发布）</summary>
public class ConfigSetAction : McpActionBase
{
    private readonly ConfigService _configService;

    /// <summary>构造函数注入ConfigService</summary>
    public ConfigSetAction(ConfigService configService) => _configService = configService;

    /// <summary>动作名</summary>
    public override String Name => "config_set";

    /// <summary>动作描述</summary>
    public override String Description => "批量设置应用配置键值对（支持value+可选comment）。注意：不会自动发布，需另行触发发布。需要Token已授权该应用。";

    /// <summary>所属模块</summary>
    public override McpModuleType Module => McpModuleType.Config;

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
                "configs": {
                  "type": "object",
                  "description": "配置字典。key为配置键，value可以是字符串（直接作为Value）或对象（含value和comment字段）",
                  "additionalProperties": true
                },
                "publish": {"type": "boolean", "description": "可选，是否立即发布，默认false"}
              },
              "required": ["app_id", "configs"]
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

        if (!@params.TryGetProperty("configs", out var configsEl) || configsEl.ValueKind != JsonValueKind.Object)
            throw new McpException(-32602, "Invalid params: configs must be an object");

        var publish = @params.TryGetProperty("publish", out var pubEl) && pubEl.ValueKind == JsonValueKind.True;

        // 查找应用配置
        var appConfig = AppConfig.FindByAppId(appId);
        if (appConfig == null) throw new McpException(-32601, $"AppConfig not found for app_id={appId}");
        if (!appConfig.Enable) throw new McpException(-32603, $"AppConfig is disabled: app_id={appId}");
        if (appConfig.Readonly) throw new McpException(-32603, $"AppConfig is readonly: app_id={appId}");

        // 构造配置字典（ConfigService.SetConfigs 接受 IDictionary<String, Object>）
        // 直接传 JsonElement（SetConfigs 内部已处理 JsonElement 对象形式）
        var configs = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in configsEl.EnumerateObject())
        {
            if (prop.Name.IsNullOrEmpty()) continue;
            // 字符串值直接用，对象值原样传 JsonElement（SetConfigs 内部会解析 Value/Comment）
            if (prop.Value.ValueKind == JsonValueKind.String)
                configs[prop.Name] = prop.Value.GetString() ?? String.Empty;
            else
                configs[prop.Name] = prop.Value.Clone();
        }

        if (configs.Count == 0)
            throw new McpException(-32602, "Invalid params: configs is empty");

        // 写入配置
        var count = _configService.SetConfigs(appConfig, configs);

        // 可选：立即发布
        Int32? publishResult = null;
        if (publish)
        {
            publishResult = await _configService.Publish(appConfig.AppId);
        }

        return new
        {
            app_id = appId,
            app_name = appConfig.Name,
            written_count = count,
            published = publish,
            publish_version = publishResult,
        };
    }
}
