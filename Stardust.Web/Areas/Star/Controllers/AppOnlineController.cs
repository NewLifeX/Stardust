using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    [StarArea]
    public class AppOnlineController : EntityController<AppOnline>
    {
        static AppOnlineController()
        {
            MenuOrder = 95;
        }
    }
}