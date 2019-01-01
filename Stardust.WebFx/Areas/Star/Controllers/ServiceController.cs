using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    public class ServiceController : EntityController<Service>
    {
        static ServiceController()
        {
            MenuOrder = 79;
        }
    }
}