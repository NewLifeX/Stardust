using System.Net.Http.Json;
using System.Text.Json;
using NewLife;
using NewLife.Remoting.Models;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Managers;

namespace ServerTest.Fixtures;

/// <summary>给节点指令测试准备的临时日志目录和部署节点。节点编码来自已登录的 StarClient，不写死。</summary>
public sealed class DeployLogSeed : IDisposable
{
    public String LogDirectory { get; }
    public String LogFileName { get; } = "smoke-app.log";
    public const String SampleLine = "deploy-log-smoke-ok";

    public AppDeploy? Deploy { get; private set; }
    public AppDeployNode? DeployNode { get; private set; }

    public DeployLogSeed()
    {
        LogDirectory = Path.Combine(Path.GetTempPath(), "stardust-node-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(LogDirectory);
        File.WriteAllText(Path.Combine(LogDirectory, LogFileName), SampleLine + "\nline2\n");
    }

    /// <summary>把部署节点指到当前在线节点。调用方负责最终删除。</summary>
    public AppDeployNode Bind(Node node)
    {
        Deploy = new AppDeploy
        {
            Name = "logseed_" + Guid.NewGuid().ToString("N")[..8],
            Enable = true,
        };
        Deploy.Insert();

        DeployNode = new AppDeployNode
        {
            DeployId = Deploy.Id,
            NodeId = node.ID,
            Enable = true,
            LogDirectory = LogDirectory,
            LogFilePattern = "*.log",
        };
        DeployNode.Insert();
        return DeployNode;
    }

    public void Dispose()
    {
        try { DeployNode?.Delete(); } catch { }
        try { Deploy?.Delete(); } catch { }
        try { Directory.Delete(LogDirectory, true); } catch { }
    }
}

/// <summary>经 StarServer 向已登录节点下发指令，并解析统一信封里的 data。</summary>
public static class NodeCommandClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<CommandReplyModel?> SendAsync(String serverUrl, String token, String nodeCode, String command, String argument, Int32 timeout = 20)
    {
        using var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        http.DefaultRequestHeaders.Add("X-Token", token);
        var resp = await http.PostAsync("/Node/SendCommand", JsonContent.Create(new { code = nodeCode, command, argument, timeout }));
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode || body.IsNullOrEmpty()) return null;

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        return data.Deserialize<CommandReplyModel>(Json);
    }
}
