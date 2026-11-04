using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Stardust.Monitors;
using Xunit;

namespace Stardust.Tests;

public class StarTracerOpenTelemetryTests
{
    [Fact]
    public void AttachOpenTelemetryCollectsActivities()
    {
        using var tracer = new StarTracer { EnableMeter = false };
        using var collector = tracer.AttachOpenTelemetry();

        Assert.NotNull(collector);
        Assert.Same(collector, tracer.OpenTelemetry);

        // 幂等，重复挂载返回同一实例
        Assert.Same(collector, tracer.AttachOpenTelemetry());

        var source = new ActivitySource("Stardust.Tests.Otel");
        using (var activity = source.StartActivity("Query", ActivityKind.Client))
        {
            activity!.SetTag("db.system", "redis");
        }

        var builder = tracer.TakeAll().Single(e => e.Name == "Stardust.Tests.Otel:Query");
        Assert.Equal(1, builder.Total);

        var samples = builder.Samples;
        Assert.NotNull(samples);

        var span = samples.Single();
        Assert.Equal(32, span.TraceId.Length);
        Assert.Equal(16, span.Id.Length);
        Assert.Contains("db.system = redis", span.Tag);
    }

    [Fact]
    public void DisabledDoesNotAttach()
    {
        using var tracer = new StarTracer { EnableOpenTelemetry = false };

        Assert.Null(tracer.AttachOpenTelemetry());
        Assert.Null(tracer.OpenTelemetry);
    }

    [Fact]
    [DisplayName("默认排除已被自研埋点覆盖的活动源")]
    public void DefaultExcludesCoveredSources()
    {
        using var tracer = new StarTracer { EnableMeter = false };
        using var collector = tracer.AttachOpenTelemetry();
        Assert.NotNull(collector);

        // ASP.NET Core 入站请求与 ADO.NET 驱动已被星尘埋点覆盖，默认不采集
        using var aspnet = new ActivitySource("Microsoft.AspNetCore");
        using var connector = new ActivitySource("connector-net");
        using var mySqlConnector = new ActivitySource("MySqlConnector");
        using var npgsql = new ActivitySource("Npgsql");
        Assert.False(collector.ShouldListenTo(aspnet));
        Assert.False(collector.ShouldListenTo(connector));
        Assert.False(collector.ShouldListenTo(mySqlConnector));
        Assert.False(collector.ShouldListenTo(npgsql));

        // 采集器原有的默认排除项保持不变
        using var http = new ActivitySource("System.Net.Http");
        Assert.False(collector.ShouldListenTo(http));

        // 没有自研埋点的第三方源仍然采集
        using var thirdParty = new ActivitySource("Stardust.Tests.ThirdParty");
        Assert.True(collector.ShouldListenTo(thirdParty));
    }

    [Fact]
    [DisplayName("被排除的活动源不产生埋点")]
    public void ExcludedSourceDoesNotReport()
    {
        using var tracer = new StarTracer { EnableMeter = false };
        using var collector = tracer.AttachOpenTelemetry();

        using var source = new ActivitySource("connector-net");
        using (source.StartActivity("SQL Statement", ActivityKind.Client)) { }

        using var thirdParty = new ActivitySource("Stardust.Tests.ThirdParty2");
        using (thirdParty.StartActivity("Query", ActivityKind.Client)) { }

        var names = tracer.TakeAll().Select(e => e.Name).ToList();
        Assert.Contains("Stardust.Tests.ThirdParty2:Query", names);
        Assert.DoesNotContain("connector-net:SQL Statement", names);
    }
}
