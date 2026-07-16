using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Entity;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>dotNet安装包管理。管理面向不同操作系统和CPU架构的.NET运行时安装包</summary>
[Menu(44)]
[NodesArea]
public class DotNetPackageController : EntityController<DotNetPackage>
{
    static DotNetPackageController()
    {
        LogOnChange = true;

        ListFields.RemoveField("Source", "Size", "FileHash", "Remark", "CreateIP", "UpdateIP");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=dotNet安装包&linkId={Id}";
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<DotNetPackage> Search(Pager p)
    {
        var version = p["version"];
        var kind = p["kind"];
        var osKind = (Stardust.Models.OSKind)p["osKind"].ToInt(-1);
        var architecture = (Stardust.Models.CpuArch)p["architecture"].ToInt(-1);
        var force = p["force"]?.ToBoolean();
        var channel = (NodeChannels)p["channel"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        var key = p["q"];

        return DotNetPackage.Search(version, kind, osKind, architecture, force, channel, enable, start, end, key, p);
    }

    /// <summary>上传文件后自动计算哈希和大小</summary>
    protected override async Task<Attachment> SaveFile(DotNetPackage entity, IFormFile file, String uploadPath, String fileName)
    {
        var att = await base.SaveFile(entity, file, uploadPath, fileName);
        if (att != null)
        {
            entity.FileHash = att.Hash;
            entity.Size = att.Size;
            entity.Source = $"/cube/file?id={att.Id}{att.Extension}";
            entity.Update();
        }

        // 返回 null 阻止 Cube 用物理路径覆盖 Source 字段
        return null;
    }
}
