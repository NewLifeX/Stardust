using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Deployment;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

[DeploymentArea]
[Menu(90)]
public class AppDeployController : EntityController<AppDeploy>
{
    private readonly StarFactory _starFactory;

    static AppDeployController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveField("WorkingDirectory");
        ListFields.RemoveField("ApolloMetaServer");
        AddFormFields.RemoveCreateField();

        LogOnChange = true;

        {
            var df = ListFields.GetField("Nodes") as ListField;
            df.Header = "节点";
            df.Title = "管理服务器节点";
            df.DisplayName = "{Nodes}";
            df.Url = "AppDeployNode?appId={Id}";
        }

        {
            var df = ListFields.GetField("Version") as ListField;
            df.Header = "版本";
            df.Title = "管理应用版本";
            df.Url = "AppDeployVersion?appId={Id}";
        }
        {
            var df = ListFields.AddListField("AddVersion", "FileName");
            df.Header = "版本";
            df.DisplayName = "添加版本";
            df.Title = "添加应用版本";
            df.Url = "AppDeployVersion/Add?appId={Id}";
        }

        {
            var df = ListFields.GetField("Name") as ListField;
            //df.Header = "应用";
            df.Url = "/Registry/App?q={Name}";
        }

        {
            var df = ListFields.AddListField("Log", "UpdateUserId");
            df.DisplayName = "修改日志";
            df.Header = "修改日志";
            df.Url = "/Admin/Log?category=应用部署&linkId={Id}";
        }
    }

    public AppDeployController(StarFactory starFactory) => _starFactory = starFactory;

    protected override IEnumerable<AppDeploy> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeploy.FindById(id);
            if (entity != null) return new List<AppDeploy> { entity };
        }

        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppDeploy.Search(category, enable, start, end, p["Q"], p);
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

    protected override Int32 OnUpdate(AppDeploy entity)
    {
        // 如果执行了启用，则通知节点
        if ((entity as IEntity).Dirtys["Enable"]) Task.Run(() => NotifyChange(entity.Id));

        return base.OnUpdate(entity);
    }

    public async Task<ActionResult> NotifyChange(Int32 id)
    {
        var deploy = AppDeploy.FindById(id);
        if (deploy != null)
        {
            // 通知该发布集之下所有节点，应用服务数据有变化，需要马上执行心跳
            var list = AppDeployNode.FindAllByAppId(deploy.Id);
            foreach (var item in list)
            {
                var node = item.Node;
                if (node != null)
                {
                    // 通过接口发送指令给StarServer
                    await _starFactory.SendNodeCommand(node.Code, "Deploy");
                }
            }
        }

        return RedirectToAction("Index");
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SyncApp()
    {
        var rs = AppDeploy.Sync();

        return JsonRefresh($"成功同步[{rs}]个应用！");
    }
}