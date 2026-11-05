using NewLife.Security;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>DEPLOY-1 应用文件日志 API 关键路径（不依赖真实 StarAgent）。</summary>
[Collection(WebApiCollection.Name)]
public class DeploymentDeployLogApiTests
{
    private readonly ApiClient _api;

    public DeploymentDeployLogApiTests(WebApiTestFactory factory, ITestOutputHelper output) =>
        _api = new(factory, output);

    static String NewName(String prefix) => prefix + Rand.NextString(8);

    static (AppDeploy deploy, Node node, AppDeployNode dn) SeedDeployNode(String? nodeLogDir = null, String? deployLogDir = null)
    {
        var node = new Node { Code = NewName("logn_"), Name = "log-node", Enable = true };
        node.Insert();

        var deploy = new AppDeploy { Name = NewName("logd_"), Enable = true, LogDirectory = deployLogDir };
        deploy.Insert();

        var dn = new AppDeployNode
        {
            DeployId = deploy.Id,
            NodeId = node.ID,
            Enable = true,
            LogDirectory = nodeLogDir,
        };
        dn.Insert();

        return (deploy, node, dn);
    }

    static void Cleanup(AppDeploy deploy, Node node, AppDeployNode dn)
    {
        try { dn.Delete(); } catch { }
        try { deploy.Delete(); } catch { }
        try { node.Delete(); } catch { }
    }

    [Fact]
    public async Task LogFiles_WithoutLogDirectory_ReturnsBusinessError()
    {
        var (deploy, node, dn) = SeedDeployNode();
        try
        {
            var api = await _api.GetApiEnvelopeAsync($"Deployment/AppDeployNode/LogFiles?id={dn.Id}");
            Assert.NotEqual(0, api.Code);
            Assert.Contains("Log directory", api.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(deploy, node, dn);
        }
    }

    [Fact]
    public async Task LogTail_WithoutFile_ReturnsBusinessError()
    {
        var (deploy, node, dn) = SeedDeployNode(nodeLogDir: Path.GetTempPath());
        try
        {
            var api = await _api.GetApiEnvelopeAsync($"Deployment/AppDeployNode/LogTail?id={dn.Id}");
            Assert.NotEqual(0, api.Code);
            Assert.False(String.IsNullOrWhiteSpace(api.Message));
        }
        finally
        {
            Cleanup(deploy, node, dn);
        }
    }

    [Fact]
    public async Task LogFiles_InvalidId_ReturnsError()
    {
        var api = await _api.GetApiEnvelopeAsync("Deployment/AppDeployNode/LogFiles?id=999999999");
        Assert.NotEqual(0, api.Code);
    }

    [Fact]
    public async Task LogFiles_WithLogDirectory_ReachesNodeCommand_AndFailsWithoutAgent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-log-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (deploy, node, dn) = SeedDeployNode(nodeLogDir: dir);
        try
        {
            var api = await _api.GetApiEnvelopeAsync($"Deployment/AppDeployNode/LogFiles?id={dn.Id}");
            Assert.NotEqual(0, api.Code);
            // StarServer 在测试中指向不可达地址，应表现为超时/连接类错误，而非「未配置目录」
            Assert.DoesNotContain("Log directory is not configured", api.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(deploy, node, dn);
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void EffectiveLogDirectory_NodeOverridesDeploy()
    {
        var nodeEnt = new Node { Code = NewName("logm_"), Name = "m", Enable = true };
        nodeEnt.Insert();
        var deploy = new AppDeploy
        {
            Name = NewName("logm_d_"),
            Enable = true,
            LogDirectory = @"D:\logs-from-deploy",
            LogFilePattern = "*.txt",
        };
        deploy.Insert();
        var dn = new AppDeployNode
        {
            DeployId = deploy.Id,
            NodeId = nodeEnt.ID,
            Enable = true,
            LogDirectory = @"D:\logs-from-node",
        };
        dn.Insert();
        try
        {
            var loaded = AppDeployNode.Find(AppDeployNode._.Id == dn.Id);
            Assert.NotNull(loaded);
            Assert.Equal(@"D:\logs-from-node", loaded!.GetEffectiveLogDirectory());
            Assert.Equal("*.txt", loaded.GetEffectiveLogFilePattern());
        }
        finally
        {
            try { dn.Delete(); } catch { }
            try { deploy.Delete(); } catch { }
            try { nodeEnt.Delete(); } catch { }
        }
    }

    [Fact]
    public void EffectiveLogDirectory_FallsBackToDeploy()
    {
        var nodeEnt = new Node { Code = NewName("logf_"), Name = "f", Enable = true };
        nodeEnt.Insert();
        var deploy = new AppDeploy
        {
            Name = NewName("logf_d_"),
            Enable = true,
            LogDirectory = @"D:\logs-deploy-only",
        };
        deploy.Insert();
        var dn = new AppDeployNode { DeployId = deploy.Id, NodeId = nodeEnt.ID, Enable = true };
        dn.Insert();
        try
        {
            var loaded = AppDeployNode.Find(AppDeployNode._.Id == dn.Id);
            Assert.Equal(@"D:\logs-deploy-only", loaded!.GetEffectiveLogDirectory());
            Assert.Equal("*.log", loaded.GetEffectiveLogFilePattern());
        }
        finally
        {
            try { dn.Delete(); } catch { }
            try { deploy.Delete(); } catch { }
            try { nodeEnt.Delete(); } catch { }
        }
    }
}
