using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Configs;
using Stardust.Server.Services;
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

    private readonly ConfigService _configService;
    private readonly StarFactory _starFactory;
    private readonly ITracer _tracer;

    public ConfigDataController(ConfigService configService, StarFactory starFactory, ITracer tracer)
    {
        _configService = configService;
        _starFactory = starFactory;
        _tracer = tracer;
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        var configId = GetRequest("configId").ToInt(-1);
        if (configId > 0 || appId > 0)
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
            var configId = GetRequest("configId").ToInt(-1);
            if (configId > 0) fields.RemoveField("ConfigName");
        }

        return fields;
    }

    protected override IEnumerable<ConfigData> Search(Pager p)
    {
        var configId = p["configId"].ToInt(-1);
        var name = p["key"];
        var scope = p["scope"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        PageSetting.EnableSelect = false;

        // 如果选择了应用，特殊处理版本
        if (configId > 0 && p.PageSize == 20) p.PageSize = 500;

        var list = ConfigData.Search(configId, name, scope, start, end, p["Q"], p);

        PageSetting.EnableAdd = configId > 0;
        if (configId > 0)
        {
            //PageSetting.EnableAdd = true;

            // 控制发布按钮
            var app = AppConfig.FindById(configId);
            PageSetting.EnableSelect = list.Any(e => e.NewVersion > app.Version);
        }

        return list;
    }

    public override async Task<ActionResult> Add(ConfigData entity)
    {
        entity.NewVersion = entity.Config.AcquireNewVersion();
        await base.Add(entity);

        return RedirectToAction("Index", new { appId = entity.ConfigId });
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

            var ver = entity.Config.AcquireNewVersion();
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
        var ver = entity.Config.AcquireNewVersion();
        entity.Version = ver;
        entity.NewStatus = ConfigData.DELETED;

        return base.OnUpdate(entity);
    }

    public async Task<ActionResult> Publish(Int32 configId)
    {
        var rs = await _configService.Publish(configId);

        return JsonRefresh($"发布成功！共通知{rs}个应用", 3);
    }
}