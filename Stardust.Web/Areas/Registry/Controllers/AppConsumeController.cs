using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(0, false)]
public class AppConsumeController : EntityController<AppConsume>
{

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }

        PageSetting.EnableAdd = false;
    }

    protected override IEnumerable<AppConsume> Search(Pager p)
    {
        //PageSetting.EnableAdd = false;
        //PageSetting.EnableNavbar = false;

        var appId = p["appId"].ToInt(-1);
        var serviceId = p["serviceId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        return AppConsume.Search(appId, serviceId, enable, p["Q"], p);
    }
}