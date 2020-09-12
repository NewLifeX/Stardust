using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    [StarArea]
    public class AppMeterController : EntityController<AppMeter>
    {
        static AppMeterController()
        {
            MenuOrder = 93;
        }
    }
}