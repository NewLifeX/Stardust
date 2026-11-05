using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Configs;
using Stardust.Server.Services;

namespace Stardust.Web.Areas.Configs.Controllers;

/// <summary>配置历史。记录配置的变更历史，支持版本追溯和回滚检查</summary>
[Menu(0, false)]
[ConfigsArea]
public class ConfigHistoryController : ConfigsEntityController<ConfigHistory>
{
    private readonly ConfigService _configService;

    /// <summary>实例化。回滚等写操作复用 ConfigService，与 API/MCP 共用同一实现</summary>
    /// <param name="configService">配置服务</param>
    public ConfigHistoryController(ConfigService configService) => _configService = configService;

    static ConfigHistoryController()
    {
        // 日志列表需要显示详细信息
        ListFields.AddDataField("Remark", null, "TraceId");

        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is ConfigHistory entity && !entity.TraceId.IsNullOrEmpty();
        //}
        ListFields.LocalizedTraceUrl();
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<ConfigHistory> Search(Pager p)
    {
        var configId = p["configId"].ToInt(-1);
        var action = p["action"];
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ConfigHistory.Search(configId, action, success, start, end, p["Q"], p);
    }

    /// <summary>回滚到历史版本。仅发布历史支持；复用 ConfigService 回滚与通知逻辑</summary>
    /// <param name="id">历史编号</param>
    public async Task<ActionResult> Rollback(Int32 id)
    {
        try
        {
            var rs = await _configService.Rollback(id, HttpContext.RequestAborted);
            return JsonRefresh($"已回滚到版本 {rs.Version}，重发布 {rs.Republished} 项");
        }
        catch (Exception ex)
        {
            return JsonRefresh(ex.Message);
        }
    }
}
