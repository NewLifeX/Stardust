using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using static Stardust.Data.Nodes.RedisData;

namespace Stardust.Web.Areas.Redis.Controllers;

[Menu(40, false)]
[RedisArea]
public class RedisDataController : WebReadOnlyEntityController<RedisData>
{
    static RedisDataController()
    {
        ListFields.RemoveField("Id");
        ListFields.RemoveField("TopCommand");
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<RedisData> Search(Pager p)
    {
        // 纯 API 无视图，原 MVC 图表(ECharts)已删除

        var redisId = p["redisId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (redisId > 0)
        {
            // 最近10小时
            if (p.PageSize == 20 && redisId > 0) p.PageSize = 24 * 60;
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        var list = RedisData.Search(redisId, start, end, p["Q"], p);

        return list;
    }
}
