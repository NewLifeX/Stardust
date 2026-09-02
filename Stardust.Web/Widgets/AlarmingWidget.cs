using NewLife.Cube.Widgets;
using NewLife.Data;
using Stardust.Data.Monitors;

namespace Stardust.Web.Widgets;

/// <summary>报警中。工作台 KPI 指标，当前未恢复的告警数与今日触发次数（AlarmRecord/AlarmHistory 生命周期）</summary>
[Widget("Alarming", "报警中", Icon = "fa-bell", Cols = 2, Sort = 6, Category = "星尘平台", Color = "red", WidgetType = WidgetTypes.Kpi)]
public class AlarmingWidget : IWidget
{
    /// <summary>获取组件数据</summary>
    public Object GetData()
    {
        var count = AlarmRecord.FindAllByStatus(AlarmStatuses.Alarming).Count;

        // 今日触发次数，AlarmHistory 按雪花主键时间区间计数
        var page = new PageParameter();
        AlarmHistory.Search(-1, DateTime.Today, DateTime.Now, null, page);

        return new KpiData
        {
            Value = count.ToString("n0"),
            Trend = $"今日触发 {page.TotalCount:n0}",
            Url = "/Monitors/AlarmRecord",
        };
    }
}
