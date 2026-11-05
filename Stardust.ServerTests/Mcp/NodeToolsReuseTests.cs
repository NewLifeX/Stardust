using System.IO;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// 节点只读 MCP 复用 MVC/API 已有查询，不再在工具内 FindAll 后内存过滤或分页。
/// 纯源码断言，不连数据库。
/// </summary>
public class NodeToolsReuseTests
{
    private static String Read(String relativePath) => File.ReadAllText(SourceRoot.FindPath(relativePath));

    /// <summary>截取某个类的源码，避免后面的写工具干扰断言。</summary>
    private static String ClassBody(String source, String className)
    {
        var marker = $"public class {className}";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"未找到 {className}");
        var next = source.IndexOf("public class ", start + marker.Length, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }

    [Fact(DisplayName = "节点列表复用 Node.Search / NodeOnline.Search，不再全表过滤分页")]
    public void NodeList_ReusesEntitySearch()
    {
        var body = ClassBody(Read("Stardust.Mcp/Tools/Nodes/NodeTools.cs"), "NodeListTool");

        Assert.Contains("McpGuard.NeedProject(ctx)", body);
        Assert.Contains("Node.Search(", body);
        Assert.Contains("NodeOnline.Search(", body);
        Assert.Contains("NodeOnline.FindByNodeId(", body);
        Assert.DoesNotContain("Node.FindAll", body);
        Assert.DoesNotContain("NodeOnline.FindAll(", body);
        Assert.DoesNotContain(".Skip(", body);
        Assert.DoesNotContain(".Take(", body);
    }

    [Fact(DisplayName = "节点详情复用 FindAllByNodeId 与 NodeCommand.Search")]
    public void NodeDetail_ReusesOnlineAndCommandSearch()
    {
        var body = ClassBody(Read("Stardust.Mcp/Tools/Nodes/NodeTools.cs"), "NodeDetailTool");

        Assert.Contains("McpGuard.Node(", body);
        Assert.Contains("NodeOnline.FindAllByNodeId(", body);
        Assert.Contains("NodeCommand.Search(", body);
        Assert.DoesNotContain("NodeCommand.FindAll", body);
        Assert.DoesNotContain("NodeOnline.FindAll(", body);
        Assert.Contains(".OrderByDescending(e => e.UpdateTime)", body);
        Assert.DoesNotContain(".Take(", body);
    }

    [Fact(DisplayName = "节点应用复用 AppOnline.Search")]
    public void NodeApps_ReusesAppOnlineSearch()
    {
        var body = ClassBody(Read("Stardust.Mcp/Tools/Nodes/NodeTools.cs"), "NodeAppsTool");

        Assert.Contains("McpGuard.Node(", body);
        Assert.Contains("AppOnline.Search(", body);
        Assert.DoesNotContain("AppOnline.FindAll", body);
    }

    [Fact(DisplayName = "节点/应用命令与进程工具仍委托 IMcpOperations")]
    public void WriteTools_StillDelegateOperations()
    {
        var source = Read("Stardust.Mcp/Tools/Nodes/NodeTools.cs");

        Assert.Contains("ops.SendNodeCommandAsync(", ClassBody(source, "NodeCommandTool"));
        Assert.Contains("ops.SendAppCommandAsync(", ClassBody(source, "AppCommandTool"));
        Assert.Contains("new AppCommandTool().InvokeAsync(", ClassBody(source, "AppProcessTool"));
    }

    [Fact(DisplayName = "节点、在线、命令、应用在线的 API 与 MVC 调用同一实体查询")]
    public void ApiAndMvc_ShareEntitySearch()
    {
        const String node = "return Node.Search(projectId, provinceId, cityId, category, product, kind, version, runtime, framework, arch, enable, start, end, p[\"Q\"], p);";
        const String online = "return NodeOnline.Search(projectId, nodeId, provinceId, cityId, category, start, end, p[\"Q\"], p);";
        const String command = "return NodeCommand.Search(nodeId, command, start, end, p[\"Q\"], p);";
        const String appOnline = "return AppOnline.Search(projectId, appId, nodeId, category, start, end, p[\"Q\"], p);";

        Assert.Contains(node, Read("Stardust.Web/Areas/Nodes/Controllers/NodeController.cs"));
        Assert.Contains(node, Read("Stardust.WebApi/Areas/Nodes/Controllers/NodeController.cs"));
        Assert.Contains(online, Read("Stardust.Web/Areas/Nodes/Controllers/NodeOnlineController.cs"));
        Assert.Contains(online, Read("Stardust.WebApi/Areas/Nodes/Controllers/NodeOnlineController.cs"));
        Assert.Contains(command, Read("Stardust.Web/Areas/Nodes/Controllers/NodeCommandController.cs"));
        Assert.Contains(command, Read("Stardust.WebApi/Areas/Nodes/Controllers/NodeCommandController.cs"));
        Assert.Contains(appOnline, Read("Stardust.Web/Areas/Registry/Controllers/AppOnlineController.cs"));
        Assert.Contains(appOnline, Read("Stardust.WebApi/Areas/Registry/Controllers/AppOnlineController.cs"));
    }
}
