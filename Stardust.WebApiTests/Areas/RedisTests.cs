using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>Redis 域：Redis 节点与 Redis 数据。</summary>
[Collection(WebApiCollection.Name)]
public class RedisTests
{
    private readonly ApiClient _api;

    public RedisTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Redis/RedisNode/GetFields?kind=List",
        "Redis/RedisData/GetFields?kind=List",
        "Redis/RedisMessageQueue/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
