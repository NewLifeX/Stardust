using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.MySql;

[DisplayName("MySql监控")]
public class MySqlArea : AreaBase
{
    public MySqlArea() : base(nameof(MySqlArea).TrimEnd("Area")) { }
}
