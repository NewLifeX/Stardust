using System;
using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Registries
{
    [DisplayName("注册中心")]
    public class RegistryArea : AreaBase
    {
        public RegistryArea() : base(nameof(RegistryArea).TrimEnd("Area")) { }

        static RegistryArea() => RegisterArea<RegistryArea>();
    }
}