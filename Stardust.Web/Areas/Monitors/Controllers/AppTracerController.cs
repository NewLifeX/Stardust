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

            p.RetrieveState = true;

            return AppTracer.Search(category, enable, start, end, p["Q"], p);
        }

        protected override Int32 OnDelete(AppTracer entity)
        {
            using var tran = AppTracer.Meta.CreateTrans();

            var rs = base.OnDelete(entity);

            var list = AppDayStat.FindAllByAppId(entity.ID);
            list.Delete();

            //var list2 = TraceDayStat.FindAllByAppId(entity.ID);
            //list2.Delete();
            TraceDayStat.DeleteByAppAndItem(entity.ID, 0);

            tran.Commit();

            return rs;
        }

        protected override Boolean Valid(AppTracer entity, DataObjectMethodType type, Boolean post)
        {
            var rs = base.Valid(entity, type, post);

            if (post && type == DataObjectMethodType.Delete)
            {
                var list = AppDayStat.FindAllByAppId(entity.ID);
                list.Delete();

                var list2 = TraceItem.FindAllByApp(entity.ID);
                list2.Delete();

                TraceDayStat.DeleteByAppAndItem(entity.ID, 0);
            }

            return rs;
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
                        app.Days = list.DistinctBy(e => e.StatDate.Date).Count();
                        app.Total = list.Sum(e => e.Total);
                    }

                    app.ItemCount = app.TraceItems.Count(e => e.Enable);
                    app.Update();
                }
            }

            return JsonRefresh("成功！");
        }

        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult FixOldData()
        {
            foreach (var item in SelectKeys)
            {
                var app = AppTracer.FindByID(item.ToInt());
                if (app != null)
                {
                    XTrace.WriteLine("修正旧数据 {0}/{1}", app.Name, app.ID);

                    {
                        var list = TraceDayStat.FindAllByAppId(app.ID);
                        foreach (var st in list)
                        {
                            var ti = app.GetOrAddItem(st.Name);
                            if (st.ItemId == 0 && !st.Name.IsNullOrEmpty())
                            {
                                st.ItemId = ti.Id;
                                st.SaveAsync();
                            }
                            if (ti.CreateTime.Year < 2000 || ti.CreateTime > st.CreateTime)
                            {
                                ti.CreateTime = st.CreateTime;
                                ti.SaveAsync(3_000);
                            }
                        }

                        app.Days = list.DistinctBy(e => e.StatDate.Date).Count();
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
                                st.SaveAsync();
                            }
                        }
                    }

                    app.ItemCount = app.TraceItems.Count(e => e.Enable);
                    app.Update();
                }
            }

            return JsonRefresh("成功！");
        }
    }
}