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
        "Gateway/GatewayCert/meta",
        "Gateway/GatewayCluster/meta",
        "Gateway/GatewayNode/meta",
        "Gateway/GatewayRoute/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }
}
