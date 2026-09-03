using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>网关域：网关路由与网关节点。</summary>
[Collection(WebApiCollection.Name)]
public class GatewayTests
{
    private readonly ApiClient _api;

    public GatewayTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Gateway/GatewayCert/GetFields?kind=List",
        "Gateway/GatewayCluster/GetFields?kind=List",
        "Gateway/GatewayNode/GetFields?kind=List",
        "Gateway/GatewayRoute/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
