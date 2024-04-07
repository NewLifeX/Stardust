using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Configs;

[DisplayName("配置中心")]
[Menu(666, true, LastUpdate = "20240407")]
public class ConfigsArea : AreaBase
{
    public ConfigsArea() : base(nameof(ConfigsArea).TrimEnd("Area")) { }

    static ConfigsArea() => RegisterArea<ConfigsArea>();
}