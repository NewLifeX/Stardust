using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Platform;
using XCode.Membership;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>星系项目。一个星系包含多个星星节点，以及多个尘埃应用，完成产品线的项目管理</summary>
[Menu(100, true, Icon = "fa-table")]
[PlatformArea]
public class GalaxyProjectController : EntityController<GalaxyProject>
{
    static GalaxyProjectController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField();
        ListFields.RemoveRemarkField();

        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/Platform/GalaxyProject/Edit?id={Id}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.GetField("Nodes") as ListField;
            df.DisplayName = "{Nodes}";
            df.Url = "/Nodes/Node?projectId={Id}";
            df.Target = "_blank";
            df.Title = "{Name}（节点）";
        }
        {
            var df = ListFields.GetField("Apps") as ListField;
            df.DisplayName = "{Apps}";
            df.Url = "/Registry/App?projectId={Id}";
            df.Target = "_blank";
            df.Title = "{Name}（应用）";
        }
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var projectId = GetRequest("Id").ToInt(-1);
        if (projectId > 0)
        {
            PageSetting.NavView = "_Project_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<GalaxyProject> Search(Pager p)
    {
        //var deviceId = p["deviceId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (p.Sort.IsNullOrEmpty())
        {
            p.Sort = "Sort";
            p.Desc = true;
        }
        //if (p.OrderBy.IsNullOrEmpty()) p.OrderBy = "sort desc,Id desc";

        return GalaxyProject.Search(start, end, p["Q"], p);
    }
}