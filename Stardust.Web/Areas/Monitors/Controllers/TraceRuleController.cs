using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>跟踪规则。全局黑白名单，白名单放行，黑名单拦截</summary>
[Menu(50, true, Icon = "fa-table")]
[MonitorsArea]
public class TraceRuleController : EntityController<TraceRule>
{
    static TraceRuleController()
    {
        LogOnChange = true;

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
        //    df.DataVisible = e => (e as TraceRule).Devices > 0;
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as TraceRule).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<TraceRule> Search(Pager p)
    {
        //var deviceId = p["deviceId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return TraceRule.Search(start, end, p["Q"], p);
    }
}