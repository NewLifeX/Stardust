using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(0, false)]
public class AppLogController : ReadOnlyEntityController<AppClientLog>
{
    protected override AppClientLog Find(Object key) => AppClientLog.FindById(key.ToLong());

    protected override IEnumerable<AppClientLog> Search(Pager p)
    {
        PageSetting.EnableAdd = false;
        PageSetting.EnableNavbar = false;

        var appId = p["appId"].ToInt(-1);
        var clientId = p["clientId"];
        var threadId = p["threadId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (start.Year < 2000)
        {
            start = DateTime.Today;
            p["dtStart"] = start.ToString("yyyy-MM-dd");
        }

        return AppClientLog.Search(appId, clientId, threadId, start, end, p["Q"], p);
    }
}