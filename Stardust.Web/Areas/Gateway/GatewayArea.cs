using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Gateway;

[DisplayName("网关管理")]
public class GatewayArea : AreaBase
{
    public GatewayArea() : base(nameof(GatewayArea).TrimEnd("Area")) { }
}