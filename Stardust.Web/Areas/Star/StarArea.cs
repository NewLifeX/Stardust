using System;
using System.ComponentModel;
using NewLife.Cube;

namespace Stardust.Web.Areas.Star
{
    [DisplayName("星尘")]
    public class StarArea : AreaBase
    {
        public StarArea() : base(nameof(StarArea).TrimEnd("Area")) { }

        static StarArea() => RegisterArea<StarArea>();
    }
}