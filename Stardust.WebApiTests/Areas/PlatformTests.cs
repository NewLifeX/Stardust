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
        "Platform/GalaxyProject/meta",
        "Platform/McpAudit/meta",
        "Platform/McpToken/meta",
        "Platform/McpTokenResource/meta",
        "Platform/ProjectUser/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }

    /// <summary>配置页（ConfigController）没有列表区段，字段统一在 form 区段下发。</summary>
    public static TheoryData<String> ConfigPages => new()
    {
        "Platform/StarServer/meta",
    };

    [Theory]
    [MemberData(nameof(ConfigPages))]
    public async Task Meta_ConfigPage_ReturnsFormFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "form", endpoint);
    }
}
