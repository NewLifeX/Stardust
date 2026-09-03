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
        "Nodes/Node/GetFields?kind=List",
        "Nodes/NodeOnline/GetFields?kind=List",
        "Nodes/NodeData/GetFields?kind=List",
        "Nodes/NodeHistory/GetFields?kind=List",
        "Nodes/NodeLocation/GetFields?kind=List",
        "Nodes/NodeRule/GetFields?kind=List",
        "Nodes/NodeFramework/GetFields?kind=List",
        "Nodes/NodeVersion/GetFields?kind=List",
        "Nodes/NodeStat/GetFields?kind=List",
        "Nodes/NodeCommand/GetFields?kind=List",
        "Nodes/DomainProvider/GetFields?kind=List",
        "Nodes/DotNetPackage/GetFields?kind=List",
        "Nodes/ProductPackage/GetFields?kind=List",
        "Nodes/ProductRelease/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
