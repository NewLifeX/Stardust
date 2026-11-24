using System.Text.Json;
using NewLife;
using Stardust.Data.Deployment;
using Stardust.Data.Platform;

namespace Stardust.Mcp.Tools.Deploy;

/// <summary>部署节点文件日志工具公共解析。经节点指令 deploy/log/list|tail 读节点磁盘，
/// 与 stardust_app_logs（SDK 上报的 AppClientLog 库表）是两条链路（Doc/DEPLOY-1-节点应用日志.md）。</summary>
internal static class DeployLogToolHelper
{
    /// <summary>解析部署节点并校验令牌项目归属：deploy_node_id → AppDeployNode → McpGuard.Deploy（项目 + 应用资源授权）。</summary>
    public static (AppDeployNode Node, AppDeploy Deploy) ResolveDeployNode(JsonElement args, McpContext ctx)
    {
        var id = McpToolBase.ReadInt(args, "deploy_node_id");
        if (id <= 0) throw new McpException(-32602, "无效参数：缺少 deploy_node_id");
        var node = ProjectScopeRules.FilterVisible(AppDeployNode.FindById(id)) ?? throw new McpException(-32602, "部署节点不存在。");
        var deploy = McpGuard.Deploy(ctx, node.DeployId);
        return (node, deploy);
    }

    public static IMcpOperations GetOperations(McpContext ctx)
        => ctx.Services.GetService(typeof(IMcpOperations)) as IMcpOperations
           ?? throw new McpException(-32000, "宿主未注册 MCP 操作适配器。");
}

/// <summary>列出部署节点配置目录下的日志文件（Agent 端 deploy/log/list，最多 100 个，最新写入在前）。</summary>
public class DeployLogFilesTool : McpToolBase
{
    public override String Name => "stardust_deploy_log_files";
    public override String Description => "List local log files on a deployment node (the agent reads its own disk via the deploy/log/list command). Newest first, max 100 files. Returns an empty list with a note when the log directory is not configured or the node is offline. Pair with stardust_deploy_log_tail.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "deploy";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_node_id = new { type = "integer", description = "部署节点编号 AppDeployNode.Id（不是 NodeId）" },
        },
        required = new[] { "deploy_node_id" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var (node, deploy) = DeployLogToolHelper.ResolveDeployNode(args, ctx);
        var ops = DeployLogToolHelper.GetOperations(ctx);
        try
        {
            var data = await ops.ReadDeployLogFilesAsync(node.Id);
            return McpResult.Ok($"{deploy.Name} 部署节点 #{node.Id}（节点 {node.NodeId}）日志文件列表", data,
                ["stardust_deploy_log_tail"], "选中文件名后调 stardust_deploy_log_tail 读取尾部内容");
        }
        catch (Exception ex)
        {
            return McpResult.Ok($"未能读取日志文件列表：{ex.Message}",
                new { files = Array.Empty<Object>(), note = ex.Message },
                null, "按 note 处理：未配置日志目录时在部署集或部署节点编辑里填写 LogDirectory；节点离线时先恢复 StarAgent 在线");
        }
    }
}

/// <summary>读取部署节点上单个日志文件的尾部（Agent 端 deploy/log/tail）。offset 传上一次返回的 file_size 可增量追新。</summary>
public class DeployLogTailTool : McpToolBase
{
    public const Int32 MaxBytes = 512 * 1024;

    public override String Name => "stardust_deploy_log_tail";
    public override String Description => "Read the tail of one log file on a deployment node (agent deploy/log/tail). For incremental polling pass offset = file_size from the previous reply to fetch only new bytes; empty content with offset == file_size means nothing new. max_bytes default 262144, capped at 524288.";
    public override McpRiskLevel Risk => McpRiskLevel.L0;
    public override String ScopeDomain => "deploy";

    public override JsonElement InputSchema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            deploy_node_id = new { type = "integer", description = "部署节点编号 AppDeployNode.Id（不是 NodeId）" },
            file = new { type = "string", description = "日志文件名（仅文件名，来自 stardust_deploy_log_files，不允许路径分隔符）" },
            offset = new { type = "integer", description = "起始偏移；增量轮询传上一次返回的 file_size，0=读文件尾部" },
            max_bytes = new { type = "integer", description = "最多读取字节数，默认 262144，上限 524288" },
        },
        required = new[] { "deploy_node_id", "file" },
    });

    public override async Task<Object> InvokeAsync(JsonElement args, McpContext ctx)
    {
        var (node, deploy) = DeployLogToolHelper.ResolveDeployNode(args, ctx);
        var file = ReadString(args, "file");
        if (file.IsNullOrEmpty()) throw new McpException(-32602, "无效参数：缺少 file，可先调 stardust_deploy_log_files。");
        if (file.Contains("..", StringComparison.Ordinal) || file.Contains('/') || file.Contains('\\'))
            throw new McpException(-32602, "无效参数：file 只能是文件名，不允许路径分隔符。");

        var offset = GetInt64(args, "offset");
        var maxBytes = GetInt32(args, "max_bytes");
        if (maxBytes <= 0) maxBytes = 262144;
        if (maxBytes > MaxBytes) maxBytes = MaxBytes;

        var ops = DeployLogToolHelper.GetOperations(ctx);
        try
        {
            var data = await ops.ReadDeployLogTailAsync(node.Id, file, offset, maxBytes);
            return McpResult.Ok($"{deploy.Name} 部署节点 #{node.Id} 日志 {file}", data,
                null, "增量轮询：下一次把 offset 设为本次返回的 file_size，即可只取新增内容");
        }
        catch (Exception ex)
        {
            return McpResult.Ok($"读取日志失败：{ex.Message}",
                new { content = "", offset, file_size = 0L, truncated = false, note = ex.Message },
                null, "按 note 处理：未配置日志目录/文件不存在/节点离线，分别对应填写 LogDirectory、重选文件、恢复 StarAgent 在线");
        }
    }
}
