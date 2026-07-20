using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Gateway;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Gateway.GatewayRoute;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关路由。请求匹配规则，定义如何将请求转发到目标集群</summary>
[Menu(20, true, Icon = "fa-route")]
[GatewayArea]
public class GatewayRouteController : EntityController<GatewayRoute>
{
    static GatewayRouteController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();
        (ListFields.GetField("Domain") as ListField).Header = "域名";
        (ListFields.GetField("Path") as ListField).Header = "路径";

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));

        SearchFields.AddField("Domain");
        SearchFields.AddField("ClusterId");
        SearchFields.AddField("Enable");
        SearchFields.AddField("Priority");
        SearchFields.AddField("CreateTime");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<GatewayRoute> Search(Pager p)
    {
        var projectId = p["projectId"].ToInt(-1);
        var priority = p["priority"].ToInt(-1);
        var clusterId = p["clusterId"].ToInt(-1);
        var domain = p["domain"];
        var stripPrefix = p["stripPrefix"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return GatewayRoute.Search(projectId, priority, clusterId, domain, stripPrefix, enable, start, end, p["Q"], p);
    }
}
