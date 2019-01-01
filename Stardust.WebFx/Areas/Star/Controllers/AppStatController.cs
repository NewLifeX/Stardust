using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    public class AppStatController : EntityController<AppStat>
    {
        static AppStatController()
        {
            MenuOrder = 91;
        }
    }
}