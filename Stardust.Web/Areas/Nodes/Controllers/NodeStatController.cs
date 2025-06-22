using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeStat;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(60)]
[NodesArea]
public class NodeStatController : ReadOnlyEntityController<NodeStat>
{
    static NodeStatController()
    {
        ListFields.RemoveField("ID", "LinkItem", "CreateTime", "UpdateTime", "Remark");

        {
            var df = ListFields.GetField("Category") as ListField;
            df.Url = "/Nodes/NodeStat?category={Category}&dtStart={StatDate}&dtEnd={StatDate}";
        }
        {
            var df = ListFields.GetField("Key") as ListField;
            df.Url = "/Nodes/NodeStat?category={Category}&key={Key}";
        }
        {
            var df = ListFields.AddListField("nodes", null, "Total");
            df.DisplayName = "明细";
            df.AddService(new MyUrl());
            df.Target = "_frame";
        }
    }

    //protected override FieldCollection OnGetFields(String kind, NodeStat entity)
    //{
    //    var fields = base.OnGetFields(kind, entity);

    //    if (kind == "List")
    //    {
    //        var category = Request.Query["category"].FirstOrDefault();
    //        if (!category.IsNullOrEmpty() && fields.GetField("Total") is ListField df)
    //        {
    //            df.DisplayName = "{Total}";
    //            df.Url = category switch
    //            {
    //                "产品" => "/Nodes/Node?product={Key}",
    //                "版本" => "/Nodes/Node?version={Key}",
    //                "操作系统" => "/Nodes/Node?osKind={Key}",
    //                "运行时" => "/Nodes/Node?runtime={Key}",
    //                "最高框架" => "/Nodes/Node?framework={Key}",
    //                "城市" => "/Nodes/Node?areaid={Key}",
    //                _ => null,
    //            };
    //        }
    //    }

    //    return fields;
    //}

    class MyUrl : IUrlExtend
    {
        public String Resolve(DataField field, IModel data)
        {
            if (field is ListField df && data is NodeStat st && !st.LinkItem.IsNullOrEmpty())
            {
                //df.DisplayName = "{Total}";
                return st.Category switch
                {
                    "产品" => $"/Nodes/Node?product={st.LinkItem}",
                    "版本" => $"/Nodes/Node?version={st.LinkItem}",
                    "操作系统" => $"/Nodes/Node?osKind={st.LinkItem}",
                    "运行时" => $"/Nodes/Node?runtime={st.LinkItem}",
                    "最高框架" => $"/Nodes/Node?framework={st.LinkItem}",
                    "城市" => $"/Nodes/Node?areaid={st.LinkItem}",
                    "芯片架构" => $"/Nodes/Node?arch={st.LinkItem}",
                    "项目" => $"/Nodes/Node?projectId={st.LinkItem}",
                    _ => $"/Nodes/Node?q={st.LinkItem}",
                };
            }

            return null;
        }
    }

    protected override IEnumerable<NodeStat> Search(Pager p)
    {
        var category = p["category"];
        var key = p["key"];
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        // 默认分类
        if (category.IsNullOrEmpty()) p["category"] = category = "操作系统";

        // 带有分类没有key也没有日期时，显示今天
        if (!category.IsNullOrEmpty() && key.IsNullOrEmpty() && start.Year < 2000)
        {
            start = end = DateTime.Today;
            p["dtStart"] = start.ToString("yyyy-MM-dd");
            p["dtEnd"] = end.ToString("yyyy-MM-dd");

            p.Sort = __.ActivesT30;
            p.Desc = true;
            p.PageSize = 100;
        }

        // 默认排序
        if (!category.IsNullOrEmpty() && !key.IsNullOrEmpty() && start.Year < 2000 && p.Sort.IsNullOrEmpty())
        {
            start = DateTime.Today.AddDays(-30);
            p["dtStart"] = start.ToString("yyyy-MM-dd");

            p.Sort = __.StatDate;
            p.Desc = true;
            p.PageSize = 100;
        }
        else if (!category.IsNullOrEmpty() && start.Year > 2000)
        {
            if (p.PageSize == 20) p.PageSize = 100;
        }

        p.RetrieveState = true;

        var list = NodeStat.Search(category, key, start, end, p["Q"], p);

        // 选定分类和统计项，显示曲线图
        if (!category.IsNullOrEmpty() && !key.IsNullOrEmpty() && list.Count > 0)
        {
            var list2 = list.OrderBy(e => e.StatDate).ToList();
            var chart = new ECharts { Height = 400, };
            chart.SetX(list2, _.StatDate);
            chart.YAxis = [
                    new YAxis{ Name = "数值", Type = "value" },
                    new YAxis{ Name = "总数", Type = "value" }
                ];
            chart.AddDataZoom();

            var line = chart.AddLine(list2, _.Total, null, true);
            line.YAxisIndex = 1;

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
            // 饼图不要显示空的统计项
            var list2 = list.Where(e => !e.Key.IsNullOrEmpty() && e.Key != "0").ToList();

            var chart = new ECharts { Height = 400 };
            chart.AddPie(list2, _.Total, e => new NameValue(e.Key, e.Total));

            //list2 = list.OrderByDescending(e => e.ActivesT7).ToList();
            var chart2 = new ECharts { Height = 400 };
            chart2.AddPie(list2, _.ActivesT30, e => new NameValue(e.Key, e.ActivesT30));

            //list2 = list.OrderByDescending(e => e.NewsT7).ToList();
            var chart3 = new ECharts { Height = 400 };
            chart3.AddPie(list, _.NewsT7, e => new NameValue(e.Key, e.NewsT7));

            ViewBag.Charts = new[] { chart2 };
            ViewBag.Charts2 = new[] { chart };
        }

        return list;
    }
}