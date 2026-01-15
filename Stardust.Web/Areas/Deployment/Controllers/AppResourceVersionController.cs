using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Deployment.AppResourceVersion;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>资源版本。资源的多个版本，支持不同运行时平台</summary>
[Menu(30, true, Icon = "fa-table")]
[DeploymentArea]
public class AppResourceVersionController : EntityController<AppResourceVersion>
{
    static AppResourceVersionController()
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
        //    df.DataVisible = e => (e as AppResourceVersion).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as AppResourceVersion).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public AppResourceVersionController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<AppResourceVersion> Search(Pager p)
    {
        var resourceId = p["resourceId"].ToInt(-1);
        var os = (Stardust.Models.OSKind)p["os"].ToInt(-1);
        var arch = (Stardust.Models.CpuArch)p["arch"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppResourceVersion.Search(resourceId, os, arch, enable, start, end, p["Q"], p);
    }
}