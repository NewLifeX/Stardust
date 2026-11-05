using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.McpApiTests;

/// <summary>部署节点文件日志工具（stardust_deploy_log_files / stardust_deploy_log_tail）集成测试。
/// 锁五件事：① 未配置日志目录 → 优雅空数据+note（配置合并处快速失败，不下发节点指令）
/// ② 不存在的部署节点 -32602 ③ 缺 file / 路径分隔符 -32602 ④ 非成员令牌 -32003（项目归属校验）
/// ⑤ tail 空 content+note。不依赖真实节点 Agent（Doc/DEPLOY-1 §9；真实磁盘读取由 L2/L3 覆盖）。</summary>
[Collection(nameof(McpApiTestCollection))]
public class DeployLogToolsTests : McpSdkTestBase
{
    public DeployLogToolsTests(ITestOutputHelper output) : base(output) { }

    private static String NoteOf(JsonElement content)
    {
        var data = content.GetProperty("data");
        return data.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String ? note.GetString() ?? "" : "";
    }

    private static void AssertNoDirectoryNote(String note)
    {
        Assert.True(
            note.Contains("Log directory", StringComparison.OrdinalIgnoreCase) || note.Contains("日志目录"),
            $"note 应含未配置日志目录话术，实际：{note}");
    }

    [Fact(DisplayName = "部署日志-1：log_files 未配置日志目录 → 空列表+note 而非异常")]
    public async Task LogFiles_WithoutDirectory_EmptyGraceful()
    {
        // 初始化器的部署节点未配置 LogDirectory：DeployService 配置合并即抛「Log directory is not configured…」，
        // 工具层兜底为空数据+note（不下发节点指令，无需 Agent 在线）。
        var deployNodeId = McpTestDataInitializer.DeployNodeId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_deploy_log_files",
                new Dictionary<String, Object?> { ["deploy_node_id"] = deployNodeId }));
            var data = content.GetProperty("data");
            Assert.Equal(0, data.GetProperty("files").GetArrayLength());
            var note = NoteOf(content);
            AssertNoDirectoryNote(note);
            Output.WriteLine($"✅ 未配置目录 → 空列表+note");
        });
    }

    [Fact(DisplayName = "部署日志-2：log_files 部署节点不存在 → -32602")]
    public async Task LogFiles_NodeNotFound_Rejected()
    {
        await AssertRejectedAsync(async () => await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            await client.CallToolAsync("stardust_deploy_log_files",
                new Dictionary<String, Object?> { ["deploy_node_id"] = 999999999 });
        }), -32602);
        Output.WriteLine("✅ 不存在的部署节点 -32602");
    }

    [Fact(DisplayName = "部署日志-3：log_tail 缺 file → -32602")]
    public async Task LogTail_WithoutFile_Rejected()
    {
        var deployNodeId = McpTestDataInitializer.DeployNodeId;
        await AssertRejectedAsync(async () => await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            await client.CallToolAsync("stardust_deploy_log_tail",
                new Dictionary<String, Object?> { ["deploy_node_id"] = deployNodeId });
        }), -32602);
        Output.WriteLine("✅ 缺 file -32602");
    }

    [Fact(DisplayName = "部署日志-4：log_tail 文件名带路径分隔符 → -32602")]
    public async Task LogTail_PathSeparator_Rejected()
    {
        var deployNodeId = McpTestDataInitializer.DeployNodeId;
        await AssertRejectedAsync(async () => await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            await client.CallToolAsync("stardust_deploy_log_tail",
                new Dictionary<String, Object?> { ["deploy_node_id"] = deployNodeId, ["file"] = "../evil.log" });
        }), -32602);
        Output.WriteLine("✅ 路径分隔符 -32602");
    }

    [Fact(DisplayName = "部署日志-5：log_tail 未配置 → 空 content+note（快速失败）")]
    public async Task LogTail_WithoutDirectory_EmptyGraceful()
    {
        var deployNodeId = McpTestDataInitializer.DeployNodeId;
        await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-full"], async client =>
        {
            var content = AssertOk(await client.CallToolAsync("stardust_deploy_log_tail",
                new Dictionary<String, Object?> { ["deploy_node_id"] = deployNodeId, ["file"] = "app.log" }));
            var data = content.GetProperty("data");
            Assert.Equal("", data.GetProperty("content").GetString());
            Assert.False(data.GetProperty("truncated").GetBoolean());
            AssertNoDirectoryNote(NoteOf(content));
            Output.WriteLine("✅ tail 未配置目录 → 空 content+note");
        });
    }

    [Fact(DisplayName = "部署日志-6：非成员令牌 → -32003（部署集项目归属校验）")]
    public async Task LogFiles_NonMemberToken_Rejected()
    {
        var deployNodeId = McpTestDataInitializer.DeployNodeId;
        await AssertRejectedAsync(async () => await RunSdkAsync(McpTestDataInitializer.Tokens["mcpapi-nonmember"], async client =>
        {
            await client.CallToolAsync("stardust_deploy_log_files",
                new Dictionary<String, Object?> { ["deploy_node_id"] = deployNodeId });
        }), -32003);
        Output.WriteLine("✅ 非成员令牌 -32003");
    }
}
