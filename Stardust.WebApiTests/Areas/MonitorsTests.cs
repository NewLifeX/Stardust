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
        "Monitors/TraceData/meta",
        "Monitors/Trace/meta",
        "Monitors/AlarmGroup/meta",
        "Monitors/AlarmHistory/meta",
        "Monitors/AlarmRecord/meta",
        "Monitors/AppDayStat/meta",
        "Monitors/AppMinuteStat/meta",
        "Monitors/AppTracer/meta",
        "Monitors/SampleData2/meta",
        "Monitors/SampleData/meta",
        "Monitors/TraceDayStat/meta",
        "Monitors/TraceHourStat/meta",
        "Monitors/TraceItem/meta",
        "Monitors/TraceMinuteStat/meta",
        "Monitors/TraceRule/meta",
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Meta_ReturnsListFields(String endpoint)
    {
        var api = await _api.GetJsonAsync(endpoint);

        ApiAssert.MetaFields(api.Data, "list", endpoint);
    }
}
