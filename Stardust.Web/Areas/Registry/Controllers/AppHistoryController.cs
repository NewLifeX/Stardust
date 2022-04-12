using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(0, false)]
    public class AppHistoryController : ReadOnlyEntityController<AppHistory>
    {
        static AppHistoryController()
        {
            ListFields.RemoveField("Id");

            {
                var df = ListFields.GetField("TraceId") as ListField;
                df.DisplayName = "跟踪";
                df.Url = StarHelper.BuildUrl("{TraceId}");
                df.DataVisible = (e, f) => e is AppHistory entity && !entity.TraceId.IsNullOrEmpty();
            }

            {
                var df = ListFields.GetField("Client") as ListField;
                df.Url = "?appId={AppId}&client={Client}";
            }

            {
                var df = ListFields.GetField("Action") as ListField;
                df.Url = "?appId={AppId}&action={Action}";
            }
        }

        protected override IEnumerable<AppHistory> Search(Pager p)
        {
            //PageSetting.EnableAdd = false;
            //PageSetting.EnableNavbar = false;

            var appId = p["appId"].ToInt(-1);
            var client = p["client"];
            var action = p["action"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            //if (appId > 0 && start.Year < 2000) p["dtStart"] = (start = DateTime.Today).ToString("yyyy-MM-dd");

            return AppHistory.Search(appId, client, action, start, end, p["Q"], p);
        }
    }
}