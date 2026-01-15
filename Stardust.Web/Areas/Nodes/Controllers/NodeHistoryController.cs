using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(60, false)]
[NodesArea]
public class NodeHistoryController : NodesEntityController<NodeHistory>
{
    static NodeHistoryController()
    {
        ListFields.RemoveField("ID", "ProvinceName", "Version", "CompileTime");
        ListFields.AddListField("Remark", null, "TraceId");

        ListFields.TraceUrl();

        {
            var df = ListFields.GetField("NodeName") as ListField;
            df.Url = "/Nodes/Node?Id={NodeID}";
            df.Title = "节点: {NodeName}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("Action") as ListField;
            df.Url = "/Nodes/NodeHistory?nodeId={NodeID}&action={Action}";
        }
    }

    protected override IEnumerable<NodeHistory> Search(Pager p)
    {
        var rids = p["areaId"].SplitAsInt("/");
        var provinceId = rids.Length > 0 ? rids[0] : -1;
        var cityId = rids.Length > 1 ? rids[1] : -1;

        var nodeId = p["nodeId"].ToInt(-1);
        var action = p["action"];
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = NodeHistory._.Id.Desc();

        return NodeHistory.Search(nodeId, provinceId, cityId, action, success, start, end, p["Q"], p);
    }
}