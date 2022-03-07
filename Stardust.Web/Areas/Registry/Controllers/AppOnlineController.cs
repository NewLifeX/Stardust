using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(95)]
    public class AppOnlineController : EntityController<AppOnline>
    {
        static AppOnlineController()
        {
            ListFields.RemoveField("Token");

            {
                var df = ListFields.AddListField("Meter", null, "PingCount");
                df.DisplayName = "性能";
                df.Header = "性能";
                df.Url = "AppMeter?appId={AppId}";
            }
            {
                var df = ListFields.GetField("TraceId") as ListField;
                df.DisplayName = "跟踪";
                df.Url = StarHelper.BuildUrl("{TraceId}");
                df.DataVisible = (e, f) => e is AppOnline entity && !entity.TraceId.IsNullOrEmpty();
            }
        }

        protected override IEnumerable<AppOnline> Search(Pager p)
        {
            var appId = p["appId"].ToInt(-1);
            var category = p["category"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return AppOnline.Search(appId, category, start, end, p["Q"], p);
        }
    }
}