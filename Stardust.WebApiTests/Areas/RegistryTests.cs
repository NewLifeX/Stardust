using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>注册中心域：应用与应用在线。</summary>
[Collection(WebApiCollection.Name)]
public class RegistryTests
{
    private readonly ApiClient _api;

    public RegistryTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Registry/App/meta",
        "Registry/AppOnline/meta",
        "Registry/AppMeter/meta",
        "Registry/AppHistory/meta",
        "Registry/AppClientLog/meta",
        "Registry/AppCommand/meta",
        "Registry/AppConsume/meta",
        "Registry/AppService/meta",
        "Registry/Service/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }
}
