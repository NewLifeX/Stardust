using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeStat;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(55)]
[NodesArea]
public class NodeStatController : ReadOnlyEntityController<NodeStat>
{
    static NodeStatController()
    {
        ListFields.RemoveField("ID", "CreateTime", "UpdateTime", "Remark");

        {
            var df = ListFields.GetField("Category") as ListField;
            df.Url = "/Nodes/NodeStat?category={Category}&dtStart={StatDate}&dtEnd={StatDate}";
        }
        {
            var df = ListFields.GetField("Key") as ListField;
            df.Url = "/Nodes/NodeStat?category={Category}&key={Key}";
        }
    }

    protected override IEnumerable<NodeStat> Search(Pager p)
    {
        var category = p["category"];
        var key = p["key"];
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        // 默认排序
        if (!category.IsNullOrEmpty() && !key.IsNullOrEmpty() && start.Year < 2000 && p.Sort.IsNullOrEmpty())
        {
            start = DateTime.Today.AddDays(-30);
            p["dtStart"] = start.ToString("yyyy-MM-dd");

            p.Sort = __.StatDate;
            p.Desc = false;
            p.PageSize = 100;
        }

        var list = NodeStat.Search(category, key, start, end, p["Q"], p);

        // 选定分类和统计项，显示曲线图
        if (!category.IsNullOrEmpty() && !key.IsNullOrEmpty() && list.Count > 0)
        {
            var list2 = list.OrderBy(e => e.StatDate).ToList();
            var chart = new ECharts { Height = 400, };
            chart.SetX(list2, _.StatDate);
            chart.YAxis = new[] {
                    new { name = "数值", type = "value" },
                    new { name = "总数", type = "value" }
                };
            chart.AddDataZoom();

            var line = chart.AddLine(list2, _.Total, null, true);
            line["yAxisIndex"] = 1;

            chart.Add(list2, _.Actives);
            chart.Add(list2, _.ActivesT7);
            chart.Add(list2, _.ActivesT30);
            chart.Add(list2, _.News);
            chart.Add(list2, _.NewsT7);
            chart.Add(list2, _.NewsT30);
            chart.SetTooltip();

            ViewBag.Charts = new[] { chart };
        }
        // 选定分类和日期，显示饼图
        else if (!category.IsNullOrEmpty() && key.IsNullOrEmpty()
            && start.Year > 2000 && start.Date == end.Date && list.Count > 0)
        {
            //var list2 = list.OrderByDescending(e => e.Total).ToList();
            var chart = new ECharts { Height = 400 };
            chart.AddPie(list, _.Total, e => new NameValue(e.Key, e.Total));

            //list2 = list.OrderByDescending(e => e.ActivesT7).ToList();
            var chart2 = new ECharts { Height = 400 };
            chart2.AddPie(list, _.ActivesT7, e => new NameValue(e.Key, e.ActivesT7));

            //list2 = list.OrderByDescending(e => e.NewsT7).ToList();
            var chart3 = new ECharts { Height = 400 };
            chart3.AddPie(list, _.NewsT7, e => new NameValue(e.Key, e.NewsT7));

            ViewBag.Charts = new[] { chart2 };
            ViewBag.Charts2 = new[] { chart };
        }

        return list;
    }
}