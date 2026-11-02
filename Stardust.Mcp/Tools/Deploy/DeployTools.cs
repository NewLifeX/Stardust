using System.Text.Json;
using NewLife;
using Stardust.Data.Deployment;

namespace Stardust.Mcp.Tools.Deploy;

static class DeployLookup
{
    public static AppDeploy ResolveDeploy(JsonElement args, McpContext ctx)
    {
        if (args.TryGetProperty("deploy_id", out var deployId) && deployId.ValueKind == JsonValueKind.Number && deployId.GetInt32() > 0)
            return McpGuard.Deploy(ctx, deployId.GetInt32());
        return McpGuard.DeployByApp(ctx, McpGuard.FindApp(ctx, McpToolBase.ReadInt(args, "app_id"), McpToolBase.ReadString(args, "app_name")).Id);
    }
}

public class DeployStatusTool : McpToolBase
{
    public override String Name => "stardust_deploy_status";
    public override String Description => "Answer whether a release landed: current version, nodes behind the target, process liveness and recent dispatch records.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_id = new { type = "integer", description = "部署集编号，与 app_id 二选一" },
            app_id = new { type = "integer", description = "应用编号，与 deploy_id 二选一" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var deploy = args.TryGetProperty("deploy_id", out _) ? McpGuard.Deploy(ctx, GetInt32(args, "deploy_id")) : McpGuard.DeployByApp(ctx, GetInt32(args, "app_id"));
        var nodes = AppDeployNode.FindAll(AppDeployNode._.DeployId == deploy.Id);
        var behind = nodes.Where(e => e.Enable && e.Version != deploy.Version).Select(e => new { e.Id, e.NodeId, e.IP, current = e.Version, target = deploy.Version }).ToArray();
        var alive = nodes.Count(e => e.ProcessId > 0);
        return Task.FromResult<Object>(McpResult.Ok($"{deploy.Name} 当前版本 {deploy.Version}，{behind.Length} 台落后，{alive} 台进程存活", new
        {
            deploy_id = deploy.Id,
            version = deploy.Version,
            auto_publish = deploy.AutoPublish,
            behind,
            alive,
        }, ["stardust_deploy_node_operate", "stardust_deploy_history"]));
    }
}

public class DeployVersionsTool : McpToolBase
{
    public override String Name => "stardust_deploy_versions";
    public override String Description => "List deployment versions with enable state, package size, commit and usability.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_id = new { type = "integer", description = "部署集编号" },
            app_id = new { type = "integer", description = "应用编号" },
            app_name = new { type = "string", description = "应用名" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var deploy = DeployLookup.ResolveDeploy(args, ctx);
        var rows = AppDeployVersion.FindAll(AppDeployVersion._.DeployId == deploy.Id).OrderByDescending(e => e.Id)
            .Select(e => new { e.Id, e.Version, e.Enable, e.Size, e.CommitId, commit = e.CommitLog, usable = e.Enable && !e.Url.IsNullOrEmpty() }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"{deploy.Name} 版本 {rows.Length} 个", new { records = rows }));
    }
}

public class DeployHistoryTool : McpToolBase
{
    public override String Name => "stardust_deploy_history";
    public override String Description => "List deployment dispatch logs for an application or deployment set. Pass app_id, app_name or deploy_id. Success means the command was submitted, not that the node finished it.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_id = new { type = "integer", description = "部署集编号" },
            app_id = new { type = "integer", description = "应用编号" },
            app_name = new { type = "string", description = "应用名" },
            since = new { type = "string", description = "时间窗，如 -24h" },
            page_size = new { type = "integer", description = "每页条数" },
        },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var deploy = DeployLookup.ResolveDeploy(args, ctx);
        var exp = AppDeployHistory._.DeployId == deploy.Id & AppDeployHistory._.CreateTime >= Since(args);
        var all = AppDeployHistory.FindAll(exp).OrderByDescending(e => e.Id).ToArray();
        var total = all.Length;
        var rows = all.Skip((Page(args) - 1) * PageSize(args)).Take(PageSize(args))
            .Select(e => new { e.Id, time = e.CreateTime, e.NodeId, e.Action, e.Success, e.Remark }).ToArray();
        return Task.FromResult<Object>(McpResult.Ok($"部署下发记录 {rows.Length}/{total} 条。成功只表示命令已下发，不是节点执行结果", new { records = rows },
            null, null, McpResult.Paged(total, Page(args), PageSize(args))));
    }
}

