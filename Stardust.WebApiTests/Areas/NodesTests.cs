using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>节点域：节点与在线节点。</summary>
[Collection(WebApiCollection.Name)]
public class NodesTests
{
    private readonly ApiClient _api;

    public NodesTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Nodes/Node/meta",
        "Nodes/NodeOnline/meta",
        "Nodes/NodeData/meta",
        "Nodes/NodeHistory/meta",
        "Nodes/NodeLocation/meta",
        "Nodes/NodeRule/meta",
        "Nodes/NodeFramework/meta",
        "Nodes/NodeVersion/meta",
        "Nodes/NodeStat/meta",
        "Nodes/NodeCommand/meta",
        "Nodes/DomainProvider/meta",
        "Nodes/DotNetPackage/meta",
        "Nodes/ProductPackage/meta",
        "Nodes/ProductRelease/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }
}
