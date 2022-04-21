using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeStat;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [Menu(55)]
    [NodesArea]
    public class NodeStatController : ReadOnlyEntityController<NodeStat>
    {
        protected override IEnumerable<NodeStat> Search(Pager p)
        {
            var areaId = p["areaId"].ToInt(-1);
            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            // 默认排序
            if (areaId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
            {
                start = DateTime.Today.AddDays(-30);
                p["dtStart"] = start.ToString("yyyy-MM-dd");

                p.Sort = __.StatDate;
                p.Desc = false;
                p.PageSize = 100;

                //// 默认全国
                //if (areaId < 0) areaId = 0;
            }

            var list = NodeStat.Search(areaId, start, end, p["Q"], p);

            if (list.Count > 0)
            {
                var hasDate = start.Year > 2000 || end.Year > 2000;
                // 绘制日期曲线图
                var ar = Area.FindByID(areaId);
                if (areaId >= 0)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = ar + "" },
                        Height = 400,
                    };
                    chart.SetX(list, _.StatDate, e => e.StatDate.ToString("MM-dd"));
                    chart.SetY("数量");
                    chart.AddLine(list, _.Total, null, true);
                    chart.Add(list, _.Actives);
                    chart.Add(list, _.T7Actives);
                    chart.Add(list, _.T30Actives);
                    chart.Add(list, _.News);
                    chart.Add(list, _.T7News);
                    chart.Add(list, _.T30News);
                    chart.Add(list, _.Registers);
                    chart.Add(list, _.MaxOnline);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
                // 指定日期后，绘制饼图
                if (hasDate && areaId < 0)
                {
                    var w = 400;
                    var h = 300;

                    var chart0 = new ECharts { Width = w, Height = h };
                    chart0.Add(list, _.Total, "pie", e => new { name = e.ProvinceName, value = e.Total });

                    var chart1 = new ECharts { Width = w, Height = h };
                    chart1.Add(list, _.Actives, "pie", e => new { name = e.ProvinceName, value = e.Actives });

                    var chart2 = new ECharts { Width = w, Height = h };
                    chart2.Add(list, _.News, "pie", e => new { name = e.ProvinceName, value = e.News });

                    var chart3 = new ECharts { Width = w, Height = h };
                    chart3.Add(list, _.Registers, "pie", e => new { name = e.ProvinceName, value = e.Registers });

                    var chart4 = new ECharts { Width = w, Height = h };
                    chart4.Add(list, _.MaxOnline, "pie", e => new { name = e.ProvinceName, value = e.MaxOnline });

                    ViewBag.Charts2 = new[] { chart0, chart1, chart2, chart3, chart4 };
                }
            }

            return list;
        }
    }
}