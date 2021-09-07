using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Redis.Controllers
{
    [RedisArea]
    public class RedisMessageQueueController : EntityController<RedisMessageQueue>
    {
        static RedisMessageQueueController()
        {
            MenuOrder = 30;

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

        protected override Boolean Valid(RedisMessageQueue entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(type + "", entity);

            var err = "";
            try
            {
                return base.Valid(entity, type, post);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                throw;
            }
            finally
            {
                if (type != DataObjectMethodType.Update) LogProvider.Provider.WriteLog(type + "", entity, err);
            }
        }
    }
}