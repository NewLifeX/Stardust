using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Deployment.SslCertificate;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>SSL证书。HTTPS证书管理，支持多域名、自动续期、多格式导出</summary>
[Menu(10, true, Icon = "fa-table")]
[DeploymentArea]
public class SslCertificateController : EntityController<SslCertificate>
{
    static SslCertificateController()
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
        //    df.DataVisible = e => (e as SslCertificate).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as SslCertificate).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public SslCertificateController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<SslCertificate> Search(Pager p)
    {
        var autoRenew = p["autoRenew"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return SslCertificate.Search(autoRenew, enable, start, end, p["Q"], p);
    }
}