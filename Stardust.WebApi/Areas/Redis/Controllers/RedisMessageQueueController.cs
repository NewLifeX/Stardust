using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
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

        return RedisMessageQueue.Search(redisId, category, start, end, p["Q"], p);
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropRow，语义逐键保真）：
    /// 列表行/导出 WebHook 值级置 null（键不删，task-94 一期脱敏）；详情/写视图不掩码（原行为：无 override）。
    /// 前端对该行 WebHook 零消费（RedisQueueCard 注释实证）。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["WebHook"] = null };

    /// <summary>仅列表/导出掩码（导出经掩码视图回退=List），详情/写视图不掩码（原行为）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => view == ViewKinds.List ? _masks : null;
}
