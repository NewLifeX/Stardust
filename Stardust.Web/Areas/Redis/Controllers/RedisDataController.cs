using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using static Stardust.Data.Nodes.RedisData;

namespace Stardust.Web.Areas.Redis.Controllers;

[Menu(40, false)]
[RedisArea]
public class RedisDataController : ReadOnlyEntityController<RedisData>
{
    static RedisDataController()
    {
        ListFields.RemoveField("Id");
        ListFields.RemoveField("TopCommand");
    }

    protected override IEnumerable<RedisData> Search(Pager p)
    {
        PageSetting.EnableAdd = false;

        var redisId = p["redisId"].ToInt(-1);

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (redisId > 0)
        {
            // 最近10小时
            if (p.PageSize == 20 && redisId > 0) p.PageSize = 24 * 60;

            PageSetting.EnableNavbar = false;
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        var list = RedisData.Search(redisId, start, end, p["Q"], p);

        if (list.Count > 0)
        {
            // 绘制日期曲线图
            var node = RedisNode.FindById(redisId);
            if (redisId >= 0 && node != null)
            {
                var list2 = list.OrderBy(e => e.Id).ToList();

                var chart = new ECharts
                {
                    Title = new ChartTitle { Text = node.Name },
                    Height = 400,
                };
                chart.SetX(list2, _.CreateTime);
                chart.SetY("指标");
                chart.AddDataZoom();

                chart.AddLine(list2, _.Speed, null, true);
                chart.Add(list2, _.InputKbps);
                chart.Add(list2, _.OutputKbps);
                chart.Add(list2, _.ConnectedClients);
                chart.Add(list2, _.UsedMemory);
                chart.Add(list2, _.Keys);
                chart.SetTooltip();
                ViewBag.Charts = new[] { chart };
            }
        }

        return list;
    }
}