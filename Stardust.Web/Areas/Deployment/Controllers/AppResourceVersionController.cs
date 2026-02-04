using System.ComponentModel;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Storages;
using XCode.Membership;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>资源版本。资源的多个版本，支持不同运行时平台</summary>
[Menu(0, false, Icon = "fa-table")]
[DeploymentArea]
public class AppResourceVersionController : DeploymentEntityController<AppResourceVersion>
{
    static AppResourceVersionController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as AppResourceVersion).Devices > 0;
        //    df.Target = "_frame";
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as AppResourceVersion).Kind).ToString("X4");
        //}
        ListFields.TraceUrl("TraceId");
    }

    private readonly IFileStorage _fileStorage;

    public AppResourceVersionController(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<AppResourceVersion> Search(Pager p)
    {
        var resourceId = p["resourceId"].ToInt(-1);
        var os = (Stardust.Models.OSKind)p["os"].ToInt(-1);
        var arch = (Stardust.Models.CpuArch)p["arch"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppResourceVersion.Search(resourceId, os, arch, enable, start, end, p["Q"], p);
    }

    protected override Boolean Valid(AppResourceVersion entity, DataObjectMethodType type, Boolean post)
    {
        if (type == DataObjectMethodType.Delete || type == DataObjectMethodType.Update) return base.Valid(entity, type, post);
        if (!post && type == DataObjectMethodType.Insert)
        {
            entity.Enable = true;
            entity.Version = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }

        return base.Valid(entity, type, post);
    }

    protected override Int32 OnDelete(AppResourceVersion entity)
    {
        //删除Attachment记录和文件，同文件有可能被多次上传，Hash查询不一定唯一
        //根据当前表Url(/cube/file?id=7185535436880961536.zip)取 Id@Attachment 唯一
        if (!entity.Url.IsNullOrEmpty())
        {
            var id = Path.GetFileNameWithoutExtension(entity.Url.Replace("/cube/file?id=", String.Empty));
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

        return base.OnDelete(entity);
    }

    protected override async Task<Attachment> SaveFile(AppResourceVersion entity, IFormFile file, String uploadPath, String fileName)
    {
        var att = await base.SaveFile(entity, file, uploadPath, fileName);
        if (att != null)
        {
            entity.Hash = att.Hash;
            entity.Size = att.Size;
            entity.Url = $"/cube/file?id={att.Id}{att.Extension}";

            entity.Update();
        }

        //// 广播指定附件在当前节点可用
        //await _fileStorage.PublishNewFileAsync(att.Id, att.FilePath, HttpContext.RequestAborted);

        // 不给上层拿到附件，避免Url字段被覆盖
        return null;
    }
}