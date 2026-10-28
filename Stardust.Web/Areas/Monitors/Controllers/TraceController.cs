using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>链路查询。按追踪编号查看完整调用链</summary>
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