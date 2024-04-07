using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Monitors;

[DisplayName("监控中心")]
[Menu(444, true, LastUpdate = "20240407")]
public class MonitorsArea : AreaBase
{
    public MonitorsArea() : base(nameof(MonitorsArea).TrimEnd("Area")) { }

    static MonitorsArea() => RegisterArea<MonitorsArea>();
}