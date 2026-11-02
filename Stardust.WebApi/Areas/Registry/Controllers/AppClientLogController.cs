using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

/// <summary>应用日志</summary>
[Menu(10, true, Icon = "fa-table")]
[RegistryArea]
public class AppClientLogController : RegistryEntityController<AppClientLog>
{
    static AppClientLogController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as AppClientLog).Devices > 0;
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as AppClientLog).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <remarks>
    /// 应用日志是分表实体（按月分库+按天分表），必须带 Id 时间窗做分表感知查询，禁止打基础表。
    /// appId 为必要过滤条件（分表感知查询按应用定位）；clientId/threadId/kind 可选；
    /// dtStart/dtEnd 均为空时默认当天 00:00 起。缺表环境返回空数据而非异常（SearchSafe 兜底）。
    /// </remarks>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppClientLog> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var clientId = p["clientId"];
        var threadId = p["threadId"];
        var kind = p["kind"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        // 均未指定时间时默认当天 00:00 起，保证分表感知查询始终有时间窗
        if (start.Year < 2000 && end.Year < 2000) start = DateTime.Today;

        return AppClientLog.SearchSafe(appId, clientId, threadId, start, end, p["Q"], kind, p, out _);
    }
}
