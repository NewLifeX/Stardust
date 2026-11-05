using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Nodes.ProductPackage;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>产品发布包。隶属于发布版本的安装包，面向不同.NET运行时目标</summary>
[Menu(20, true, Icon = "fa-table")]
[NodesArea]
public class ProductPackageController : NodesEntityController<ProductPackage>
{
    static ProductPackageController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();
    }

    //private readonly ITracer _tracer;

    //public ProductPackageController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<ProductPackage> Search(Pager p)
    {
        var releaseId = p["releaseId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ProductPackage.Search(releaseId, start, end, p["Q"], p);
    }

    /// <summary>上传文件后自动计算哈希和大小</summary>
    /// <remarks>与 dotNet 安装包保持一致，数据库里只保存相对地址，安装引导页按需补全并加签</remarks>
    protected override async Task<Attachment> SaveFile(ProductPackage entity, IFormFile file, String uploadPath, String fileName)
    {
        var att = await base.SaveFile(entity, file, uploadPath, fileName);
        if (att != null)
        {
            entity.FileHash = att.Hash;
            entity.Size = att.Size;
            entity.Source = $"/cube/file?id={att.Id}{att.Extension}";
            if (!(entity as XCode.IEntity).IsNullKey) entity.Update();
        }

        // API 版 /attachments 端点直接使用返回值，必须返回附件实体；
        // 基类 SaveFiles 写回的也是 GetAttachmentUrl 相对地址（/cube/file?id=…），与这里手写的值相同，不会被物理路径覆盖
        return att;
    }
}
