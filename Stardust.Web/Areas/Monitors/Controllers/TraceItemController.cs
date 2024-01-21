using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(65, false)]
[MonitorsArea]
public class TraceItemController : EntityController<TraceItem>
{
    static TraceItemController()
    {
        LogOnChange = true;

        ListFields.RemoveField("AlarmRobot");
        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();
        ListFields.RemoveRemarkField();

        {
            var df = ListFields.AddListField("Monitor", null, "Name");
            df.DisplayName = "每日监控";
            df.Header = "每日监控";
            df.Url = "/Monitors/TraceDayStat?appId={AppId}&itemId={Id}";
        }
        {
            var df = ListFields.AddListField("Log", "CreateUser");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=跟踪项&linkId={Id}";
            df.Target = "_frame";
        }
    }

    protected override IEnumerable<TraceItem> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var entity = TraceItem.FindById(id);
            if (entity != null) return new[] { entity };
        }

        var appId = p["appId"].ToInt(-1);
        var name = p["name"];
        var kind = p["kind"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        p.RetrieveState = true;

        return TraceItem.Search(appId, name, kind, enable, start, end, p["Q"], p);
    }

    protected override Boolean Valid(TraceItem entity, DataObjectMethodType type, Boolean post)
    {
        var rs = base.Valid(entity, type, post);

        if (post && type == DataObjectMethodType.Delete) TraceDayStat.DeleteByAppAndItem(entity.AppId, entity.Id);

        return rs;
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Fix()
    {
        foreach (var item in SelectKeys)
        {
            var ti = TraceItem.FindById(item.ToInt());
            if (ti != null)
            {
                XTrace.WriteLine("修正 {0}/{1}", ti.Name, ti.Id);

                {
                    var list = TraceDayStat.FindAllByAppAndItem(ti.AppId, ti.Id);
                    ti.Days = list.DistinctBy(e => e.StatDate.Date).Count();
                    ti.Total = list.Sum(e => e.Total);
                }

                ti.Update();
            }
        }

        return JsonRefresh("成功！");
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult FixDisplay()
    {
        var rules = NodeRule.FindAllWithCache().Where(e => e.Enable).OrderByDescending(e => e.Priority).ToList();
        if (rules.Count > 0)
        {
            foreach (var item in SelectKeys)
            {
                var ti = TraceItem.FindById(item.ToInt());
                if (ti != null && ti.DisplayName.IsNullOrEmpty())
                {
                    // 去掉 http:// https:// 等前缀
                    var name = ti.Name;
                    var p = name.IndexOf("://");
                    if (p >= 0)
                    {
                        name = name[(p + 3)..];
                        if (!name.IsNullOrEmpty())
                        {
                            var rule = rules.FirstOrDefault(e => e.Rule.IsMatch(name, StringComparison.OrdinalIgnoreCase));
                            if (rule != null)
                            {
                                XTrace.WriteLine("修正显示 {0}/{1}", ti.Name, ti.Id);

                                var dis = !rule.Name.IsNullOrEmpty() ? rule.Name : rule.Category;

                                var ss = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                                ti.DisplayName = $"{dis}/{ss[^1]}";

                                ti.Update();
                            }
                        }
                    }
                }
            }
        }

        return JsonRefresh("成功！");
    }
}