using NewLife.Cube.Charts;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Charts;

/// <summary>应用分钟统计图表。为监控大屏构建按应用聚合的横向堆叠柱状图</summary>
public static class AppMinuteStatChart
{
    /// <summary>构建监控大屏图表。错误数使用负值向左展开，总数使用正值向右展开</summary>
    /// <param name="list">按应用聚合后的分钟统计列表</param>
    /// <returns>ECharts 实例</returns>
    /// <remarks>
    /// NewLife.Cube 同时提供 <c>SetTooltip(trigger, axisPointerType, backgroundColor)</c> 与
    /// <c>SetTooltip(trigger, formatterScript)</c> 两个重载，直接传两个位置参数会命中后者，
    /// 把 axisPointerType 当作 JavaScript 函数解析而抛异常。这里必须使用命名参数指定 axisPointerType。
    /// </remarks>
    public static ECharts BuildDashboard(IReadOnlyList<AppMinuteStat> list)
    {
        var chart = new ECharts
        {
            Height = 800,
        };
        chart.SetTooltip("axis", axisPointerType: "shadow");
        chart.Legend = new Legend { Data = ["总数", "错误数"] };
        chart["grid"] = new { left = "3%", right = "4%", bottom = "3%", containLabel = true };

        chart.XAxis = [new XAxis { Type = "value" }];
        chart.YAxis = [
            new YAxis{
                Type = "category",
                AxisTick = new { show = false },
                Data = list.Select(e => e.AppName).ToArray()
            }
        ];

        chart.Add(new Series
        {
            Name = "错误数",
            Type = "bar",
            ["stack"] = "总量",
            ["label"] = new { show = true, position = "left" },
            ["itemStyle"] = new { color = "rgba(255, 0, 0, 0.5)", },
            Data = list.Select(e => (Object)(-e.Errors)).ToArray(),
        });
        chart.Add(new Series
        {
            Name = "总数",
            Type = "bar",
            ["stack"] = "总量",
            ["label"] = new { show = true },
            Data = list.Select(e => (Object)e.Total).ToArray(),
        });

        return chart;
    }
}
