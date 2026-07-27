using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Platform.McpTokenResource;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>MCP令牌资源授权。Token与项目/节点/应用的授权关系</summary>
[Menu(20, true, Icon = "fa-table")]
[PlatformArea]
public class McpTokenResourceController : EntityController<McpTokenResource>
{
    static McpTokenResourceController()
    {
        // 列表页隐藏长文本字段，保留关键列
        ListFields.RemoveField("Remark", "CreateIP");
        ListFields.RemoveCreateField();
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<McpTokenResource> Search(Pager p)
    {
        var tokenId = p["tokenId"].ToInt(-1);
        var resourceType = p["resourceType"];
        var resourceId = p["resourceId"].ToInt(-1);
        var isAll = p["isAll"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return McpTokenResource.Search(tokenId, resourceType, resourceId, isAll, enable, start, end, p["Q"], p);
    }
}
