using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Monitors;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(20)]
[MonitorsArea]
public class AlarmGroupController : EntityController<AlarmGroup>
{
    static AlarmGroupController()
    {
        LogOnChange = true;

        {
            var df = ListFields.AddListField("test", "CreateUser");
            df.DisplayName = "测试";
            df.Url = "/Monitors/AlarmGroup/ExecuteNow?id={Id}";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("History", "CreateUser");
            df.DisplayName = "告警历史";
            df.Header = "告警历史";
            df.Url = "/Monitors/AlarmHistory?groupId={Id}";
        }
        {
            var df = ListFields.AddListField("Log", "CreateUser");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=告警组&linkId={Id}";
            df.Target = "_frame";
        }
    }

    protected override IEnumerable<AlarmGroup> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var app = AlarmGroup.FindById(id);
            if (app != null) return new[] { app };
        }

        var name = p["name"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AlarmGroup.Search(name, enable, start, end, p["Q"], p);
    }

    /// <summary>马上执行</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult ExecuteNow(String id)
    {
        try
        {
            var entity = AlarmGroup.FindById(id.ToInt());
            if (entity != null && entity.Enable)
            {
                var hi = RobotHelper.SendAlarm(entity.Name, entity.WebHook, "报警测试", entity.Content);
                if (!hi.Success && !hi.Error.IsNullOrEmpty()) throw new Exception(hi.Error);
            }

            return JsonRefresh("OK", 0);
        }
        catch (Exception ex)
        {
            return Json(500, "执行失败！" + ex.Message);
        }
    }
}