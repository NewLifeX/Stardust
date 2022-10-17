using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Deployment;
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

        AddFormFields.RemoveCreateField();

        LogOnChange = true;
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

    //protected override Boolean Valid(AppDeployVersion entity, DataObjectMethodType type, Boolean post)
    //{
    //    if (post)
    //    {
    //        switch (type)
    //        {
    //            case DataObjectMethodType.Update:
    //                entity.App?.Fix();
    //                break;
    //            case DataObjectMethodType.Insert:
    //                break;
    //            case DataObjectMethodType.Delete:
    //                break;
    //            default:
    //                break;
    //        }
    //    }

    //    return base.Valid(entity, type, post);
    //}

    protected override Int32 OnInsert(AppDeployVersion entity)
    {
        var rs = base.OnInsert(entity);
        entity.App?.Fix();
        return rs;
    }

    protected override Int32 OnUpdate(AppDeployVersion entity)
    {
        var rs = base.OnUpdate(entity);
        entity.App?.Fix();
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
        }

        // 不给上层拿到附件，避免Url字段被覆盖
        return null;
    }
}