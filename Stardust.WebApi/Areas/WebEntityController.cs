using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using XCode;

namespace Stardust.Web.Areas;

/// <summary>星尘业务控制器基类（可写）。提供 MVC 版兼容的 JsonRefresh 与统一的导航/字段裁剪。</summary>
public class WebEntityController<T> : EntityController<T> where T : Entity<T>, new()
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        WebEntityCompat.ApplyNav(PageSetting, name => GetRequest(name));
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        return WebEntityCompat.TrimFields(fields, kind, name => GetRequest(name));
    }

    /// <summary>Json 刷新响应（消息重载）。</summary>
    protected virtual ActionResult JsonRefresh(String message, Int32 delay = 1) => WebEntityCompat.JsonRefresh(message);

    /// <summary>Json 刷新响应（数据重载）。</summary>
    protected virtual ActionResult JsonRefresh(Object data) => WebEntityCompat.JsonRefresh(data);
}

/// <summary>
/// 星尘只读业务控制器基类。与 Web 版对齐：统计/历史类表（TraceData、NodeStat、AppMinuteStat 等）
/// 在 MVC 版继承 ReadOnlyEntityController，不提供 Add/Edit/Delete 端点，API 版必须保持同样的只读边界。
/// 本基类在只读语义之上补充本地 NewLife.Cube(API版) 缺失的 JsonRefresh 兼容方法。
/// </summary>
public class WebReadOnlyEntityController<T> : ReadOnlyEntityController<T> where T : Entity<T>, new()
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        WebEntityCompat.ApplyNav(PageSetting, name => GetRequest(name));
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        return WebEntityCompat.TrimFields(fields, kind, name => GetRequest(name));
    }

    /// <summary>Json 刷新响应（消息重载）。</summary>
    protected virtual ActionResult JsonRefresh(String message, Int32 delay = 1) => WebEntityCompat.JsonRefresh(message);

    /// <summary>Json 刷新响应（数据重载）。</summary>
    protected virtual ActionResult JsonRefresh(Object data) => WebEntityCompat.JsonRefresh(data);
}

/// <summary>可写/只读两个基类共用的兼容逻辑，避免重复实现。</summary>
static class WebEntityCompat
{
    /// <summary>按请求参数设置导航视图，与 MVC 版 OnActionExecuting 保持一致。</summary>
    public static void ApplyNav(PageSetting setting, Func<String, String> getRequest)
    {
        var appId = getRequest("appId").ToInt(-1);
        if (appId <= 0) appId = getRequest("configId").ToInt(-1);
        if (appId <= 0) appId = getRequest("deployId").ToInt(-1);
        if (appId <= 0) appId = getRequest("monitorId").ToInt(-1);
        if (appId > 0)
        {
            setting.NavView = "_App_Nav";
            setting.EnableNavbar = false;
        }

        var projectId = getRequest("projectId").ToInt(-1);
        if (projectId > 0)
        {
            setting.NavView = "_Project_Nav";
            setting.EnableNavbar = false;
        }

        var nodeId = getRequest("nodeId").ToInt(-1);
        if (nodeId > 0)
        {
            setting.NavView = "_Node_Nav";
            setting.EnableNavbar = false;
        }
    }

    /// <summary>列表场景下裁剪冗余外键列，与 MVC 版 OnGetFields 保持一致。</summary>
    public static FieldCollection TrimFields(FieldCollection fields, ViewKinds kind, Func<String, String> getRequest)
    {
        if (kind == ViewKinds.List)
        {
            if (getRequest("appId").ToInt(-1) > 0) fields.RemoveField("ProjectName", "AppName", "Category");

            if (getRequest("projectId").ToInt(-1) > 0) fields.RemoveField("ProjectName");

            if (getRequest("deployId").ToInt(-1) > 0) fields.RemoveField("DeployName");

            if (getRequest("configId").ToInt(-1) > 0) fields.RemoveField("ConfigName");

            if (getRequest("nodeId").ToInt(-1) > 0) fields.RemoveField("NodeName");
        }

        return fields;
    }

    /// <summary>
    /// Json 刷新响应。兼容 MVC 版 NewLife.Cube.Core 的 EntityController.JsonRefresh，
    /// 本地 NewLife.Cube(API版) 未提供该方法，故在星尘基类统一补充。
    /// 纯 API 场景下等价于返回统一响应 {code:0, message} / {code:0, data}。
    /// </summary>
    public static ActionResult JsonRefresh(String message, Int32 delay = 1) =>
        new JsonResult(new ApiResponse<Object> { Code = 0, Message = message });

    /// <summary>Json 刷新响应（数据重载）。</summary>
    public static ActionResult JsonRefresh(Object data) =>
        new JsonResult(new ApiResponse<Object> { Code = 0, Data = data });
}
