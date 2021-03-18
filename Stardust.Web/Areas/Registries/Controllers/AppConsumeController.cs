using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppConsumeController : EntityController<AppConsume>
    {
        static AppConsumeController()
        {
            MenuOrder = 73;
        }
    }
}