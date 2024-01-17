using System.ComponentModel;
using NewLife.Cube;
using Stardust.Server;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>平台设置控制器</summary>
[Menu(10, true)]
[DisplayName("平台设置")]
[PlatformArea]
public class StarServerController : ConfigController<StarServerSetting>
{
}
