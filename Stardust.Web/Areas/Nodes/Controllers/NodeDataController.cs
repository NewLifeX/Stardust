using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using static Stardust.Data.Nodes.NodeData;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeDataController : ReadOnlyEntityController<NodeData>
    {
        static NodeDataController()
        {
            MenuOrder = 60;

            ListFields.RemoveField("Id");
        }

        protected override IEnumerable<NodeData> Search(Pager p)
        {
            PageSetting.EnableAdd = false;

            var nodeId = p["nodeId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (nodeId > 0)
            {
                // 最近24小时
                if (p.PageSize == 20 && nodeId > 0) p.PageSize = 600;

                PageSetting.EnableNavbar = false;
            }

            if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.ID.Desc();

            var list = NodeData.Search(nodeId, start, end, p["Q"], p);

            if (list.Count > 0)
            {
                var hasDate = start.Year > 2000 || end.Year > 2000;
                // 绘制日期曲线图
                var node = Node.FindByID(nodeId);
                if (nodeId >= 0 && node != null)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = node.Name + " @ " + list[0].LocalTime.ToFullString() },
                        Height = 400,
                    };
                    chart.SetX(list, _.LocalTime, e => e.LocalTime.ToString("HH:mm"));
                    chart.SetY("指标");
                    chart.AddLine(list, _.CpuRate, e => (Int32)(e.CpuRate * 100), true);
                    chart.Add(list, _.AvailableMemory, "line", e => node.Memory == 0 ? 0 : (e.AvailableMemory * 100 / node.Memory));
                    chart.Add(list, _.AvailableFreeSpace, "line", e => node.TotalSize == 0 ? 0 : (e.AvailableFreeSpace * 100 / node.TotalSize));
                    chart.Add(list, _.Temperature);
                    chart.Add(list, _.Delay);
                    chart.Add(list, _.Offset);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
            }

            return list;
        }
    }
}