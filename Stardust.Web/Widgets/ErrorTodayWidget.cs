using NewLife.Cube.Widgets;
using Stardust.Data.Monitors;

namespace Stardust.Web.Widgets;

/// <summary>今日错误。工作台 KPI 指标，今日全部应用调用错误次数与错误率（AppDayStat 预聚合表）</summary>
[Widget("ErrorToday", "今日错误", Icon = "fa-exclamation-triangle", Cols = 2, Sort = 5, Category = "星尘平台", Color = "orange", WidgetType = WidgetTypes.Kpi)]
public class ErrorTodayWidget : IWidget
{
    /// <summary>获取组件数据</summary>
    public Object GetData()
    {
        var today = AppDayStat.Search(DateTime.Today, null);
        var total = today.Sum(e => e.Total);
        var errors = today.Sum(e => e.Errors);

        var rate = total > 0 ? (Double)errors / total : 0;

        return new KpiData
        {
            Value = errors.ToString("n0"),
            Trend = $"错误率 {rate:p2}",
            Url = "/Monitors/AppDayStat",
        };
    }
}
