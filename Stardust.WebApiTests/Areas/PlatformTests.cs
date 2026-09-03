using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>平台域：星图项目与 MCP。</summary>
[Collection(WebApiCollection.Name)]
public class PlatformTests
{
    private readonly ApiClient _api;

    public PlatformTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Platform/GalaxyProject/GetFields?kind=List",
        "Platform/McpAudit/GetFields?kind=List",
        "Platform/McpToken/GetFields?kind=List",
        "Platform/McpTokenResource/GetFields?kind=List",
        "Platform/ProjectUser/GetFields?kind=List",
        "Platform/StarServer/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
