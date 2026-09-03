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
        "Registry/App/GetFields?kind=List",
        "Registry/AppOnline/GetFields?kind=List",
        "Registry/AppMeter/GetFields?kind=List",
        "Registry/AppHistory/GetFields?kind=List",
        "Registry/AppClientLog/GetFields?kind=List",
        "Registry/AppCommand/GetFields?kind=List",
        "Registry/AppConsume/GetFields?kind=List",
        "Registry/AppService/GetFields?kind=List",
        "Registry/Service/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
