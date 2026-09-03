using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Gateway;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Gateway.GatewayNode;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关节点。集群中的后端服务器节点</summary>
[Menu(30, true, Icon = "fa-server")]
[GatewayArea]
public class GatewayNodeController : EntityController<GatewayNode>
{
    static GatewayNodeController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();
        (ListFields.GetField("IsHealthy") as ListField).Header = "健康状态";

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));

        SearchFields.AddField("ClusterId");
        SearchFields.AddField("IsHealthy");
        SearchFields.AddField("Enable");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
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
