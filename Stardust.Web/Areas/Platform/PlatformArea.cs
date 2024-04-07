using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Platform;

[DisplayName("平台管理")]
[Menu(999, true, LastUpdate = "20240407")]
public class PlatformArea : AreaBase
{
    public PlatformArea() : base(nameof(PlatformArea).TrimEnd("Area")) { }
}