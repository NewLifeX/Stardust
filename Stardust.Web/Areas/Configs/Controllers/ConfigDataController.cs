using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data.Configs;
using XCode;

namespace Stardust.Web.Areas.Configs.Controllers;

[Menu(0, false)]
[ConfigsArea]
public class ConfigDataController : EntityController<ConfigData>
{
    static ConfigDataController()
    {
        ListFields.AddDataField("Value", null, "Scope");
        ListFields.AddDataField("NewValue", null, "NewStatus");
        ListFields.RemoveField("Remark");
        ListFields.RemoveField("CreateIP", "UpdateIP");

        AddFormFields.RemoveCreateField();
        AddFormFields.RemoveUpdateField();
        AddFormFields.RemoveField("Version", "NewVersion", "NewValue", "NewStatus");

        EditFormFields.RemoveCreateField();
        EditFormFields.RemoveUpdateField();
        EditFormFields.RemoveField("Version", "NewVersion");

        {
            var df = EditFormFields.GetField("Value");
            df.ReadOnly = true;
        }
    }

    private readonly StarFactory _starFactory;
    private readonly ITracer _tracer;

    public ConfigDataController(StarFactory starFactory, ITracer tracer)
    {
        _starFactory = starFactory;
        _tracer = tracer;
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

        //PageSetting.EnableAdd = false;
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

    protected override IEnumerable<ConfigData> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var name = p["key"];
        var scope = p["scope"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        PageSetting.EnableSelect = false;

        // 如果选择了应用，特殊处理版本
        if (appId > 0 && p.PageSize == 20) p.PageSize = 500;

        var list = ConfigData.Search(appId, name, scope, start, end, p["Q"], p);

        PageSetting.EnableAdd = appId > 0;
        if (appId > 0)
        {
            //PageSetting.EnableAdd = true;

            // 控制发布按钮
            var app = AppConfig.FindById(appId);
            PageSetting.EnableSelect = list.Any(e => e.NewVersion > app.Version);
        }

        return list;
    }

    public override async Task<ActionResult> Add(ConfigData entity)
    {
        entity.NewVersion = entity.App.AcquireNewVersion();
        await base.Add(entity);

        return RedirectToAction("Index", new { appId = entity.AppId });
    }

    //public override ActionResult Edit(ConfigData entity)
    //{
    //    base.Edit(entity);

    //    return RedirectToAction("Index", new { appId = entity.AppId });
    //}

    protected override Int32 OnUpdate(ConfigData entity)
    {
        var e = entity as IEntity;
        if (e.HasDirty)
        {
            // 在用版本禁止修改，未发布的新版本可以
            if (entity.Version > 0)
            {
                if (e.Dirtys[nameof(entity.Key)]) throw new ArgumentException("禁止修改名称，建议新增配置", nameof(entity.Key));
                if (e.Dirtys[nameof(entity.Scope)]) throw new ArgumentException("禁止修改作用域，建议新增配置", nameof(entity.Scope));
                if (e.Dirtys[nameof(entity.Value)]) throw new ArgumentException("禁止修改正在使用的数值！", nameof(entity.Value));
            }

            var ver = entity.App.AcquireNewVersion();
            entity.NewVersion = ver;
        }
        if (e.Dirtys[nameof(entity.Enable)])
        {
            // 在用版本禁止修改，未发布的新版本可以
            if (entity.Version > 0)
            {
                // 禁用启用修改为期望值，发布后才能执行
                entity.NewStatus = entity.Enable ? ConfigData.ENABLED : ConfigData.DISABLED;
                entity.Enable = !entity.Enable;
            }
        }

        return base.OnUpdate(entity);
    }

    protected override Int32 OnDelete(ConfigData entity)
    {
        // 在用版本禁止修改，未发布的新版本可以
        if (entity.Version == 0) return base.OnDelete(entity);

        // 删除操作，直接修改为即将被删除
        var ver = entity.App.AcquireNewVersion();
        entity.Version = ver;
        entity.NewStatus = ConfigData.DELETED;

        return base.OnUpdate(entity);
    }

    public async Task<ActionResult> Publish(Int32 appId)
    {
        using var span = _tracer?.NewSpan(nameof(Publish), appId + "");
        try
        {
            var app = AppConfig.FindById(appId);
            if (app == null) throw new ArgumentNullException(nameof(appId));

            if (app.Version >= app.NextVersion) throw new ApiException(701, "已经是最新版本！");
            app.Publish();

            await _starFactory.SendAppCommand(app.Name, "config/publish", "");

            return JsonRefresh("发布成功！", 3);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            return Json(0, ex.Message, ex);
        }
    }
}