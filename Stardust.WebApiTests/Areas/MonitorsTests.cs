using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>监控域：链路追踪与告警组。</summary>
[Collection(WebApiCollection.Name)]
public class MonitorsTests
{
    private readonly ApiClient _api;

    public MonitorsTests(WebApiTestFactory factory, ITestOutputHelper output) => _api = new(factory, output);

    public static TheoryData<String> Endpoints => new()
    {
        "Monitors/TraceData/GetFields?kind=List",
        "Monitors/Trace/GetFields?kind=List",
        "Monitors/AlarmGroup/GetFields?kind=List",
        "Monitors/AlarmHistory/GetFields?kind=List",
        "Monitors/AlarmRecord/GetFields?kind=List",
        "Monitors/AppDayStat/GetFields?kind=List",
        "Monitors/AppMinuteStat/GetFields?kind=List",
        "Monitors/AppTracer/GetFields?kind=List",
        "Monitors/SampleData2/GetFields?kind=List",
        "Monitors/SampleData/GetFields?kind=List",
        "Monitors/TraceDayStat/GetFields?kind=List",
        "Monitors/TraceHourStat/GetFields?kind=List",
        "Monitors/TraceItem/GetFields?kind=List",
        "Monitors/TraceMinuteStat/GetFields?kind=List",
        "Monitors/TraceRule/GetFields?kind=List",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task GetFields_ReturnsFieldList(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.FieldList(api.Data, endpoint);
    }
}
