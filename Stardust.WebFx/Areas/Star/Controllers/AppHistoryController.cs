using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    public class AppHistoryController : EntityController<AppHistory>
    {
        static AppHistoryController()
        {
            MenuOrder = 93;
        }
    }
}