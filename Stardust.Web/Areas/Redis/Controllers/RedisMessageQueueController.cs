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

            var act = type switch
            {
                DataObjectMethodType.Update => "修改",
                DataObjectMethodType.Insert => "添加",
                DataObjectMethodType.Delete => "删除",
                _ => type + "",
            };

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(act, entity);

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
                LogProvider.Provider.WriteLog(act, entity, err);
            }
        }
    }
}