using NewLife.Cube;
using Stardust.Data;

namespace Stardust.Web.Areas.Star.Controllers
{
    [StarArea]
    public class AppController : EntityController<App>
    {
        static AppController()
        {
            MenuOrder = 99;
        }
    }
}