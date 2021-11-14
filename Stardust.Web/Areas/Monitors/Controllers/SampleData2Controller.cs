using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class SampleData2Controller : ReadOnlyEntityController<SampleData2>
    {
        static SampleData2Controller()
        {
            MenuOrder = 49;

            ListFields.RemoveField("ID");
            ListFields.RemoveField("DataId");

            var df = ListFields.AddListField("trace", "TraceId");
            df.DisplayName = "追踪";
            df.Header = "追踪";
            df.Url = "/trace?id={TraceId}";
        }

        protected override IEnumerable<SampleData2> Search(Pager p)
        {
            var traceId = p["traceId"];

            // 指定追踪标识后，分页500
            if (!traceId.IsNullOrEmpty())
            {
                if (p.PageSize == 20) p.PageSize = 500;
            }
            if (p.Sort.IsNullOrEmpty()) p.OrderBy = SampleData2._.Id.Desc();

            return SampleData2.Search(traceId, p["Q"], p);
        }

        /// <summary>菜单不可见</summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        protected override IDictionary<System.Reflection.MethodInfo, Int32> ScanActionMenu(XCode.Membership.IMenu menu)
        {
            if (menu.Visible)
            {
                menu.Visible = false;
                (menu as XCode.IEntity).Update();
            }

            return base.ScanActionMenu(menu);
        }
    }
}