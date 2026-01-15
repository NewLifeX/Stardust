using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(99)]
public class AppController : RegistryEntityController<App>
{
    static AppController()
    {
        LogOnChange = true;

        ListFields.RemoveField("ProjectId", "Secret", "WebHook", "AllowControlNodes", "Remark");
        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?projectId={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/Registry/App/Detail?id={Id}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.AddListField("AppLog", "Version");
            df.DisplayName = "应用日志";
            df.Url = "/Registry/AppLog?appId={Id}";
        }
        {
            var df = ListFields.AddListField("Log", "CreateUser");
            df.DisplayName = "审计日志";
            df.Url = "/Admin/Log?category=应用系统&linkId={Id}";
            df.Target = "_frame";
        }
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("Id").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override IEnumerable<App> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var node = App.FindById(id);
            if (node != null) return [node];
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return App.Search(projectId, category, enable, start, end, p["Q"], p);
    }

    /// <summary>搜索</summary>
    /// <param name="category"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public ActionResult AppSearch(Int32 projectId, String category, String key = null)
    {
        var page = new PageParameter { PageSize = 20 };

        //// 默认排序
        //if (page.Sort.IsNullOrEmpty()) page.Sort = _.Name;

        var list = App.Search(projectId, category, true, DateTime.MinValue, DateTime.MinValue, key, page);

        return Json(0, null, list.Select(e => new
        {
            e.Id,
            e.Name,
            e.DisplayName,
            e.Category,
        }).ToArray());
    }

    /// <summary>启用禁用下线告警</summary>
    /// <param name="enable"></param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SetAlarm(Boolean enable = true)
    {
        foreach (var item in SelectKeys)
        {
            var dt = App.FindById(item.ToInt());
            if (dt != null)
            {
                dt.AlarmOnOffline = enable;
                dt.Save();
            }
        }

        return JsonRefresh("操作成功！");
    }
}