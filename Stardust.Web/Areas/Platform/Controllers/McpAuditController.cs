using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Platform.McpAudit;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>MCP审计日志。记录每次MCP工具调用</summary>
[Menu(10, true, Icon = "fa-table")]
[PlatformArea]
public class McpAuditController : EntityController<McpAudit>
{
    static McpAuditController()
    {
        // 列表页隐藏长文本字段，保留关键审计列
        ListFields.RemoveField("Arguments", "CallerUserAgent", "ErrorMessage", "TraceId");
        ListFields.RemoveCreateField().RemoveRemarkField();
        ListFields.TraceUrl("TraceId");
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<McpAudit> Search(Pager p)
    {
        var tokenId = p["tokenId"].ToInt(-1);
        var toolName = p["toolName"];
        var actionName = p["actionName"];
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return McpAudit.Search(tokenId, toolName, actionName, success, start, end, p["Q"], p);
    }
}
