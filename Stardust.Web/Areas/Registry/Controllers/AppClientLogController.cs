using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

/// <summary>应用日志</summary>
[Menu(10, true, Icon = "fa-table")]
[RegistryArea]
public class AppClientLogController : EntityController<AppClientLog>
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

    /// <summary>高级搜索。按应用、线程、时间范围与关键字分页查询，未指定时间时默认查当天（按时间分表）</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppClientLog> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var threadId = p["threadId"];
        //var deviceId = p["deviceId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        if (start.Year < 2000 && end.Year < 2000)
        {
            var dt = DateTime.Today;
            start = dt;
            end = dt;
            p["dtStart"] = start.ToString("yyyy-MM-dd");
            p["dtEnd"] = end.ToString("yyyy-MM-dd");
        }

        return AppClientLog.Search(threadId, appId, start, end, p["Q"], p);
    }
}
