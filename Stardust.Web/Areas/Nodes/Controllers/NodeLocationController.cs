using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeLocation;
using System.ComponentModel;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点定位。根据网关IP和MAC规则，自动匹配节点所在地理位置</summary>
[Menu(10, true, Icon = "fa-table")]
[NodesArea]
public class NodeLocationController : EntityController<NodeLocation>
{
    static NodeLocationController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var ss = SearchFields.AddDataField("AreaId") as SearchField;
        //    ss.View = "_Area3";
        //}
        {
            var ff = AddFormFields.GetField("AreaId") as FormField;
            ff.ItemView = "_Area3";
        }
        {
            var ff = EditFormFields.GetField("AreaId") as FormField;
            ff.ItemView = "_Area3";
        }
    }

    //private readonly ITracer _tracer;

    //public NodeLocationController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<NodeLocation> Search(Pager p)
    {
        var name = p["name"];
        var enable = p["enable"]?.ToBoolean();
        var areaId = p["areaId"].ToInt(-1);
        //if (areaId <= 0)
        //{
        //    var areaIds = p["areaId"].SplitAsInt("/");
        //    if (areaIds != null && areaIds.Length > 0) areaId = areaIds[^1];
        //}

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return NodeLocation.Search(name, areaId, enable, start, end, p["Q"], p);
    }

    //protected override Boolean Valid(NodeLocation entity, DataObjectMethodType type, Boolean post)
    //{
    //    if (post && type is DataObjectMethodType.Insert or DataObjectMethodType.Update)
    //    {
    //        var areaIds = GetRequest("areaId").SplitAsInt("/");
    //        if (areaIds != null && areaIds.Length > 0) entity.AreaId = areaIds[^1];
    //    }
    //    if (!post && type is DataObjectMethodType.Update)
    //    {
    //        if (ViewBag.Page is Pager page)
    //        {
    //            page["AreaId"] = entity.AreaId.ToString();
    //        }
    //    }

    //    return base.Valid(entity, type, post);
    //}
}