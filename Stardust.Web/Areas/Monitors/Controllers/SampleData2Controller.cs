using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class SampleData2Controller : ReadOnlyEntityController<SampleData2>
{
    static SampleData2Controller()
    {
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
}