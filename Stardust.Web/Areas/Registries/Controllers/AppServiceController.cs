using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppServiceController : EntityController<AppService>
    {
        static AppServiceController()
        {
            MenuOrder = 75;
        }
    }
}