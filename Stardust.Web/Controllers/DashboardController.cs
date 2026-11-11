using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using Stardust.Data.Monitors;
using Stardust.Web.Models.Dashboard;
using Stardust.Web.Services;
using XCode.Membership;

namespace Stardust.Web.Controllers;

/// <summary>星尘看板中心。系统/项目/应用/节点/大屏五个分层视角</summary>
/// <remarks>
/// 看板使用独立布局，不复用魔方主题；数据由 <see cref="DashboardService"/> 服务端装配，
/// 页面 30 秒轮询 <c>/Dashboard/Data</c> 更新 KPI 与图表。
/// 数据范围严格按当前用户角色与项目成员关系过滤，无授权时展示空态。
/// </remarks>
[DisplayName("看板中心")]
[Menu(150, true, Icon = "fa-th-large", LastUpdate = "20261031")]
public class DashboardController(DashboardService service) : ControllerBaseX
{
    private readonly DashboardService _service = service;

    /// <summary>动作执行前。自定义页面不继承实体控制器的鉴权，统一在此要求登录</summary>
    /// <param name="context">动作执行上下文</param>
    /// <remarks>未登录访问一律跳转登录页并回到原始地址；数据接口返回 401，避免前端拿到登录页 HTML</remarks>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        if (ManageProvider.User != null) return;

