using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Platform
{
    [DisplayName("平台管理")]
    public class PlatformArea : AreaBase
    {
        public PlatformArea() : base(nameof(PlatformArea).TrimEnd("Area")) { }
    }
}