public class PipelineRunDetailTool : McpToolBase
{
    public override String Name => "stardust_pipeline_run_detail";
    public override String Description => "Read one pipeline run with steps, commit and a warning when every step was skipped but the run is marked success.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            run_id = new { type = "integer", description = "流水线运行编号" },
        },
        required = new[] { "run_id" },
    });

    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var run = AppPipelineRun.FindById(GetInt32(args, "run_id")) ?? throw new McpException(-32602, "流水线运行不存在。");
        var pipeline = AppPipeline.FindById(run.PipelineId) ?? throw new McpException(-32602, "流水线不存在。");
        McpGuard.Deploy(ctx, pipeline.DeployId);
        var steps = AppPipelineStep.FindAll(AppPipelineStep._.RunId == run.Id);
        var allSkipped = steps.Count > 0 && steps.All(e => e.Status.EqualIgnoreCase("Skipped"));
        return Task.FromResult<Object>(McpResult.Ok(allSkipped ? "该运行标记成功，但所有步骤都被跳过，没有节点实际部署" : $"运行 {run.Id} 状态 {run.Status}", new
        {
            run.Id,
            status = run.Status.ToString(),
            run.CommitId,
            run.CommitMessage,
            all_skipped = allSkipped,
            steps = steps.Select(e => new { e.StepType, e.NodeId, e.Status, e.Message }).ToArray(),
        }));
    }
}

public class UseVersionTool : McpToolBase
{
    public override String Name => "stardust_deploy_use_version";
    public override String Description => "Switch the effective deployment version. If auto publish is off, this only changes the version number and does not install nodes.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            version_id = new { type = "integer", description = "版本编号" },
        },
        required = new[] { "version_id" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var ver = AppDeployVersion.FindById(GetInt32(args, "version_id")) ?? throw new McpException(-32602, "版本不存在。");
        var deploy = McpGuard.Deploy(ctx, ver.DeployId);
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        var message = await ops.UseVersionAsync(ver.Id, ctx.CallerIp);
        var hint = deploy.AutoPublish ? "已开启自动发布，启用节点会收到安装命令" : "未开启自动发布，仅切换版本号，需要 stardust_deploy_node_operate 或 stardust_deploy_release 才会下发";
        return McpResult.Ok($"版本已切换为 {ver.Version}。{hint}", new { deploy_id = deploy.Id, version = ver.Version, auto_publish = deploy.AutoPublish, message },
            null, "如需回退到旧版本，重新调用 stardust_deploy_use_version 选择旧版本编号");
    }
}

public class NodeOperateTool : McpToolBase
{
    public override String Name => "stardust_deploy_node_operate";
    public override String Description => "Operate one deployment node: install, start, restart, stop or uninstall. stop and uninstall require confirm=true because they disable the node.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_node_id = new { type = "integer", description = "部署节点编号" },
            action = new { type = "string", @enum = new[] { "install", "start", "restart", "stop", "uninstall" }, description = "节点操作" },
            confirm = new { type = "boolean", description = "stop/uninstall 必须传 true" },
        },
        required = new[] { "deploy_node_id", "action" },
    });
    public override Task<Object> InvokeAsync(JsonElement args, McpContext ctx) => Operate(args, ctx);

    internal static async Task<Object> Operate(JsonElement args, McpContext ctx)
    {
        var node = AppDeployNode.FindById(GetInt32(args, "deploy_node_id")) ?? throw new McpException(-32602, "部署节点不存在。");
        var deploy = McpGuard.Deploy(ctx, node.DeployId);
        var act = GetString(args, "action");
        if (!new[] { "install", "start", "restart", "stop", "uninstall" }.Contains(act))
            throw new McpException(-32602, "action 只能是 install/start/restart/stop/uninstall。");
        if ((act == "stop" || act == "uninstall") && !GetBoolean(args, "confirm"))
            throw new McpException(-32003, "stop/uninstall 会把部署节点 Enable 置为 false，必须传 confirm=true。");
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        var id = await ops.ControlDeployAsync(deploy.Id, node.Id, act, ctx.CallerIp);
        var rollback = act is "stop" or "uninstall"
            ? "节点已被停用；如需恢复，调用 stardust_deploy_node_operate 执行 install 或 start"
            : "如需回滚此操作，调用 stardust_deploy_history 查看结果后用 stardust_deploy_node_operate 下发相反动作";
        return McpResult.Ok($"已向节点 {node.NodeId} 下发 {act}。命令已发出，结果看部署历史", new { command_id = id, node.Id, action = act },
            null, rollback);
    }
}

