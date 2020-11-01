using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class RedisMessageQueueController : ReadOnlyEntityController<RedisMessageQueue>
    {
        static RedisMessageQueueController() => MenuOrder = 30;

        protected override IEnumerable<RedisMessageQueue> Search(Pager p)
        {
            var redisId = p["redisId"].ToInt(-1);

            var category = p["category"];
            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return RedisMessageQueue.Search(redisId, category, start, end, p["Q"], p);
        }
    }
}