using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Storages;
using XCode.Membership;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点版本。追踪各组件在节点上的版本信息，管理组件升级包上传与分发</summary>
[Menu(40)]
[NodesArea]
public class NodeVersionController : NodesEntityController<NodeVersion>
{
    private readonly IFileStorage _fileStorage;

    public NodeVersionController(IFileStorage fileStorage) => _fileStorage = fileStorage;

    static NodeVersionController()
    {
        LogOnChange = true;

        ListFields.RemoveField("Source", "Size", "FileHash", "Preinstall", "Executor");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = ListFields.AddListField("Log", "CreateUserID");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=节点版本&linkId={ID}";
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<NodeVersion> Search(Pager p)
    {
        var enable = p["enable"]?.ToBoolean();
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        var key = p["q"];

        return NodeVersion.Search(start, end, enable, key, p);
    }

    protected override Int32 OnDelete(NodeVersion entity)
    {
        if (!entity.Source.IsNullOrEmpty())
        {
            //取 Id@Attachment 唯一
            var id = Path.GetFileNameWithoutExtension(entity.Source.Replace("/cube/file?id=", String.Empty));
            var att = Attachment.FindById(id.ToLong());
            if (att != null)
            {
                var attPath = att.GetFilePath();
                if (System.IO.File.Exists(attPath))
                {
                    XTrace.Log.Error($"success delete {attPath}");
                    System.IO.File.Delete(attPath);
                }
                //删除记录
                att.DeleteAsync();
            }
        }

        var rs = base.OnDelete(entity);
        return rs;
    }

    protected override async Task<Attachment> SaveFile(NodeVersion entity, IFormFile file, String uploadPath, String fileName)
    {
        var att = await base.SaveFile(entity, file, uploadPath, fileName);
        if (att != null)
        {
            entity.FileHash = att.Hash;
            entity.Size = att.Size;
            entity.Source = $"/cube/file?id={att.Id}{att.Extension}";

            entity.Update();
        }

        // 不给上层拿到附件，避免Url字段被覆盖
        return null;
    }
}
