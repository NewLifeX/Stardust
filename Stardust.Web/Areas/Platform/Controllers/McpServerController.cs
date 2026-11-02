using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Platform.McpServer;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>MCP服务器。远端MCP服务端点注册表</summary>
[Menu(25, true, Icon = "fa-server")]
[PlatformArea]
public class McpServerController : EntityController<McpServer>
{
    static McpServerController()
    {
        // 列表页隐藏自动维护字段
        ListFields.RemoveField("CreateIP", "UpdateIP");
        ListFields.RemoveCreateField().RemoveRemarkField();
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<McpServer> Search(Pager p)
    {
        var enable = p["enable"]?.ToBoolean();
        var projectId = p["projectId"].ToInt(0);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return McpServer.Search(enable, projectId, start, end, p["Q"], p);
    }
}
