using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Platform;
using XCode.Membership;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>项目用户关系。每个项目拥有哪些用户</summary>
[Menu(10, false, Icon = "fa-table")]
[PlatformArea]
public class ProjectUserController : WebEntityController<ProjectUser>
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

    /// <summary>高级搜索。按项目、用户、启用状态分页查询。未传的编号为 -1，表示不按该列过滤。</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<ProjectUser> Search(Pager p)
    {
        var projectId = p["projectId"].ToInt(-1);
        var userId = p["userId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ProjectUser.Search(projectId, userId, enable, start, end, p["Q"], p);
    }

    /// <summary>插入前确认项目和用户都存在。与 Web 宿主同逻辑。</summary>
    protected override Int32 OnInsert(ProjectUser entity)
    {
        entity.EnsureLink();
        return base.OnInsert(entity);
    }
}
