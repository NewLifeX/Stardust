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

        // 自定义页面不会自动带上标题，不设置时浏览器标签页为空
        ViewBag.Title = "链路查询";

        return View();
    }
}