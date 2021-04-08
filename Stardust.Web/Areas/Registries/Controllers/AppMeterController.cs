using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Web;
using Stardust.Data;
using XCode;
using XCode.Membership;
using static Stardust.Data.AppMeter;

namespace Stardust.Web.Areas.Registries.Controllers
{
    [RegistryArea]
    public class AppMeterController : EntityController<AppMeter>
    {
        static AppMeterController()
        {
            MenuOrder = 93;

            ListFields.RemoveField("Id");
        }

        protected override IEnumerable<AppMeter> Search(Pager p)
        {
            PageSetting.EnableAdd = false;

            var appId = p["appId"].ToInt(-1);
            var clientId = p["clientId"];

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            if (appId > 0)
            {
                // 最近24小时
                if (p.PageSize == 20 && appId > 0) p.PageSize = 1440;

                // 自动客户端
                if (clientId.IsNullOrEmpty())
                {
                    var clients = AppMeter.GetClientIds(appId);
                    if (clients != null && clients.Count > 0) clientId = clients.FirstOrDefault(e => e.Key != "null").Key;
                }

                PageSetting.EnableNavbar = false;
            }

            if (p.Sort.IsNullOrEmpty()) p.OrderBy = _.Id.Desc();

            var list = AppMeter.Search(appId, clientId, start, end, p["Q"], p);

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
                    chart.SetX(list2, _.CreateTime, e => e.CreateTime.ToString("HH:mm"));
                    chart.SetY("指标");
                    chart.AddLine(list2, _.Memory, null, true);
                    chart.Add(list2, _.Threads);
                    chart.Add(list2, _.Handles);
                    chart.Add(list2, _.Connections);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
            }

            return list;
        }

        /// <summary>菜单不可见</summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        protected override IDictionary<MethodInfo, Int32> ScanActionMenu(IMenu menu)
        {
            if (menu.Visible)
            {
                menu.Visible = false;
                (menu as IEntity).Update();
            }

            return base.ScanActionMenu(menu);
        }
    }
}