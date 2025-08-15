using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using static Stardust.Data.Nodes.NodeData;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(60, false)]
[NodesArea]
public class NodeDataController : ReadOnlyEntityController<NodeData>
{
    static NodeDataController() => ListFields.RemoveField("Id");

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var nodeId = GetRequest("nodeId").ToInt(-1);
        if (nodeId > 0)
        {
            PageSetting.NavView = "_Node_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override IEnumerable<NodeData> Search(Pager p)
    {
        PageSetting.EnableAdd = false;

        var nodeId = p["nodeId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (nodeId > 0)
        {
            // 最近10小时
            if (p.PageSize == 20 && nodeId > 0)
            {
                p.PageSize = 24 * 60;

                // 默认查询最近24小时。如果指定了应用，还需要根据节点心跳间隔来调整
                var node = Node.FindByID(nodeId);
                if (node != null && node.Period > 0)
                {
                    p.PageSize = 24 * 3600 / node.Period;
                }
            }

            PageSetting.EnableNavbar = false;

            //if (start.Year < 2000)
            //{
            //    start = DateTime.Today;
            //    p["dtStart"] = start.ToFullString();
            //}
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        var list = NodeData.Search(nodeId, start, end, p["Q"], p);

        if (list.Count > 0)
        {
            // 绘制日期曲线图
            var node = Node.FindByID(nodeId);
            if (nodeId >= 0 && node != null)
            {
                var list2 = list.OrderBy(e => e.LocalTime).ToList();

                var chart = new ECharts
                {
                    Title = new ChartTitle { Text = node.Name },
                    Height = 400,
                };
                chart.SetX(list2, _.LocalTime);
                //chart.SetY("指标");
                chart.YAxis = [
                    new YAxis{ Name = "指标", Type = "value" },
                    new YAxis{ Name = "网络", Type = "value" }
                ];
                chart.AddDataZoom();
                chart.AddLine(list2, _.CpuRate, e => Math.Round(e.CpuRate * 100), true);

                var series = chart.Add(list2, _.AvailableMemory, "line", e => node.Memory == 0 ? 0 : (100 - (e.AvailableMemory * 100 / node.Memory)));
                series.Name = "已用内存";
                series = chart.Add(list2, _.AvailableFreeSpace, "line", e => node.TotalSize == 0 ? 0 : (100 - (e.AvailableFreeSpace * 100 / node.TotalSize)));
                series.Name = "已用磁盘";

                if (list2.Any(e => e.Temperature > 0))
                    chart.AddLine(list2, _.Temperature, e => Math.Round(e.Temperature, 2), true);
                if (list2.Any(e => e.Battery > 0))
                    chart.AddLine(list2, _.Battery, e => Math.Round(e.Battery * 100), true);

                if (list2.Any(e => e.UplinkSpeed > 0))
                {
                    var line = chart.Add(list2, _.UplinkSpeed, "line", e => e.UplinkSpeed / 1000);
                    line.Name = "网络上行";
                    line.YAxisIndex = 1;
                }
                if (list2.Any(e => e.DownlinkSpeed > 0))
                {
                    var line = chart.Add(list2, _.DownlinkSpeed, "line", e => e.DownlinkSpeed / 1000);
                    line.Name = "网络下行";
                    line.YAxisIndex = 1;
                }

                if (list2.Any(e => e.TcpConnections > 0))
                {
                    var line = chart.Add(list2, _.TcpConnections);
                    line.YAxisIndex = 1;
                }
                //if (list2.Any(e => e.TcpTimeWait > 0))
                //{
                //    var line = chart.Add(list2, _.TcpTimeWait);
                //    line.YAxisIndex = 1;
                //}
                //if (list2.Any(e => e.TcpCloseWait > 0))
                //{
                //    var line = chart.Add(list2, _.TcpCloseWait);
                //    line.YAxisIndex = 1;
                //}

                chart.AddLine(list2, _.IntranetScore, e => Math.Round(e.IntranetScore * 100));
                chart.AddLine(list2, _.InternetScore, e => Math.Round(e.InternetScore * 100));

                //chart.Add(list2, _.Offset);
                chart.SetTooltip();
                ViewBag.Charts = new[] { chart };
            }

            if (list.Count > 1000) list = list.Take(1000).ToList();
        }

        return list;
    }
}