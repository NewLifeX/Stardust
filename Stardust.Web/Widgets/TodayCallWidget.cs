using NewLife.Cube.Widgets;
using Stardust.Data.Monitors;

namespace Stardust.Web.Widgets;

/// <summary>今日调用量。工作台 KPI 指标，今日全部应用调用次数（AppDayStat 预聚合表，每应用每日一行）</summary>
[Widget("TodayCall", "今日调用量", Icon = "fa-bar-chart", Cols = 2, Sort = 4, Category = "星尘平台", Color = "purple", WidgetType = WidgetTypes.Kpi)]
public class TodayCallWidget : IWidget
{
    /// <summary>获取组件数据</summary>
    public Object GetData()
    {
        var today = AppDayStat.Search(DateTime.Today, null);
        var total = today.Sum(e => e.Total);

        var yesterday = AppDayStat.Search(DateTime.Today.AddDays(-1), null);
        var total2 = yesterday.Sum(e => e.Total);

        return new KpiData
        {
            Value = total.ToString("n0"),
            Trend = $"昨日 {total2:n0}",
            Url = "/Monitors/AppDayStat",
        };
    }
}
