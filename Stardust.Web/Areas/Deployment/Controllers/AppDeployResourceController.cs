using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>应用资源。应用部署集引用的共享资源，发布时一并下发到目标节点</summary>
[Menu(0, false, Icon = "fa-table")]
[DeploymentArea]
public class AppDeployResourceController : DeploymentEntityController<AppDeployResource>
{
    static AppDeployResourceController()
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
        //    df.DataVisible = e => (e as AppDeployResource).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as AppDeployResource).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public AppDeployResourceController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<AppDeployResource> Search(Pager p)
    {
        var deployId = p["deployId"].ToInt(-1);
        var resourceId = p["resourceId"].ToInt(-1);
        var autoPublish = p["autoPublish"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppDeployResource.Search(deployId, resourceId, autoPublish, enable, start, end, p["Q"], p);
    }
}