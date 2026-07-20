using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>产品版本管理。产品发布版本，一个版本包含多个面向不同.NET运行时的包</summary>
[Menu(42)]
[NodesArea]
public class ProductReleaseController : EntityController<ProductRelease>
{
    static ProductReleaseController()
    {
        LogOnChange = true;

        ListFields.RemoveCreateField().RemoveRemarkField();

        var list = ListFields;
        list.Clear();
        var allows = new[] { "Id", "Version", "ProductCode", "Enable", "Force", "Channel", "Remark", "CreateTime", "UpdateTime" };
        foreach (var item in allows)
        {
            list.AddListField(item);
        }

        {
            var df = ListFields.AddListField("Packages", "Remark");
            df.DisplayName = "发布包";
            df.Header = "发布包";
            df.Url = "ProductPackage?releaseId={Id}";
            df.Target = "_frame";
        }

        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=产品版本&linkId={Id}";
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<ProductRelease> Search(Pager p)
    {
        var version = p["version"];
        var productCode = p["productCode"];
        var enable = p["enable"]?.ToBoolean();
        var force = p["force"]?.ToBoolean();
        var channel = (NodeChannels)p["channel"].ToInt();
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        var key = p["q"];

        if (channel <= 0) channel = (NodeChannels)(-1);

        return ProductRelease.Search(version, productCode, force, channel, enable, start, end, key, p);
    }

    protected override Int32 OnDelete(ProductRelease entity)
    {
        // 删除关联的所有发布包
        var packages = ProductPackage.FindAllByReleaseId(entity.Id);
        foreach (var pkg in packages)
        {
            pkg.Delete();
        }

        return base.OnDelete(entity);
    }
}
