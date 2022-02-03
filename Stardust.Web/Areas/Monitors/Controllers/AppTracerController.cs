using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [Menu(90)]
    [MonitorsArea]
    public class AppTracerController : EntityController<AppTracer>
    {
        static AppTracerController()
        {
            LogOnChange = true;

            {
                var df = ListFields.AddListField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=应用跟踪器&linkId={Id}";
            }
        }

        protected override IEnumerable<AppTracer> Search(Pager p)
        {
            var id = p["Id"].ToInt(-1);
            if (id > 0)
            {
                var app = AppTracer.FindByID(id);
                if (app != null) return new[] { app };
            }

            var category = p["category"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppTracer.Search(category, enable, start, end, p["Q"], p);
        }

        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult Fix()
        {
            foreach (var item in SelectKeys)
            {
                var app = AppTracer.FindByID(item.ToInt());
                if (app != null)
                {
                    XTrace.WriteLine("修正 {0}/{1}", app.Name, app.ID);

                    {
                        var list = TraceDayStat.FindAllByAppId(app.ID);
                        foreach (var st in list)
                        {
                            if (st.ItemId == 0 && !st.Name.IsNullOrEmpty())
                            {
                                var ti = app.GetOrAddItem(st.Name);
                                st.ItemId = ti.Id;
                                st.Update();
                            }
                        }
                        app.Days = list.Count;
                        app.Total = list.Sum(e => e.Total);
                    }
                    {
                        var list = TraceHourStat.FindAllByAppId(app.ID);
                        foreach (var st in list)
                        {
                            if (st.ItemId == 0 && !st.Name.IsNullOrEmpty())
                            {
                                var ti = app.GetOrAddItem(st.Name);
                                st.ItemId = ti.Id;
                                st.Update();
                            }
                        }
                    }

                    app.Update();
                }
            }

            return JsonRefresh("成功！");
        }
    }
}