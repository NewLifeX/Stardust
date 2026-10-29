extern alias web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Stardust.Data.Monitors;
using Xunit;
using web::Stardust.Web.Areas.Monitors.Charts;

namespace ServerTest.Monitors;

/// <summary>应用分钟统计监控大屏图表测试</summary>
public class AppMinuteStatChartTests
{
    [Fact]
    [DisplayName("监控大屏图表使用 axis+shadow 提示且可正常序列化")]
    public void BuildDashboard_UsesAxisShadowTooltip()
    {
        var list = new List<AppMinuteStat>
        {
            new() { AppId = 1, Total = 100, Errors = 5 },
            new() { AppId = 2, Total = 50, Errors = 0 },
        };

        var chart = AppMinuteStatChart.BuildDashboard(list);
        var json = chart.Build();

        // Cube 6.16 的 SetTooltip(trigger, axisPointerType) 与 SetTooltip(trigger, formatterScript) 重载冲突，
        // 旧代码传两个位置参数会命中 formatter 重载并抛 ArgumentOutOfRangeException
        Assert.Contains("\"trigger\":\"axis\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"type\":\"shadow\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formatter", json, StringComparison.OrdinalIgnoreCase);
    }
}
