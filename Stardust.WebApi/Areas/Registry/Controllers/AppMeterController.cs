using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode;
using XCode.Membership;
using static Stardust.Data.AppMeter;

namespace Stardust.Web.Areas.Registry.Controllers;

/// <summary>应用性能。按应用客户端维度展示性能指标面板（吞吐量/响应时间/错误率）</summary>
[RegistryArea]
[Menu(0, false)]
public class AppMeterController : RegistryEntityController<AppMeter>
{
    static AppMeterController()
    {
        ListFields.RemoveField("Id");

        {
            var df = ListFields.GetField("ClientId") as ListField;
            df.Url = "/Registry/AppMeter?appId={AppId}&clientId={ClientId}";
        }
        {
            var df = ListFields.GetField("Source") as ListField;
            df.Url = "/Registry/AppMeter?appId={AppId}&clientId={ClientId}&source={Source}";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppMeter> Search(Pager p)
    {
        // 纯 API 无视图，原 MVC 图表(ECharts)已删除

        var appId = p["appId"].ToInt(-1);
        var clientId = p["clientId"];
        var source = p["source"];

        // 应用在线多IP时，只取第一个
        if (!clientId.IsNullOrEmpty())
        {
            var idx = clientId.IndexOf(',');
            if (idx > 0) clientId = clientId[..idx];
        }

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        if (appId > 0)
        {
            // 最近24小时
            if (p.PageSize == 20 && appId > 0)
            {
                p.PageSize = 1440;

                // 默认查询最近24小时。如果指定了应用，还需要根据应用心跳间隔来调整
                var app = App.FindById(appId);
                if (app != null && app.Period > 0)
                {
                    p.PageSize = 24 * 3600 / app.Period;
                }
            }

            //// 自动客户端
            //if (clientId.IsNullOrEmpty())
            //{
            //    var clients = GetClientIds(appId);
            //    if (clients != null && clients.Count > 0) clientId = clients.FirstOrDefault(e => e.Key != "null").Key;
            //}

            if (start.Year < 2000)
            {
                start = DateTime.Today;
                p["dtStart"] = start.ToFullString();
            }
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        var list = AppMeter.Search(appId, clientId, source, start, end, p["Q"], p);

        // 如果没有clientId，则可能列表数据里面只有一个，选择它，便于展示图表
        if (list.Count > 0 && clientId.IsNullOrEmpty())
        {
            var cs = list.Where(e => !e.ClientId.IsNullOrEmpty()).Select(e => e.ClientId).Distinct().ToList();
            if (cs.Count == 1) clientId = cs[0];
        }

        // 纯 API 无视图，原 MVC 图表(ECharts)已删除

        return list;
    }
}
