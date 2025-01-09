using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Platform;
using XCode.Membership;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>项目用户关系。每个项目拥有哪些用户</summary>
[Menu(10, false, Icon = "fa-table")]
[PlatformArea]
public class ProjectUserController : EntityController<ProjectUser>
{
    static ProjectUserController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as ProjectUser).Devices > 0;
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as ProjectUser).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public ProjectUserController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<ProjectUser> Search(Pager p)
    {
        //var deviceId = p["deviceId"].ToInt(-1);
        //var enable = p["enable"]?.Boolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ProjectUser.Search(start, end, p["Q"], p);
    }
}