using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Nodes;
using XCode;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点历史。记录节点登录/登出/升级等关键操作事件，支持按节点和操作类型检索</summary>
[Menu(60, false)]
[NodesArea]
public class NodeHistoryController : NodesEntityController<NodeHistory>
{
    static NodeHistoryController()
    {
        ListFields.RemoveField("ID", "ProvinceName", "Version", "CompileTime");
        ListFields.AddListField("Remark", null, "TraceId");

        ListFields.LocalizedTraceUrl();

        {
            var df = ListFields.GetField("NodeName") as ListField;
            df.Url = "/Nodes/Node?Id={NodeID}";
            df.Title = "节点: {NodeName}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("Action") as ListField;
            df.Url = "/Nodes/NodeHistory?nodeId={NodeID}&action={Action}";
            // 数据库只存英文规范键，列表按当前界面语言显示；链接仍使用原始值，保证筛选有效
            df.GetValue = e => e is NodeHistory entity ? HistoryAction.Localize(entity.Action) : null;
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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

    /// <summary>查看。详情中的操作名同样按当前界面语言显示</summary>
    /// <param name="id">编号</param>
    /// <returns></returns>
    public override ActionResult Detail(String id)
    {
        var rs = base.Detail(id);
        if (rs is ViewResult view && view.Model is NodeHistory entity)
        {
            // 克隆后再翻译，避免改动单对象缓存中的实体
            var clone = entity.CloneEntity();
            clone.Action = HistoryAction.Localize(clone.Action);
            view.ViewData.Model = clone;
        }

        return rs;
    }
}
