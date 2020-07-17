using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Charts;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Threading;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeStat;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeStatController : EntityController<NodeStat>
    {
        static NodeStatController()
        {
            MenuOrder = 30;

            // 计算统计
            _timer = new TimerX(DoNodeStat, null, 10_000, 60_000) { Async = true };
            _timerReport = new TimerX(DoReport, null, DateTime.Today.AddHours(8), 12 * 3600 * 1000) { Async = true };

            //// 先来一次
            //_timerReport.SetNext(10_000);
        }

        protected override IEnumerable<NodeStat> Search(Pager p)
        {
            var areaId = p["areaId"].ToInt(-1);
            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            // 默认排序
            if (areaId >= 0 && start.Year < 2000 && p.Sort.IsNullOrEmpty())
            {
                start = DateTime.Today.AddDays(-30);
                p["dtStart"] = start.ToString("yyyy-MM-dd");

                p.Sort = NodeStat.__.StatDate;
                p.Desc = false;
                p.PageSize = 100;

                //// 默认全国
                //if (areaId < 0) areaId = 0;
            }

            var list = NodeStat.Search(areaId, start, end, p["Q"], p);

            if (list.Count > 0)
            {
                var hasDate = start.Year > 2000 || end.Year > 2000;
                // 绘制日期曲线图
                var ar = Area.FindByID(areaId);
                if (areaId >= 0)
                {
                    var chart = new ECharts
                    {
                        Title = new ChartTitle { Text = ar + "" },
                        Height = 400,
                    };
                    chart.SetX(list, _.StatDate, e => e.StatDate.ToString("MM-dd"));
                    chart.SetY("数量");
                    chart.AddLine(list, _.Total, null, true);
                    chart.Add(list, _.Actives);
                    chart.Add(list, _.T7Actives);
                    chart.Add(list, _.T30Actives);
                    chart.Add(list, _.News);
                    chart.Add(list, _.T7News);
                    chart.Add(list, _.T30News);
                    chart.Add(list, _.Registers);
                    chart.Add(list, _.MaxOnline);
                    chart.SetTooltip();
                    ViewBag.Charts = new[] { chart };
                }
                // 指定日期后，绘制饼图
                if (hasDate && areaId < 0)
                {
                    var w = 400;
                    var h = 300;

                    var chart0 = new ECharts { Width = w, Height = h };
                    chart0.Add(list, _.Total, "pie", e => new { name = e.ProvinceName, value = e.Total });

                    var chart1 = new ECharts { Width = w, Height = h };
                    chart1.Add(list, _.Actives, "pie", e => new { name = e.ProvinceName, value = e.Actives });

                    var chart2 = new ECharts { Width = w, Height = h };
                    chart2.Add(list, _.News, "pie", e => new { name = e.ProvinceName, value = e.News });

                    var chart3 = new ECharts { Width = w, Height = h };
                    chart3.Add(list, _.Registers, "pie", e => new { name = e.ProvinceName, value = e.Registers });

                    var chart4 = new ECharts { Width = w, Height = h };
                    chart4.Add(list, _.MaxOnline, "pie", e => new { name = e.ProvinceName, value = e.MaxOnline });

                    ViewBag.Charts2 = new[] { chart0, chart1, chart2, chart3, chart4 };
                }
            }

            return list;
        }

        private static readonly TimerX _timer;
        private static void DoNodeStat(Object state)
        {
            var date = DateTime.Today;

            var p = Parameter.GetOrAdd(0, "统计", "节点日统计", new DateTime(date.Year, date.Month, 1).ToString("yyyy-MM-dd"));
            date = p.GetValue().ToDateTime();

            while (date <= DateTime.Today)
            {
                NodeStat.ProcessDate(date);

                date = date.AddDays(1);
            }

            // 保存位置
            p.SetValue(date.AddDays(-1));
            p.Save();
        }

        private static readonly TimerX _timerReport;
        private static void DoReport(Object state)
        {
            // 昨天的所有统计数据
            var dt = DateTime.Now;
            if (dt.Hour < 18) dt = dt.AddDays(-1);
            var list = NodeStat.FindAllByDate(dt.Date);
            list = list.OrderByDescending(e => e.Total).ToList();
            if (list.Count == 0) return;

            var name = Environment.MachineName;

            var sb = new StringBuilder();
            sb.Append($"星尘[{dt:MM-dd}@{name}]报告（今天/7天/30天）：\n");
            foreach (var item in list)
            {
                var pname = item.AreaID <= 0 ? "全国" : item.ProvinceName?.Trim();
                sb.Append($"[{pname}] 总数{item.Total}，活跃{item.Actives}/{item.T7Actives}/{item.T30Actives}，新增{item.News}/{item.T7News}/{item.T30News}，最高在线{item.MaxOnline}");
                if (item.MaxOnlineTime.Year > 2000) sb.Append($" [{item.MaxOnlineTime.ToFullString("")}]");
                sb.Append("\n");
            }

            var msg = sb.ToString();
            XTrace.WriteLine(msg);

            // 发钉钉
            //var token = "83694ec8aa5c1b3337cbda5f576692e7f7e35343ef2e58d68ff399dd77a7017c";
            var p = Parameter.GetOrAdd(0, "统计", "钉钉令牌");
            var token = p.GetValue() + "";
            if (token.IsNullOrEmpty()) return;

            try
            {
                SendDingTalk(token, msg);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        private static void SendDingTalk(String access_token, String content)
        {
            var action = $"robot/send?access_token={access_token}";
            var model = new { msgtype = "text", text = new { content } };

            /*
             * {"errmsg":"ok","errcode":0}
             * {"errmsg":"param error","errcode":300001}
             */

            var client = new HttpClient
            {
                BaseAddress = new Uri("https://oapi.dingtalk.com")
            };
            client.PostAsync<String>(action, model);
        }
    }
}