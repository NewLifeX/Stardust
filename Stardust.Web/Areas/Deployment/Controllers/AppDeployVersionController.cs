using System.ComponentModel;
using System.Runtime.ConstrainedExecution;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Web.Services;
using XCode;
using XCode.Membership;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Areas.Deployment.Controllers;

[Menu(80)]
[DeploymentArea]
public class AppDeployVersionController : EntityController<AppDeployVersion>
{
    static AppDeployVersionController()
    {
        ListFields.RemoveCreateField();
        ListFields.RemoveRemarkField();

        AddFormFields.RemoveCreateField();
        AddFormFields.RemoveField("Hash");
        ListFields.TraceUrl();

        LogOnChange = true;

        {
            var df = ListFields.GetField("AppName") as ListField;
            df.Url = "/Deployment/AppDeploy?Id={AppId}";
        }
        {
            var df = ListFields.AddListField("UseVersion", null, "Enable");
            df.Header = "使用版本";
            df.DisplayName = "使用版本";
            df.Title = "应用部署集使用该版本";
            df.Url = "/Deployment/AppDeployVersion/UseVersion?Id={Id}";
            df.DataAction = "action";
        }
    }

    private readonly DeployService _deployService;
    private readonly ITracer _tracer;

    public AppDeployVersionController(DeployService deployService, ITracer tracer)
    {
        _deployService = deployService;
        _tracer = tracer;
    }

    protected override IEnumerable<AppDeployVersion> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeployVersion.FindById(id);
            if (entity != null) return new List<AppDeployVersion> { entity };
        }

        var appId = p["appId"].ToInt(-1);
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        PageSetting.EnableAdd = appId > 0;
        PageSetting.EnableNavbar = false;

        return AppDeployVersion.Search(appId, null, null, start, end, p["Q"], p);
    }

    protected override Boolean Valid(AppDeployVersion entity, DataObjectMethodType type, Boolean post)
    {
        if (!post && type == DataObjectMethodType.Insert) entity.Version = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        if (post)
            entity.TraceId = DefaultSpan.Current?.TraceId;

        return base.Valid(entity, type, post);
    }

    protected override Int32 OnInsert(AppDeployVersion entity)
    {
        var app = entity.App;
        var rs = base.OnInsert(entity);
        app?.Fix();

        // 上传完成即发布
        //if (!entity.Url.IsNullOrEmpty() && app != null && app.Enable && app.AutoPublish)
        //// 插入的时候，还没有保存文件
        //if (app != null && app.Enable && app.AutoPublish)
        //{
        //    app.Version = entity.Version;
        //    app.Update();

        //    // 文件还没保存，所以需要延迟发布
        //    Task.Run(async () =>
        //    {
        //        await Task.Delay(1000);
        //        await Publish(entity.App);
        //    });
        //}

        return rs;
    }

    protected override Int32 OnUpdate(AppDeployVersion entity)
    {
        entity.TraceId = DefaultSpan.Current?.TraceId;

        //var changed = (entity as IEntity).Dirtys[nameof(entity.Url)];

        var app = entity.App;
        var rs = base.OnUpdate(entity);
        app?.Fix();

        // 上传完成即发布。即使新增，也是插入后保存文件，然后再来OnUpdate
        if (entity.Enable && !entity.Url.IsNullOrEmpty() && app != null && app.Enable && app.AutoPublish)
        {
            app.Version = entity.Version;
            app.Update();

            Publish(entity.App).Wait();
        }

        return rs;
    }

    protected override Int32 OnDelete(AppDeployVersion entity)
    {
        var rs = base.OnDelete(entity);
        entity.App?.Fix();
        return rs;
    }

    protected override async Task<Attachment> SaveFile(AppDeployVersion entity, IFormFile file, String uploadPath, String fileName)
    {
        var att = await base.SaveFile(entity, file, uploadPath, fileName);
        if (att != null)
        {
            entity.Hash = att.Hash;
            entity.Size = att.Size;
            entity.Url = $"/cube/file?id={att.Id}{att.Extension}";

            entity.Update();
        }

        // 不给上层拿到附件，避免Url字段被覆盖
        return null;
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> UseVersion(Int32 id)
    {
        var ver = AppDeployVersion.FindById(id);
        if (ver == null) throw new Exception("找不到版本！");

        if (!ver.Enable) throw new Exception("版本未启用！");
        if (ver.Version.IsNullOrEmpty()) throw new Exception("版本号未设置！");
        if (ver.Url.IsNullOrEmpty()) throw new Exception("文件不存在！");

        ver.TraceId = DefaultSpan.Current?.TraceId;
        ver.Update();

        var app = ver.App;
        app.Version = ver.Version;
        app.Update();

        // 自动发布。应用版本后自动发布到启用节点，加快发布速度
        await Publish(app);

        return JsonRefresh($"成功！");
    }

    async Task Publish(AppDeploy app)
    {
        if (app == null) return;

        // 自动发布。应用版本后自动发布到启用节点，加快发布速度
        if (app.Enable && app.AutoPublish)
        {
            using var span = _tracer?.NewSpan("AutoPublish", app);
            try
            {
                var ts = new List<Task>();
                var appNodes = AppDeployNode.FindAllByAppId(app.Id);
                foreach (var item in appNodes)
                {
                    //span?.AppendTag(item);
                    if (item.Enable) ts.Add(_deployService.Control(app, item, "install", UserHost));
                }
                span?.AppendTag($"控制{ts.Count}个节点");
                await Task.WhenAll(ts);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                throw;
            }
        }
    }
}