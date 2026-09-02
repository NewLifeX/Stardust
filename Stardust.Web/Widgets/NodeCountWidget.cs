using NewLife.Cube.Widgets;
using Stardust.Data.Nodes;

namespace Stardust.Web.Widgets;

/// <summary>节点总数。工作台 KPI 指标，平台接入节点总量（Node 主表）</summary>
[Widget("NodeCount", "节点总数", Icon = "fa-desktop", Cols = 2, Sort = 2, Category = "星尘平台", Color = "blue", WidgetType = WidgetTypes.Kpi)]
public class NodeCountWidget : IWidget
{
    /// <summary>获取组件数据</summary>
    public Object GetData()
    {
        var total = Node.Meta.Count;
        var online = NodeOnline.Meta.Count;

        return new KpiData
        {
            Value = total.ToString("n0"),
            Trend = $"在线 {online:n0}",
            Url = "/Nodes/Node",
        };
    }
}
