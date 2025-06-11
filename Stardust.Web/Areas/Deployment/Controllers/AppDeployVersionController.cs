using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Web.Services;
using XCode.Membership;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Areas.Deployment.Controllers;

[Menu(80, false)]
[DeploymentArea]
public class AppDeployVersionController : EntityController<AppDeployVersion>
{
    static AppDeployVersionController()
    {
        ListFields.RemoveField("Hash", "CommitId", "CommitLog");
        ListFields.RemoveCreateField();
        ListFields.RemoveRemarkField();

        AddFormFields.RemoveCreateField();
        AddFormFields.RemoveField("Hash");
        ListFields.TraceUrl();

        LogOnChange = true;

        {
            var df = ListFields.GetField("DeployName") as ListField;
            df.Url = "/Deployment/AppDeploy?deployId={DeployId}";
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

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        var deployId = GetRequest("deployId").ToInt(-1);
        if (deployId > 0 || appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var deployId = GetRequest("deployId").ToInt(-1);
            if (deployId > 0) fields.RemoveField("DeployName");
        }

        return fields;
    }

    protected override IEnumerable<AppDeployVersion> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeployVersion.FindByKey(id);
            if (entity != null) return [entity];
        }

        var deployId = p["deployId"].ToInt(-1);
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        PageSetting.EnableAdd = deployId > 0;
        PageSetting.EnableNavbar = false;

        return AppDeployVersion.Search(deployId, null, null, start, end, p["Q"], p);
    }

    protected override Boolean Valid(AppDeployVersion entity, DataObjectMethodType type, Boolean post)
    {
        if (type == DataObjectMethodType.Delete || type == DataObjectMethodType.Update) return base.Valid(entity, type, post);
        if (!post && type == DataObjectMethodType.Insert)
        {
            entity.Enable = true;
            entity.Version = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }

        if (post)
        {
            // 根据包名去查应用发布集，如果是不小心上传了其它包，则给出提醒
            foreach (var file in Request.Form.Files)
            {
                var deploy = entity.Deploy;
                var name = Path.GetFileName(file.FileName);
                if (!name.IsNullOrEmpty() && deploy != null)
                {
                    if (!deploy.PackageName.IsNullOrEmpty() && !deploy.PackageName.IsMatch(name, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"文件名[{name}]与应用包名[{deploy.PackageName}]不匹配！");

                    //var deploy = AppDeploy.FindByName(name);
                    //if (deploy != null && deploy.Id != entity.AppId)
                    //    throw new InvalidOperationException($"文件名[{name}]对应另一个应用[{deploy}]，请确保是否上传错误！");
                }
            }

            entity.TraceId = DefaultSpan.Current?.TraceId;
        }

        return base.Valid(entity, type, post);
    }

    protected override Int32 OnInsert(AppDeployVersion entity)
    {
        var app = entity.Deploy;
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

        var app = entity.Deploy;
        var rs = base.OnUpdate(entity);
        app?.Fix();

        //// 上传完成即发布。即使新增，也是插入后保存文件，然后再来OnUpdate
        //if (entity.Enable && !entity.Url.IsNullOrEmpty() && app != null && app.Enable && app.AutoPublish)
        //{
        //    app.Version = entity.Version;
        //    app.Update();

        //    Publish(entity.App).Wait();
        //}

        return rs;
    }

    protected override Int32 OnDelete(AppDeployVersion entity)
    {
        //删除Attachment记录和文件，同文件有可能被多次上传，Hash查询不一定唯一
        //根据当前表Url(/cube/file?id=7185535436880961536.zip)取 Id@Attachment 唯一
        if (!entity.Url.IsNullOrEmpty())
        {
            var id = Path.GetFileNameWithoutExtension(entity.Url.Replace("/cube/file?id=", string.Empty));
            var att = Attachment.FindById(id.ToLong());
            if (att != null)
            {
                var attPath = att.GetFilePath();
                //防意外丢失
                if (System.IO.File.Exists(attPath))
                {
                    System.IO.File.Delete(attPath);
                }
                //删除记录
                att.DeleteAsync();
            }
        }

        var rs = base.OnDelete(entity);
        entity.Deploy?.Fix();
        return rs;
    }

    protected override async Task<Attachment> SaveFile(AppDeployVersion entity, IFormFile file, String uploadPath, String fileName)
    {
        var att = await base.SaveFile(entity, file, uploadPath, fileName);
        if (att != null)
        {
            var deploy = entity.Deploy;

            _deployService.ReadDotNet(entity, att, uploadPath);

            // 处理Nginx
            if (deploy.Port == 0 || deploy.Urls.IsNullOrEmpty())
                _deployService.ReadNginx(entity, att, uploadPath);
            if (deploy.Port > 0 && !deploy.Urls.IsNullOrEmpty())
                _deployService.BuildNginx(entity, att, uploadPath);

            entity.Hash = att.Hash;
            entity.Size = att.Size;
            entity.Url = $"/cube/file?id={att.Id}{att.Extension}";

            entity.Update();

            // 上传完成即发布。即使新增，也是插入后保存文件，然后再来OnUpdate
            if (entity.Enable && !entity.Url.IsNullOrEmpty() && deploy != null && deploy.Enable && deploy.AutoPublish)
            {
                deploy.Version = entity.Version;
                deploy.Update();

                _ = Publish(entity.Deploy);
            }
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

        var app = ver.Deploy;
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
                //// 排序和延迟
                //appNodes = appNodes.OrderBy(e => e.Delay).ToList();
                foreach (var item in appNodes)
                {
                    //span?.AppendTag(item);
                    if (item.Enable) ts.Add(_deployService.Control(app, item, "install", UserHost, item.Delay, 0));
                    //if (item.Enable)
                    //{
                    //    ts.Add(Task.Run(async () =>
                    //    {
                    //        if (item.Delay > 0) await Task.Delay(item.Delay * 1000);
                    //        await _deployService.Control(app, item, "install", UserHost, 0);
                    //    }));
                    //}
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