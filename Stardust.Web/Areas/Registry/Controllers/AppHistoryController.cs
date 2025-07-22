using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(0, false)]
public class AppHistoryController : ReadOnlyEntityController<AppHistory>
{
    static AppHistoryController()
    {
        ListFields.RemoveField("Id", "Version");
        ListFields.AddListField("Remark", null, "TraceId");

        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is AppHistory entity && !entity.TraceId.IsNullOrEmpty();
        //}
        ListFields.TraceUrl();

        {
            var df = ListFields.GetField("AppName") as ListField;
            df.Url = "/Registry/App?Id={AppId}";
            df.Title = "应用: {AppName}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("Client") as ListField;
            df.Url = "/Registry/AppHistory?appId={AppId}&client={Client}";
        }

        {
            var df = ListFields.GetField("Action") as ListField;
            df.Url = "/Registry/AppHistory?appId={AppId}&action={Action}";
        }
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var appId = GetRequest("appId").ToInt(-1);
            if (appId > 0) fields.RemoveField("AppName");
        }

        return fields;
    }

    protected override IEnumerable<AppHistory> SearchData(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        if (appId >= 0) PageSetting.OrderByKey = true;

        return base.SearchData(p);
    }

    protected override IEnumerable<AppHistory> Search(Pager p)
    {
        //PageSetting.EnableAdd = false;
        //PageSetting.EnableNavbar = false;

        var appId = p["appId"].ToInt(-1);
        var client = p["client"];
        var action = p["action"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        //if (appId > 0 && start.Year < 2000) p["dtStart"] = (start = DateTime.Today).ToString("yyyy-MM-dd");

        return AppHistory.Search(appId, client, action, start, end, p["Q"], p);
    }
}