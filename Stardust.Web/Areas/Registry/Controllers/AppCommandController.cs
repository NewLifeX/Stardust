using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers;

[Menu(58, false)]
[RegistryArea]
public class AppCommandController : EntityController<AppCommand>
{
    static AppCommandController()
    {
        {
            var df = ListFields.GetField("Command") as ListField;
            df.Url = "/Registry/AppCommand?appId={AppId}&command={Command}";
        }
        ListFields.TraceUrl();
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

    protected override IEnumerable<AppCommand> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var command = p["command"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppCommand.Search(appId, command, start, end, p["Q"], p);
    }
}