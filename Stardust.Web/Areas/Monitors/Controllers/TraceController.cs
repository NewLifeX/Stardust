using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>链路查询。按追踪编号查看完整调用链</summary>
/// <remarks>自定义页面不会自动带上实体控制器的鉴权，必须显式标注，否则匿名用户可直接访问</remarks>
[DisplayName("链路查询")]
[Menu(10, true)]
[MonitorsArea]
[EntityAuthorize]
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