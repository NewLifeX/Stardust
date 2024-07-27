using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Registry;

[DisplayName("应用中心")]
[Menu(777, true, LastUpdate = "20240727")]
public class RegistryArea : AreaBase
{
    public RegistryArea() : base(nameof(RegistryArea).TrimEnd("Area")) { }

    static RegistryArea() => RegisterArea<RegistryArea>();
}