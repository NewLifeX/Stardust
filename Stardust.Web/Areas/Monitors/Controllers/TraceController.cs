using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

[DisplayName("链路查询")]
[Menu(10, true)]
[MonitorsArea]
public class TraceController : ControllerBaseX
{
    public ActionResult Index()
    {
        PageSetting.EnableNavbar = false;

        return View();
    }
}