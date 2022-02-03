using System;
using System.Collections.Generic;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Web.Areas.Redis.Controllers
{
    [Menu(30)]
    [RedisArea]
    public class RedisMessageQueueController : EntityController<RedisMessageQueue>
    {
        static RedisMessageQueueController()
        {
            LogOnChange = true;

            ListFields.RemoveCreateField();
            ListFields.RemoveUpdateField();
            ListFields.AddField("UpdateTime");
            ListFields.RemoveField("WebHook");

            {
                var df = ListFields.AddListField("Log", "UpdateTime");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=Redis消息队列&linkId={Id}";
            }
        }

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