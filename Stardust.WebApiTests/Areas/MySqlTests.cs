using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>MySQL 监控域：依赖 IMySqlService，无真实 MySQL 时框架不应崩溃。</summary>
[Collection(WebApiCollection.Name)]
public class MySqlTests
{
    private readonly ApiClient _api;

    public MySqlTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "MySql/MySqlData/GetFields?kind=List",
        "MySql/MySqlNode/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