        var http = context.HttpContext;
        var accept = http.Request.Headers["Accept"] + "";
        if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase) || http.Request.Path.Value?.EndsWith("/Data", StringComparison.OrdinalIgnoreCase) == true)
            context.Result = new UnauthorizedResult();
        else
            context.Result = new RedirectResult($"/Admin/User/Login?r={Uri.EscapeDataString(http.Request.Path + http.Request.QueryString)}");
    }

    /// <summary>看板入口。默认进入系统级看板</summary>
    /// <returns>重定向结果</returns>
    [Route("[controller]")]
    public ActionResult Index() => Redirect("/Dashboard/System");

    /// <summary>站点根路径。直接呈现系统级看板，与 /Dashboard/System 等价</summary>
    /// <param name="range">时间范围。1h/24h/7d</param>
    /// <returns>页面</returns>
    [Route("/")]
    public ActionResult Root(String range) => RenderSystem(range);

    /// <summary>系统级看板</summary>
    /// <param name="range">时间范围。1h/24h/7d</param>
    /// <returns>页面</returns>
    [Route("[controller]/System")]
    public ActionResult SystemBoard(String range) => RenderSystem(range);

    private ActionResult RenderSystem(String range)
    {
        range = NormalizeRange(range);
        var scope = ResolveScope();
        var model = _service.BuildSystem(scope, range);
        Prepare(model, "系统级看板");

        return View("System", model);
    }

    /// <summary>项目看板。未指定项目时为项目选择页</summary>
    /// <param name="id">项目编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>页面</returns>
    [Route("[controller]/Project/{id:int?}")]
    [Route("[controller]/Projects")]
    public ActionResult ProjectBoard(Int32? id, String range)
    {
        range = NormalizeRange(range);
        var scope = ResolveScope();

        if (id == null || id <= 0)
        {
            var picker = _service.BuildProjectPicker(scope);
            Prepare(picker, picker.Title);
            return View("Projects", picker);
        }

        var model = _service.BuildProject(scope, id.Value, range);
        if (model.Project.Id == 0)
        {
            // 无权限或不存在：回到选择页，避免泄露项目是否存在
            var picker = _service.BuildProjectPicker(scope);
            Prepare(picker, picker.Title);
            return View("Projects", picker);
        }
        Prepare(model, model.Project.Name + " · 项目看板", new ProjectOption { Id = model.Project.Id, Name = model.Project.Name });

        return View("Project", model);
    }

    /// <summary>应用看板。未指定应用时为应用选择页</summary>
    /// <param name="id">应用编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>页面</returns>
    [Route("[controller]/App/{id:int?}")]
    [Route("[controller]/Apps")]
    public ActionResult AppBoard(Int32? id, String range)
    {
        range = NormalizeRange(range);
        var scope = ResolveScope();

        if (id == null || id <= 0)
        {
            var picker = _service.BuildAppPicker(scope);
            Prepare(picker, picker.Title);
            return View("Apps", picker);
        }

        var model = _service.BuildApp(scope, id.Value, range);
        if (model.Info.Id == 0)
        {
            var picker = _service.BuildAppPicker(scope);
            Prepare(picker, picker.Title);
            return View("Apps", picker);
        }
        Prepare(model, model.Info.DisplayName.IsNullOrEmpty() ? model.Info.Name : model.Info.DisplayName);

        return View("App", model);
    }

    /// <summary>节点看板。未指定节点时为节点选择页</summary>
    /// <param name="id">节点编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>页面</returns>
    [Route("[controller]/Node/{id:int?}")]
    [Route("[controller]/Nodes")]
    public ActionResult NodeBoard(Int32? id, String range)
    {
        range = NormalizeRange(range);
        var scope = ResolveScope();

        if (id == null || id <= 0)
        {
            var picker = _service.BuildNodePicker(scope);
            Prepare(picker, picker.Title);
            return View("Nodes", picker);
        }

        var model = _service.BuildNode(scope, id.Value, range);
        if (model.Info.Id == 0)
        {
            var picker = _service.BuildNodePicker(scope);
            Prepare(picker, picker.Title);
            return View("Nodes", picker);
        }
        Prepare(model, model.Info.Name);

        return View("Node", model);
    }

    /// <summary>大屏模式</summary>
    /// <param name="range">时间范围</param>
    /// <returns>页面</returns>
    [Route("[controller]/Wallboard")]
    public ActionResult Wallboard(String range)
    {
        range = NormalizeRange(range);
        var scope = ResolveScope();
        var model = _service.BuildWallboard(scope, range);
        Prepare(model, "大屏模式");

        ViewBag.Range = range;
        return View("Wallboard", model);
    }

    /// <summary>看板数据。供页面自动刷新使用，返回与首屏一致的视图模型</summary>
    /// <param name="page">页面标识</param>
    /// <param name="id">对象编号</param>
    /// <param name="range">时间范围</param>
    /// <returns>JSON</returns>
    [Route("[controller]/Data")]
    public ActionResult Data(String page, Int32 id, String range)
    {
        range = NormalizeRange(range);
        var scope = ResolveScope();

        Object model = page.ToLowerInvariant() switch
        {
            "system" => _service.BuildSystem(scope, range),
            "project" => _service.BuildProject(scope, id, range),
            "app" => _service.BuildApp(scope, id, range),
            "node" => _service.BuildNode(scope, id, range),
            "wallboard" => _service.BuildWallboard(scope, range),
            _ => _service.BuildSystem(scope, range),
        };

        Response.Headers["Cache-Control"] = "no-store";
        return Json(model);
    }

    /// <summary>确认报警。记录确认人与时间，不改变报警状态</summary>
    /// <param name="id">报警编号</param>
    /// <returns>JSON</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[controller]/Alarm/Ack")]
    public ActionResult AckAlarm([FromForm] Int64 id)
    {
        var record = AlarmRecord.FindByKey(id);
        if (record == null) return NotFound();
        if (record.Status == AlarmStatuses.Recovered) return StatusCode(400, new { message = "The alarm has already recovered." });

        if (!CanOperateAlarm(record, out var message)) return StatusCode(403, new { message });

        var user = ManageProvider.User?.Name ?? "";
        if (!record.Ack(user)) return StatusCode(500, new { message = "Failed to acknowledge the alarm." });

        _service.ClearCache();
        return Json(new { success = true, ackUser = record.AckUser, ackTime = record.AckTime.ToString("MM-dd HH:mm") });
    }

    /// <summary>静默报警。在截止时间前不发送外部通知</summary>
    /// <param name="id">报警编号</param>
    /// <param name="hours">静默小时数。默认 24 小时，最多 7 天</param>
    /// <returns>JSON</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[controller]/Alarm/Silence")]
    public ActionResult SilenceAlarm([FromForm] Int64 id, [FromForm] Int32 hours = 24)
    {
        var record = AlarmRecord.FindByKey(id);
        if (record == null) return NotFound();
        if (record.Status == AlarmStatuses.Recovered) return StatusCode(400, new { message = "The alarm has already recovered." });

        if (!CanOperateAlarm(record, out var message)) return StatusCode(403, new { message });

        hours = Math.Clamp(hours, 1, 24 * 7);
        if (!record.Silence(hours)) return StatusCode(500, new { message = "Failed to silence the alarm." });

        _service.ClearCache();
        return Json(new { success = true, hours, silenceTime = record.SilenceTime.ToString("MM-dd HH:mm") });
    }

    /// <summary>校验当前用户能否操作指定报警</summary>
    /// <param name="record">报警记录</param>
    /// <param name="message">失败原因</param>
    /// <returns>是否允许</returns>
    private Boolean CanOperateAlarm(AlarmRecord record, out String message)
    {
        message = null;

        if (ManageProvider.User == null)
        {
            message = "Not logged in.";
            return false;
        }

        if (!HasAlarmPermission())
        {
            message = "No permission to operate alarms.";
            return false;
        }

        var scope = ResolveScope();
        if (!_service.CanOperateAlarm(scope, record))
        {
            message = "No permission to operate alarms of this project.";
            return false;
        }

        return true;
    }

    /// <summary>当前用户是否拥有告警操作权限</summary>
    /// <returns>是否允许确认或静默</returns>
    private static Boolean HasAlarmPermission()
    {
        var menu = ManageProvider.Menu?.FindByUrl("/Monitors/AlarmRecord");
        return menu != null && ManageProvider.User is IUser user && user.Has(menu, PermissionFlags.Update);
    }

    /// <summary>当前用户可见范围</summary>
    /// <returns>可见范围</returns>
    private ScopeInfo ResolveScope() => _service.ResolveScope(ManageProvider.User?.ID ?? 0);

    /// <summary>准备看板布局所需的公共视图数据</summary>
    /// <param name="model">看板模型</param>
    /// <param name="title">页面标题</param>
    /// <param name="current">当前项目上下文</param>
    private void Prepare(DashboardPageModel model, String title, ProjectOption current = null)
    {
        ViewBag.Page = model.Page;
        ViewBag.Title = title;
        ViewBag.Range = model.Range;
        ViewBag.Scope = model.Scope;
        ViewBag.ObjectId = model.Id;
        ViewBag.CurrentProject = current;
        ViewBag.CanAlarm = HasAlarmPermission();

        PageSetting.EnableNavbar = false;
    }

    private static String NormalizeRange(String range) => range?.ToLowerInvariant() switch
    {
        "1h" => "1h",
        "7d" => "7d",
        _ => "24h",
    };
}
