using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    public class ServiceStatController : EntityController<ServiceStat>
    {
        static ServiceStatController()
        {
            MenuOrder = 75;
        }
    }
}