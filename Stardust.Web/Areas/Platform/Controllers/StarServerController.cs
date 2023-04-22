using System.ComponentModel;
using NewLife.Cube;
using Stardust.Server;
using Stardust.Web.Areas.Platform;

namespace Stardust.Web.Areas.Platform.Controllers
{
    /// <summary>平台设置控制器</summary>
    [DisplayName("平台设置")]
    [PlatformArea]
    public class StarServerController : ConfigController<StarServerSetting>
    {
    }
}