public class DeployReleaseTool : McpToolBase
{
    public override String Name => "stardust_deploy_release";
    public override String Description => "Release an application. Pass app_id or app_name, and version or version_id. Omit deploy_node_ids to release all enabled nodes. dry_run=true only returns the plan.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_id = new { type = "integer", description = "部署集编号" },
            app_id = new { type = "integer", description = "应用编号" },
            app_name = new { type = "string", description = "应用名" },
            version_id = new { type = "integer", description = "版本编号，与 version 二选一" },
            version = new { type = "string", description = "版本号，与 version_id 二选一" },
            deploy_node_ids = new { type = "array", items = new { type = "integer" }, description = "目标节点编号，空表示全部启用节点" },
            dry_run = new { type = "boolean", description = "只出计划不执行" },
        },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var deploy = args.TryGetProperty("deploy_id", out var deployId) && deployId.GetInt32() > 0
            ? McpGuard.Deploy(ctx, deployId.GetInt32())
            : McpGuard.DeployByApp(ctx, McpGuard.FindApp(ctx, GetInt32(args, "app_id"), GetString(args, "app_name")).Id);
        AppDeployVersion ver;
        if (GetInt32(args, "version_id") > 0)
            ver = AppDeployVersion.FindById(GetInt32(args, "version_id")) ?? throw new McpException(-32602, "版本不存在。");
        else
        {
            var version = GetString(args, "version");
            if (version.IsNullOrEmpty()) throw new McpException(-32602, "请传 version 或 version_id。可先调 stardust_deploy_versions。");
            ver = AppDeployVersion.FindAll(AppDeployVersion._.DeployId == deploy.Id & AppDeployVersion._.Version == version).FirstOrDefault()
                ?? throw new McpException(-32602, $"部署集里没有版本 {version}。");
        }
        if (ver.DeployId != deploy.Id) throw new McpException(-32602, "版本不属于该部署集。");
        var nodes = args.TryGetProperty("deploy_node_ids", out var idEl) && idEl.ValueKind == JsonValueKind.Array
            ? idEl.EnumerateArray().Select(e => AppDeployNode.FindById(e.GetInt32())).Where(e => e != null && e.DeployId == deploy.Id).ToArray()
            : AppDeployNode.FindAll(AppDeployNode._.DeployId == deploy.Id).Where(e => e.Enable).ToArray();
        if (nodes.Length == 0) throw new McpException(-32602, "没有可发布的启用节点。");
        var plan = nodes.Select(e => new { e.Id, e.NodeId, from = e.Version, to = ver.Version }).ToArray();
        if (GetBoolean(args, "dry_run")) return McpResult.Ok($"发布计划 {plan.Length} 台，未执行", new { plan });
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        await ops.UseVersionAsync(ver.Id, ctx.CallerIp);
        var results = new List<Object>();
        foreach (var node in nodes)
        {
            var id = await ops.ControlDeployAsync(deploy.Id, node.Id, "install", ctx.CallerIp);
            results.Add(new { node.Id, command_id = id, status = "submitted" });
        }
        return McpResult.Ok($"已切换到 {ver.Version} 并向 {results.Count} 台下发安装。命令已发出，结果看部署历史", new { results },
            null, "如需回滚，调用 stardust_deploy_use_version 切回旧版本号，再对目标节点下发 install");
    }
}

public class PipelineTriggerTool : McpToolBase
{
    public override String Name => "stardust_pipeline_trigger";
    public override String Description => "Manually trigger a pipeline. The result distinguishes a created run from a rejected trigger and includes the reason.";
    public override McpRiskLevel Risk => McpRiskLevel.L1;
    public override String ScopeDomain => "deploy";
    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            pipeline_id = new { type = "integer", description = "流水线编号" },
        },
        required = new[] { "pipeline_id" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var pipeline = AppPipeline.FindById(GetInt32(args, "pipeline_id")) ?? throw new McpException(-32602, "流水线不存在。");
        McpGuard.Deploy(ctx, pipeline.DeployId);
        var ops = ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations ?? throw new McpException(-32000, "宿主未注册 MCP 写操作适配器。");
        try
        {
            var run = await ops.TriggerPipelineAsync(pipeline.Id, ctx.CallerIp);
            return McpResult.Ok("流水线已创建运行", new { created = true, run },
                null, "如构建失败，调用 stardust_pipeline_run_detail 查看失败步骤，修正后重新触发");
        }
        catch (Exception ex)
        {
            return McpResult.Ok("流水线未创建运行：" + ex.Message, new { created = false, reason = ex.Message });
        }
    }
}
