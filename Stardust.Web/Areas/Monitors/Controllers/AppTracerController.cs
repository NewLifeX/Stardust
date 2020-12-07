using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class AppTracerController : EntityController<AppTracer>
    {
        static AppTracerController() => MenuOrder = 90;

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

        protected override Boolean Valid(AppTracer entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            var act = type switch
            {
                DataObjectMethodType.Update => "修改",
                DataObjectMethodType.Insert => "添加",
                DataObjectMethodType.Delete => "删除",
                _ => type + "",
            };

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(act, entity);

            var err = "";
            try
            {
                return base.Valid(entity, type, post);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                throw;
            }
            finally
            {
                LogProvider.Provider.WriteLog(act, entity, err);
            }
        }
    }
}