using NewLife.Cube.Widgets;
using Stardust.Data.Nodes;

namespace Stardust.Web.Widgets;

/// <summary>在线节点。工作台 KPI 指标，当前在线节点数（NodeOnline 会话表，10分钟不活跃自动清理离线）</summary>
[Widget("OnlineNode", "在线节点", Icon = "fa-server", Cols = 2, Sort = 1, Category = "星尘平台", Color = "green", WidgetType = WidgetTypes.Kpi)]
public class OnlineNodeWidget : IWidget
{
    /// <summary>获取组件数据</summary>
    public Object GetData()
    {
        var online = NodeOnline.Meta.Count;
        var total = Node.Meta.Count;

        return new KpiData
        {
            Value = online.ToString("n0"),
            Trend = $"总节点 {total:n0}",
            Url = "/Nodes/NodeOnline",
        };
    }
}
