using NewLife;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;

namespace Stardust.Mcp;

/// <summary>项目边界与敏感值处理。令牌严格单项目，所有读路径都从这里取对象。</summary>
public static class McpGuard
{
    public static void NeedProject(McpContext ctx)
    {
        if (ctx.ProjectId <= 0)
            throw new McpException(-32003, "当前令牌未绑定项目。请在 MCP 令牌管理页绑定项目后再调用业务工具。");

        // 令牌绑定了用户时，校验该用户是否为项目成员（向后兼容：UserId==0 的老令牌跳过）
        if (ctx.UserId > 0)
        {
            var isMember = ProjectUser.FindAll(ProjectUser._.UserId == ctx.UserId & ProjectUser._.ProjectId == ctx.ProjectId & ProjectUser._.Enable == true).Any();
            if (!isMember)
                throw new McpException(-32003, $"用户 {ctx.UserId} 不是项目 {ctx.ProjectId} 的成员，无权访问。");
        }
    }

    /// <summary>校验令牌是否被授权访问指定资源（App/Node）。若令牌未配置任何资源授权，则放行（向后兼容老令牌）。</summary>
    private static void CheckResource(McpContext ctx, String resourceType, Int32 resourceId)
    {
        var records = McpTokenResource.FindAllByTokenId(ctx.TokenId);
        if (records == null || records.Count == 0) return;

        var matched = records.Any(r => r.Enable &&
            (r.IsAll
                ? r.ResourceType.EqualIgnoreCase(resourceType)
                : r.ResourceType.EqualIgnoreCase(resourceType) && r.ResourceId == resourceId));
        if (!matched)
            throw new McpException(-32003, $"令牌未被授权访问该{resourceType}资源 {resourceId}。请在 MCP 令牌管理页为该令牌添加资源授权。");
    }

    public static App FindApp(McpContext ctx, Int32 appId, String appName)
    {
        if (appId > 0) return App(ctx, appId);
        NeedProject(ctx);
        if (appName.IsNullOrEmpty()) throw new McpException(-32602, "请传 app_id 或 app_name。");
        var list = Stardust.Data.App.FindAll(Stardust.Data.App._.ProjectId == ctx.ProjectId & Stardust.Data.App._.Name == appName);
        var app = list.FirstOrDefault() ?? Stardust.Data.App.FindAll(Stardust.Data.App._.ProjectId == ctx.ProjectId & Stardust.Data.App._.DisplayName == appName).FirstOrDefault();
        if (app == null) throw new McpException(-32003, $"当前项目里没有名为 {appName} 的应用。");
        return app;
    }

    public static App App(McpContext ctx, Int32 appId)
    {
        NeedProject(ctx);
        var app = Stardust.Data.App.FindById(appId);
        if (app == null || app.ProjectId != ctx.ProjectId)
            throw new McpException(-32003, $"应用 {appId} 不存在，或不属于当前令牌绑定的项目。");
        CheckResource(ctx, "App", app.Id);
        return app;
    }

    public static Node Node(McpContext ctx, Int32 nodeId)
    {
        NeedProject(ctx);
        var node = Stardust.Data.Nodes.Node.FindByID(nodeId);
        if (node == null || node.ProjectId != ctx.ProjectId)
            throw new McpException(-32003, $"节点 {nodeId} 不存在，或不属于当前令牌绑定的项目。");
        CheckResource(ctx, "Node", node.ID);
        return node;
    }

    public static AppDeploy Deploy(McpContext ctx, Int32 deployId)
    {
        NeedProject(ctx);
        var deploy = AppDeploy.FindById(deployId);
        if (deploy == null || deploy.ProjectId != ctx.ProjectId)
            throw new McpException(-32003, $"部署集 {deployId} 不存在，或不属于当前令牌绑定的项目。");
        CheckResource(ctx, "App", deploy.AppId);
        return deploy;
    }

    public static AppDeploy DeployByApp(McpContext ctx, Int32 appId)
    {
        var app = App(ctx, appId);
        var list = AppDeploy.FindAll(AppDeploy._.AppId == app.Id & AppDeploy._.ProjectId == ctx.ProjectId);
        var deploy = list.FirstOrDefault(e => e.Enable) ?? list.FirstOrDefault();
        if (deploy == null) throw new McpException(-32602, $"应用 {app.Name} 还没有部署集。");
        return deploy;
    }

    public static AppConfig Config(McpContext ctx, Int32 configId)
    {
        NeedProject(ctx);
        var cfg = AppConfig.FindById(configId);
        if (cfg == null || cfg.ProjectId != ctx.ProjectId)
            throw new McpException(-32003, $"配置 {configId} 不存在，或不属于当前令牌绑定的项目。");
        CheckResource(ctx, "App", cfg.AppId);
        return cfg;
    }

    /// <summary>敏感 key 关键词列表。由 McpEngine 初始化时从 StardustMcpOptions 注入；默认用全局默认列表。</summary>
    public static String[] SensitiveKeyKeywords { get; set; } = StardustMcpOptions.DefaultSensitiveKeyKeywords;

    /// <summary>判断配置 key 是否敏感：key 名包含任一关键词（大小写不敏感）即视为敏感。</summary>
    public static Boolean Secret(String key)
    {
        if (key.IsNullOrEmpty()) return false;
        var k = key.ToLowerInvariant();
        foreach (var kw in SensitiveKeyKeywords)
        {
            if (k.Contains(kw.ToLowerInvariant())) return true;
        }
        return false;
    }

    public static String Mask(String value) => value.IsNullOrEmpty() ? value : "***";
}
