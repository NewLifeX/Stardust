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
}
