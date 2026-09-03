using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>配置域：应用配置与配置数据。</summary>
[Collection(WebApiCollection.Name)]
public class ConfigsTests
{
    private readonly ApiClient _api;

    public ConfigsTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Configs/AppConfig/GetFields?kind=List",
        "Configs/AppRule/GetFields?kind=List",
        "Configs/ConfigData/GetFields?kind=List",
        "Configs/ConfigHistory/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }

    [Fact]
    public async Task AppConfig_Index_IsAccessible()
    {
        var resp = await _api.GetAsync("Configs/AppConfig");

        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }
}
