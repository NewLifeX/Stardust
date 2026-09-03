using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeStat;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点统计。按时间维度统计节点数量、在线率等指标，支持图表和趋势展示</summary>
[Menu(60)]
[NodesArea]
public class NodeStatController : WebReadOnlyEntityController<NodeStat>
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
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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

        // 纯 API 无视图，原 MVC 图表(ECharts)已删除

        return list;
    }
}
