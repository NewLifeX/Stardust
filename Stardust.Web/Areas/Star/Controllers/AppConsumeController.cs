using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    [StarArea]
    public class AppConsumeController : EntityController<AppConsume>
    {
        static AppConsumeController()
        {
            MenuOrder = 73;
        }
    }
}