using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Gateway;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Gateway.GatewayNode;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关节点。集群中的后端服务器节点</summary>
[Menu(30, true, Icon = "fa-table")]
[GatewayArea]
public class GatewayNodeController : EntityController<GatewayNode>
{
    static GatewayNodeController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as GatewayNode).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as GatewayNode).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public GatewayNodeController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<GatewayNode> Search(Pager p)
    {
        var clusterId = p["clusterId"].ToInt(-1);
        var address = p["address"];
        var isHealthy = p["isHealthy"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return GatewayNode.Search(clusterId, address, isHealthy, enable, start, end, p["Q"], p);
    }
}