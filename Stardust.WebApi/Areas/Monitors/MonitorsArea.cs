using System.ComponentModel;
using NewLife;
using NewLife.Cube;
using XCode;

namespace Stardust.Web.Areas.Monitors;

[DisplayName("监控中心")]
[Menu(444, true, LastUpdate = "20240407")]
public class MonitorsArea : AreaBase
{
    public MonitorsArea() : base(nameof(MonitorsArea).TrimSuffix("Area")) { }

    static MonitorsArea() => RegisterArea<MonitorsArea>();
}

[MonitorsArea]
public class MonitorsEntityController<T> : WebEntityController<T> where T : Entity<T>, new()
{
}
