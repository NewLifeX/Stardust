using System;
using System.Collections.Generic;
using NewLife.Configuration;
using NewLife.Data;

namespace Stardust.Configs
{
    internal class StarHttpConfigProvider : HttpConfigProvider
    {
        protected override IDictionary<String, Object> GetAll()
        {
            var dic = base.GetAll();

            // 接收配置中心颁发的WorkerId
            if (dic.TryGetValue("NewLife.WorkerId", out var wid))
            {
                var id = wid.ToInt();
                if (id > 0) Snowflake.GlobalWorkerId = id;
            }

            return dic;
        }
    }
}