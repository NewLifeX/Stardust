using System;
using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Configs
{
    [DisplayName("配置中心")]
    public class ConfigsArea : AreaBase
    {
        public ConfigsArea() : base(nameof(ConfigsArea).TrimEnd("Area")) { }

        static ConfigsArea() => RegisterArea<ConfigsArea>();
    }
}