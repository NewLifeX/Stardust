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
    [Menu(20)]
    [MonitorsArea]
    public class AlarmGroupController : EntityController<AlarmGroup>
    {
        static AlarmGroupController()
        {
            {
                var df = ListFields.AddListField("History", "CreateUser");
                df.DisplayName = "告警历史";
                df.Header = "告警历史";
                df.Url = "AlarmHistory?groupId={Id}";
            }
            {
                var df = ListFields.AddListField("Log", "CreateUser");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=告警组&linkId={Id}";
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

        protected override Boolean Valid(AlarmGroup entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(type + "", entity);

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
                if (type != DataObjectMethodType.Update) LogProvider.Provider.WriteLog(type + "", entity, err);
            }
        }
    }
}