using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Web.Areas.Redis.Controllers;

/// <summary>Redis消息队列。管理 Redis 消息队列的消费状态和积压情况</summary>
[Menu(30, false)]
[RedisArea]
public class RedisMessageQueueController : WebEntityController<RedisMessageQueue>
{
    static RedisMessageQueueController()
    {
        LogOnChange = true;

        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();
        ListFields.AddField("UpdateTime");
        ListFields.RemoveField("WebHook");

        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is RedisMessageQueue entity && !entity.TraceId.IsNullOrEmpty();
        //}
        ListFields.TraceUrl();
        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=Redis消息队列&linkId={Id}";
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<RedisMessageQueue> Search(Pager p)
    {
        var redisId = p["redisId"].ToInt(-1);

        var category = p["category"];
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return RedisMessageQueue.Search(redisId, category, start, end, p["Q"], p).Select(CropRow);
    }

    /// <summary>出参投影：克隆实体并置空 WebHook 明文，杜绝列表行泄漏（task-94 一期脱敏，OBS-3 CropPfx 同款语义：值级置 null）。
    /// 前端对该行 WebHook 零消费（RedisQueueCard 注释实证）；克隆为全新实例，不原地改写共享缓存中的原实体。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含 WebHook 明文的克隆实体</returns>
    private static RedisMessageQueue CropRow(RedisMessageQueue src)
    {
        var dst = new RedisMessageQueue();
        foreach (var fi in RedisMessageQueue.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.WebHook = null;
        return dst;
    }
}
