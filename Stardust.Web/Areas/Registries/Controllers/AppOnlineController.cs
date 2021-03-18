using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppOnlineController : EntityController<AppOnline>
    {
        static AppOnlineController()
        {
            MenuOrder = 95;
        }
    }
}