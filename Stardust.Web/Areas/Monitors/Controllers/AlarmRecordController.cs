using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>报警记录。记录报警的开始、持续和恢复，统计报警持续时间</summary>
[Menu(15, true, Icon = "fa-bell")]
[MonitorsArea]
public class AlarmRecordController : EntityController<AlarmRecord>
{
    static AlarmRecordController()
    {
        ListFields.RemoveCreateField();

        {
            var df = ListFields.AddListField("History", "Creator");
            df.DisplayName = "告警历史";
            df.Header = "告警历史";
            df.Url = "/Monitors/AlarmHistory?groupId={GroupId}";
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<AlarmRecord> Search(Pager p)
    {
        var groupId = p["groupId"].ToInt(-1);
        var category = p["category"];
        var status = (AlarmStatuses)p["status"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AlarmRecord.Search(groupId, category, status, start, end, p["Q"], p);
    }
}