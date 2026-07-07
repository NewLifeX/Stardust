using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Gateway;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Gateway.GatewayCert;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关证书。SSL证书配置，用于HTTPS终止和SNI匹配</summary>
[Menu(10, true, Icon = "fa-lock")]
[GatewayArea]
public class GatewayCertController : EntityController<GatewayCert>
{
    static GatewayCertController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));

        SearchFields.AddField("Domain");
        SearchFields.AddField("Enable");
        SearchFields.AddField("CreateTime");
    }

    protected override IEnumerable<GatewayCert> Search(Pager p)
    {
        var domain = p["domain"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return GatewayCert.Search(domain, enable, start, end, p["Q"], p);
    }
}