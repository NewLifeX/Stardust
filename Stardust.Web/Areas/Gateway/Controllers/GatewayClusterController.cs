using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Gateway;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Gateway.GatewayCluster;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关集群。后端服务器集群，定义了负载均衡策略和健康检查配置</summary>
[Menu(40, true, Icon = "fa-table")]
[GatewayArea]
public class GatewayClusterController : EntityController<GatewayCluster>
{
    static GatewayClusterController()
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
        //    df.DataVisible = e => (e as GatewayCluster).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as GatewayCluster).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public GatewayClusterController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<GatewayCluster> Search(Pager p)
    {
        var projectId = p["projectId"].ToInt(-1);
        var sessionSticky = p["sessionSticky"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return GatewayCluster.Search(projectId, sessionSticky, enable, start, end, p["Q"], p);
    }
}