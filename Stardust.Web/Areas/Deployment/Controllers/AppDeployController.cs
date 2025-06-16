using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Deployment;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

[DeploymentArea]
[Menu(90)]
public class AppDeployController : EntityController<AppDeploy>
{
    static AppDeployController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveField("AppId", "MultiVersion", "Repository", "Branch", "ProjectPath", "PackageFilters", "WorkingDirectory", "UserName", "Environments", "MaxMemory", "Mode", "Remark");
        AddFormFields.RemoveCreateField();

        LogOnChange = true;

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?Id={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("AppName") as ListField;
            df.Url = "/Registry/App?Id={AppId}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/Deployment/AppDeploy?appId={AppId}&deployId={Id}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.AddListField("NodeManage", null, "Nodes") as ListField;
            df.DisplayName = "部署节点";
            df.Url = "/Deployment/AppDeployNode?deployId={Id}";
        }
        {
            var df = ListFields.AddListField("BuildNode", null, "Nodes") as ListField;
            df.DisplayName = "编译节点";
            df.Url = "/Deployment/AppBuildNode?deployId={Id}";
        }
        {
            var df = ListFields.GetField("Version") as ListField;
            df.Header = "版本";
            df.Title = "管理应用版本";
            df.Url = "/Deployment/AppDeployVersion?deployId={Id}";
        }
        {
            var df = ListFields.AddListField("AddVersion", "FileName");
            df.Header = "版本管理";
            df.DisplayName = "版本管理";
            df.Title = "管理所有版本文件";
            df.Url = "/Deployment/AppDeployVersion?deployId={Id}";
        }
        {
            var df = ListFields.AddListField("History", "UpdateUserId");
            df.DisplayName = "部署历史";
            df.Url = "/Deployment/AppDeployHistory?deployId={Id}";
        }
        {
            var df = ListFields.AddListField("Log", "UpdateUserId");
            df.DisplayName = "审计日志";
            df.Url = "/Admin/Log?category=应用部署&linkId={Id}";
            df.Target = "_frame";
        }
    }

    //public AppDeployController(StarFactory starFactory) => _starFactory = starFactory;

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

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var appId = GetRequest("appId").ToInt(-1);
            if (appId > 0) fields.RemoveField("ProjectName", "AppName", "Category");

            //var deployId = GetRequest("deployId").ToInt(-1);
            //if (deployId > 0) fields.RemoveField("DeployName");
        }

        return fields;
    }

    protected override IEnumerable<AppDeploy> Search(Pager p)
    {
        var id = p["deployId"].ToInt(-1);
        if (id <= 0) id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeploy.FindByKey(id);
            if (entity != null) return [entity];
        }
        var appId = p["appId"].ToInt(-1);
        if (appId > 0)
        {
            var list = AppDeploy.FindAllByAppId(appId);
            if (list.Count == 0)
            {
                // 自动新建发布集
                var app = App.FindById(appId);
                if (app != null)
                {
                    var entity = new AppDeploy
                    {
                        AppId = appId,
                        Name = app.Name,
                        ProjectId = app.ProjectId,
                        Category = app.Category
                    };
                    entity.Insert();
                }
            }
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppDeploy.Search(projectId, appId, category, enable, start, end, p["Q"], p);
    }

    protected override Boolean Valid(AppDeploy entity, DataObjectMethodType type, Boolean post)
    {
        if (!post)
        {
            if (type == DataObjectMethodType.Insert)
            {
                entity.Enable = true;
                //entity.AutoStart = true;
            }

            return base.Valid(entity, type, post);
        }

        if (entity.Id == 0)
        {
            // 从应用表继承ID
            var app = App.FindByName(entity.Name);
            if (app != null) entity.Id = app.Id;
        }

        entity.Refresh();

        return base.Valid(entity, type, post);
    }

    //protected override Int32 OnUpdate(AppDeploy entity)
    //{
    //    // 如果执行了启用，则通知节点
    //    if ((entity as IEntity).Dirtys["Enable"]) Task.Run(() => NotifyChange(entity.Id));

    //    return base.OnUpdate(entity);
    //}

    //public async Task<ActionResult> NotifyChange(Int32 id)
    //{
    //    var deploy = AppDeploy.FindById(id);
    //    if (deploy != null)
    //    {
    //        // 通知该发布集之下所有节点，应用服务数据有变化，需要马上执行心跳
    //        var list = AppDeployNode.FindAllByAppId(deploy.Id);
    //        foreach (var item in list)
    //        {
    //            var node = item.Node;
    //            if (node != null)
    //            {
    //                // 通过接口发送指令给StarServer
    //                await _starFactory.SendNodeCommand(node.Code, "deploy/publish");
    //            }
    //        }
    //    }

    //    return RedirectToAction("Index");
    //}

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SyncApp()
    {
        var rs = AppDeploy.Sync();

        return JsonRefresh($"成功同步[{rs}]个应用！");
    }
}