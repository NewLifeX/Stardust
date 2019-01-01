using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    public class IpStatController : EntityController<IpStat>
    {
        static IpStatController()
        {
            MenuOrder = 59;
        }
    }
}