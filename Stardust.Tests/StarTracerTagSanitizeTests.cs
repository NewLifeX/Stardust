using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Stardust.Monitors;
using Xunit;

namespace Stardust.Tests;

public class StarTracerTagSanitizeTests
{
    [Fact]
    [DisplayName("埋点标签里的连接串与口令被脱敏")]
    public void SanitizeSecretTags()
    {
        Assert.True(StarTracer.EnableTagSanitize);

        using var tracer = new StarTracer { EnableMeter = false };
        using var collector = tracer.AttachOpenTelemetry();

        using var source = new ActivitySource("Stardust.Tests.Sanitize");
        using (var activity = source.StartActivity("Query", ActivityKind.Client))
        {
            activity!.SetTag("db.connection_string", "server=db.example.com;port=3306;database=stardust;user id=star;password=P@ssw0rd;");
            activity.SetTag("db.system", "mysql");
            activity.SetTag("auth", "password=P@ssw0rd;api_key=sk-123456");
        }

        var builders = tracer.TakeAll().ToArray();
        var builder = builders.Single(e => e.Name == "Stardust.Tests.Sanitize:Query");
        var span = builder.Samples!.Single();

        // 脱敏在上报前统一执行，这里直接调用同一入口
        StarTracer.SanitizeTags(builders);

        // 连接串整段遮蔽
        Assert.Contains("db.connection_string = ***", span.Tag);
        Assert.DoesNotContain("db.example.com", span.Tag);
        Assert.DoesNotContain("user id=star", span.Tag);

        // 独立的口令与密钥键值对被遮蔽
        Assert.DoesNotContain("P@ssw0rd", span.Tag);
        Assert.DoesNotContain("sk-123456", span.Tag);
        Assert.Contains("password=***", span.Tag);

        // 普通标签保持原样
        Assert.Contains("db.system = mysql", span.Tag);
    }
}
