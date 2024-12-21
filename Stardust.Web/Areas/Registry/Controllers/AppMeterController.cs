using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using XCode;
using XCode.Membership;
using static Stardust.Data.AppMeter;

namespace Stardust.Web.Areas.Registry.Controllers;

[RegistryArea]
[Menu(0, false)]
public class AppMeterController : EntityController<AppMeter>
{
    static AppMeterController()
    {
        ListFields.RemoveField("Id");

        {
            var df = ListFields.GetField("ClientId") as ListField;
            df.Url = "/Registry/AppMeter?appId={AppId}&clientId={ClientId}";
        }
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        var appId = GetRequest("appId").ToInt(-1);
        if (appId > 0)
        {
            PageSetting.NavView = "_App_Nav";
            PageSetting.EnableNavbar = false;
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var appId = GetRequest("appId").ToInt(-1);
            if (appId > 0) fields.RemoveField("AppName");
        }

        return fields;
    }

    protected override IEnumerable<AppMeter> Search(Pager p)
    {
        PageSetting.EnableAdd = false;

        var appId = p["appId"].ToInt(-1);
        var clientId = p["clientId"];

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
            if (p.PageSize == 20 && appId > 0) p.PageSize = 1440;

            //// 自动客户端
            //if (clientId.IsNullOrEmpty())
            //{
            //    var clients = GetClientIds(appId);
            //    if (clients != null && clients.Count > 0) clientId = clients.FirstOrDefault(e => e.Key != "null").Key;
            //}

            PageSetting.EnableNavbar = false;

            if (start.Year < 2000)
            {
                start = DateTime.Today;
                p["dtStart"] = start.ToFullString();
            }
        }

        if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

        var list = AppMeter.Search(appId, clientId, start, end, p["Q"], p);

        // 如果没有clientId，则可能列表数据里面只有一个，选择它，便于展示图表
        if (list.Count > 0 && clientId.IsNullOrEmpty())
        {
            var cs = list.Where(e => !e.ClientId.IsNullOrEmpty()).Select(e => e.ClientId).Distinct().ToList();
            if (cs.Count == 1) clientId = cs[0];
        }

        if (list.Count > 0 && !clientId.IsNullOrEmpty())
        {
            // 绘制日期曲线图
            var app = App.FindById(appId);
            if (appId >= 0 && app != null)
            {
                var list2 = list.OrderBy(e => e.Id).ToList();

                var chart = new ECharts
                {
                    Title = new ChartTitle { Text = app.Name + "#" + clientId },
                    Height = 400,
                };
                chart.SetX(list2, _.Time, e => (e.Time.Year > 2000 ? e.Time : e.CreateTime).ToFullString());
                //chart.SetY("指标");
                chart.YAxis = new[] {
                    new { name = "指标", type = "value" },
                    new { name = "百分比（%）", type = "value" }
                };
                chart.AddDataZoom();
                chart.AddLine(list2, _.Memory, null, true);

                var line = chart.AddLine(list2, _.CpuUsage, null, true);
                line.YAxisIndex = 1;

                chart.Add(list2, _.Threads);
                chart.Add(list2, _.AvailableThreads);
                chart.Add(list2, _.PendingItems);
                chart.Add(list2, _.CompletedItems);
                chart.Add(list2, _.Handles);
                chart.Add(list2, _.Connections);
                chart.Add(list2, _.HeapSize);
                chart.Add(list2, _.GCCount);

                chart.SetTooltip();
                ViewBag.Charts = new[] { chart };
            }
        }

        return list;
    }
}