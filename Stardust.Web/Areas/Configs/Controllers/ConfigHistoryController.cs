using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers;

[Menu(0, false)]
[ConfigsArea]
public class ConfigHistoryController : ConfigsEntityController<ConfigHistory>
{
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
        ListFields.TraceUrl();
    }

    protected override IEnumerable<ConfigHistory> Search(Pager p)
    {
        var configId = p["configId"].ToInt(-1);
        var action = p["action"];
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ConfigHistory.Search(configId, action, success, start, end, p["Q"], p);
    }
}