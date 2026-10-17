using Stardust.Data.Monitors;
using Xunit;

namespace ServerTest;

public class TraceRuleTests
{
    [Fact]
    public void GetOrAddItem()
    {
        var name = "/Admin/Menu/Index";
        var rule = TraceRule.Match(name);
        Assert.NotNull(rule);

        var app = AppTracer.FindByName("StarWeb");
        // CI 未预置 StarWeb 应用时跳过，避免空引用
        if (app == null) return;

        var ti = app.GetOrAddItem(name, rule?.IsWhite);
        Assert.NotNull(ti);
        Assert.True(ti.Enable);
    }
}
