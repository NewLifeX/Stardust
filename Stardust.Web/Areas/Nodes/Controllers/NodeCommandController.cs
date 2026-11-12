using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode.Membership;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点指令。下发到节点的命令记录</summary>
[Menu(58, false)]
[NodesArea]
public class NodeCommandController : EntityController<NodeCommand>
{
    private readonly StarFactory _starFactory;

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
        ListFields.LocalizedTraceUrl();
    }

    public NodeCommandController(StarFactory starFactory) => _starFactory = starFactory;

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

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<NodeCommand> Search(Pager p)
    {
        var nodeId = p["nodeId"].ToInt(-1);
        var command = p["command"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return NodeCommand.Search(nodeId, command, start, end, p["Q"], p);
    }

    /// <summary>下发命令。校验参数后经 StarFactory 发送命令给在线节点</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [DisplayName("下发命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Send([FromBody] NodeCommandSendModel model)
    {
        var nodeId = model?.NodeId ?? 0;
        var command = model?.Command;

        if (nodeId <= 0) return Json(400, "节点编号无效");
        if (command.IsNullOrEmpty()) return Json(400, "命令不能为空");

        var node = Node.FindByID(nodeId);
        if (node == null) return Json(400, $"节点不存在（{nodeId}）");

        try
        {
            var reply = await _starFactory.SendNodeCommandAsync(node.Code, command, null, 0, 3600, 5, HttpContext.RequestAborted);
            if (reply == null) return Json(500, "命令下发超时或连接不可用");
        }
        catch (Exception ex)
        {
            return Json(500, ex.Message);
        }

        return JsonRefresh($"指令已下发：{command}");
    }
}
