using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers;

[Menu(58, false)]
[NodesArea]
public class NodeCommandController : EntityController<NodeCommand>
{
    static NodeCommandController()
    {
        ListFields.RemoveField("StartTime", "Expire", "UpdateUserId");
        ListFields.RemoveCreateField();
        //ListFields.AddListField("StartTime", null, "Result");
        ListFields.AddListField("Expire", null, "Result");
        ListFields.AddListField("CreateUser", "UpdateTime");

        {
            var df = ListFields.GetField("Command") as ListField;
            df.Url = "/Nodes/NodeCommand?nodeId={NodeId}&command={Command}";
        }
        ListFields.TraceUrl();
    }

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

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var nodeId = GetRequest("nodeId").ToInt(-1);
            if (nodeId > 0) fields.RemoveField("NodeName");
        }

        return fields;
    }

    protected override IEnumerable<NodeCommand> Search(Pager p)
    {
        var nodeId = p["nodeId"].ToInt(-1);
        var command = p["command"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return NodeCommand.Search(nodeId, command, start, end, p["Q"], p);
    }
}