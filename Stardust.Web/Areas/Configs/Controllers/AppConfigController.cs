using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Configs;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Configs.Controllers;

[Menu(58)]
[ConfigsArea]
public class AppConfigController : EntityController<AppConfig>
{
    static AppConfigController()
    {
        LogOnChange = true;

        ListFields.RemoveCreateField();
        ListFields.RemoveField("AppId", "AppName", "ProjectName", "PublishTime", "Quotes", "QuoteNames", "EnableApollo", "ApolloMetaServer", "ApolloAppId", "ApolloNameSpace", "Remark");

        {
            var df = ListFields.AddListField("ConfigData", "Enable");
            df.Header = "管理配置";
            df.DisplayName = "管理配置";
            df.Title = "查看该应用所有配置数据";
            df.Url = "/Configs/ConfigData?appId={Id}";
            df.Target = "_frame";
        }

        {
            var df = ListFields.AddListField("Publish", "Version");
            df.Header = "发布";
            df.DisplayName = "发布";
            df.Url = "/Configs/AppConfig/Publish?appId={Id}";
            df.DataAction = "action";
            df.DataVisible = e => e is AppConfig ac && ac.Version < ac.NextVersion;
        }

        {
            var df = ListFields.AddListField("History", "Version");
            df.Header = "历史";
            df.DisplayName = "历史";
            df.Title = "查看该应用的配置历史";
            df.Url = "/Configs/ConfigHistory?appId={Id}";
            df.Target = "_frame";
        }

        {
            var df = ListFields.AddListField("Preview", "Version");
            df.Header = "预览";
            df.DisplayName = "预览";
            df.Title = "查看该应用的配置数据";
            df.Url = "/config/getall?appId={Name}&secret={appSecret}";
            df.Target = "_blank";
        }

        //{
        //    var df = ListFields.AddListField("Online", "Version");
        //    df.Header = "在线实例";
        //    df.DisplayName = "在线实例";
        //    df.Title = "查看该应用的在线实例应用";
        //    df.Url = "/registry/AppOnline?appId={AppId}";
        //    df.DataVisible = e => e is AppConfig entity && entity.AppId > 0;
        //}

        {
            var df = ListFields.AddListField("Log", "UpdateUserID");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=应用配置&linkId={Id}";
            df.Target = "_frame";
        }

        {
            var df = AddFormFields.AddDataField("Quotes", "IsGlobal");
            df.DataSource = x => AppConfig.FindAllWithCache().Where(e => e.CanBeQuoted).ToDictionary(e => e.Id, e => e.Name);
        }

        {
            var df = EditFormFields.AddDataField("Quotes", "IsGlobal");
            df.DataSource = x => AppConfig.FindAllWithCache().Where(e => e.CanBeQuoted).ToDictionary(e => e.Id, e => e.Name);
        }
    }

    private readonly StarFactory _starFactory;
    private readonly ITracer _tracer;

    public AppConfigController(StarFactory starFactory, ITracer tracer)
    {
        _starFactory = starFactory;
        _tracer = tracer;
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        if (appId <= 0) appId = GetRequest("Id").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
        var projectId = GetRequest("projectId").ToInt(-1);
        if (projectId > 0)
        {
            PageSetting.NavView = "_Project_Nav";
            PageSetting.EnableNavbar = false;
        }

        //PageSetting.EnableAdd = false;
    }

    protected override IEnumerable<AppConfig> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppConfig.FindById(id);
            if (entity != null) return new List<AppConfig> { entity };
        }
        var appId = p["appId"].ToInt(-1);
        if (appId > 0)
        {
            var entity = AppConfig.FindByAppId(appId);
            if (entity != null) return new List<AppConfig> { entity };
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppConfig.Search(projectId, category, enable, start, end, p["Q"], p);
    }

    protected override Boolean Valid(AppConfig entity, DataObjectMethodType type, Boolean post)
    {
        if (post)
        {
            // 更新时关联应用
            switch (type)
            {
                case DataObjectMethodType.Update:
                case DataObjectMethodType.Insert:
                    if (entity.AppId == 0)
                    {
                        var app = App.FindByName(entity.Name);
                        if (app != null) entity.AppId = app.Id;
                    }
                    break;
            }
        }

        return base.Valid(entity, type, post);
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

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SyncApp()
    {
        var rs = AppConfig.Sync();

        return JsonRefresh($"成功同步[{rs}]个应用！");
    }
}