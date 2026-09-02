using NewLife.Cube.Widgets;
using Stardust.Data;

namespace Stardust.Web.Widgets;

/// <summary>在线应用。工作台 KPI 指标，当前在线应用实例数（AppOnline 会话表，非单例超时清理、单例保留2天）</summary>
[Widget("OnlineApp", "在线应用", Icon = "fa-tasks", Cols = 2, Sort = 3, Category = "星尘平台", Color = "cyan", WidgetType = WidgetTypes.Kpi)]
public class OnlineAppWidget : IWidget
{
    /// <summary>获取组件数据</summary>
    public Object GetData()
    {
        var online = AppOnline.Meta.Count;
        var apps = App.Meta.Count;

        return new KpiData
        {
            Value = online.ToString("n0"),
            Trend = $"应用 {apps:n0}",
            Url = "/Registry/AppOnline",
        };
    }
}
