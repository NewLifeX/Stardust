using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace Stardust.Web.Areas.Gateway.Controllers;

/// <summary>网关证书。SSL证书配置，统一使用星尘部署中心的 SslCertificate 管理</summary>
[Menu(10, true, Icon = "fa-lock")]
[GatewayArea]
public class GatewayCertController : EntityController<SslCertificate>
{
    static GatewayCertController()
    {
        ListFields.RemoveCreateField().RemoveUpdateField().RemoveRemarkField();
        ListFields.RemoveField("PfxPassword", "Issuer", "Subject", "NotBefore", "NotAfter", "Thumbprint",
            "AutoRenew", "Provider", "RenewDays", "LastRenew",
            "CreateUserId", "CreateIP", "UpdateUserId", "UpdateIP");

        AddFormFields.RemoveField("Id".Split(","));
        EditFormFields.RemoveField("Id".Split(","));

        SearchFields.AddField("Domain");
        SearchFields.AddField("Enable");
        SearchFields.AddField("CreateTime");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<SslCertificate> Search(Pager p)
    {
        var domain = p["domain"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return SslCertificate.Search(null, enable, start, end, p["Q"], p);
    }
}
