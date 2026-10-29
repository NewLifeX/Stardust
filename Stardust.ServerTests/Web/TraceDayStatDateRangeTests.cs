extern alias web;
using System;
using System.ComponentModel;
using System.Reflection;
using Xunit;

namespace ServerTest.Web;

/// <summary>跟踪天统计默认时间范围测试</summary>
public class TraceDayStatDateRangeTests
{
    private static MethodInfo GetMethod() =>
        typeof(web::Stardust.Web.Areas.Monitors.MonitorSearchHelper)
            .GetMethod("ResolveDateRange", BindingFlags.Public | BindingFlags.Static);

    private static (DateTime Start, DateTime End) Invoke(Int32 appId, Int32 itemId, DateTime date, DateTime start, DateTime end)
    {
        var method = GetMethod();
        Assert.NotNull(method);

        Object[] args = [appId, itemId, date, start, end];
        method.Invoke(null, args);

        return ((DateTime)args[3], (DateTime)args[4]);
    }

    [Fact]
    [DisplayName("列表页未选时间时默认只查当天，避免全表扫描与历史异常值溢出")]
    public void ResolveDateRange_DefaultsToTodayForList()
    {
        var (start, end) = Invoke(-1, -1, default, default, default);

        Assert.Equal(DateTime.Today, start);
        Assert.Equal(DateTime.Today, end);
    }

    [Fact]
    [DisplayName("选中应用时不限制时间，保持趋势查询")]
    public void ResolveDateRange_KeepsEmptyRangeForApp()
    {
        var (start, end) = Invoke(1, -1, default, default, default);

        Assert.Equal(default, start);
        Assert.Equal(default, end);
    }

    [Fact]
    [DisplayName("显式指定日期或时间范围时不被覆盖")]
    public void ResolveDateRange_RespectsExplicitRange()
    {
        var day = new DateTime(2026, 10, 28);
        var (start, end) = Invoke(-1, -1, day, default, default);
        Assert.Equal(day, start);
        Assert.Equal(day, end);

        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 20);
        (start, end) = Invoke(-1, -1, day, from, to);
        Assert.Equal(from, start);
        Assert.Equal(to, end);
    }
}
