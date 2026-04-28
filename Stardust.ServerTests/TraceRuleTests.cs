using Stardust.Data.Monitors;
using Xunit;

namespace Stardust.ServerTests;

public class TraceRuleTests
{
    [Fact]
    public void GetOrAddItem()
    {
        var name = "/Admin/Menu/Index";
        var rule = TraceRule.Match(name);
        Assert.NotNull(rule);

        var app = AppTracer.FindByName("StarWeb");
        var ti = app.GetOrAddItem(name, rule?.IsWhite);
        Assert.NotNull(ti);
        Assert.True(ti.Enable);
    }
}
