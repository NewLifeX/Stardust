using System;
using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Redis
{
    [DisplayName("Redis管理")]
    public class RedisArea : AreaBase
    {
        public RedisArea() : base(nameof(RedisArea).TrimEnd("Area")) { }

        static RedisArea() => RegisterArea<RedisArea>();
    }
}
