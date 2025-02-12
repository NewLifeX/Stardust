using System;
using NewLife.Reflection;
using Stardust.Data.Monitors;
using Xunit;
using static Stardust.Server.Services.MonitorService;

namespace Stardust.ServerTests.Services;

public class MonitorServiceTests
{
    [Fact]
    public void GetClientTest()
    {
        var actor = new WebHookActor();
        actor.App = new AppTracer { WebHook = "https://newlifex.com/monitor/push?id=1234#token=abcd" };

        var uri = new Uri(actor.App.WebHook);
        Assert.Equal("newlifex.com", uri.Host);
        Assert.Equal("/monitor/push", uri.AbsolutePath);
        Assert.Equal("?id=1234", uri.Query);
        Assert.Equal("/monitor/push?id=1234", uri.PathAndQuery);

        var client = actor.GetClient();
        Assert.NotNull(client);
        Assert.Equal("/monitor/push?id=1234", actor.GetValue("_action"));
        Assert.Equal("abcd", actor.GetValue("_token"));
    }
}
