using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(95, true)]
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
        }
    }
}