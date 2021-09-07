using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    public class AppOnlineController : EntityController<AppOnline>
    {
        static AppOnlineController()
        {
            MenuOrder = 95;

            {
                var df = ListFields.AddListField("Meter", null, "PingCount");
                df.DisplayName = "性能";
                df.Header = "性能";
                df.Url = "AppMeter?appId={AppId}";
            }
        }
    }
}