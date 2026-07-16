using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Gateway;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Gateway.GatewayCluster;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关集群。后端服务器集群，定义了负载均衡策略和健康检查配置</summary>
[Menu(40, true, Icon = "fa-cubes")]
[GatewayArea]
public class GatewayClusterController : EntityController<GatewayCluster>
{
    static GatewayClusterController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();
        ListFields.AddField("ActiveNodes");

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));

        SearchFields.AddField("LoadBalance");
        SearchFields.AddField("SessionSticky");
        SearchFields.AddField("Enable");
        SearchFields.AddField("CreateTime");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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
