using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Web;
using Stardust.Data.Monitors;
using Stardust.Web.Models;
using XCode.Membership;

namespace Stardust.Web.Controllers
{
    public class TraceController : Controller
    {
        [Route("[controller]")]
        public ActionResult Index(String id, Pager pager)
        {
            if (id.IsNullOrEmpty()) throw new ArgumentNullException(nameof(id));

            var list = Search(id, pager);

            if (list.Count > 0)
            {
                var appName = list[0].AppName;
                if (appName.IsNullOrEmpty()) ViewBag.Title = $"{appName}调用链";
            }

            var model = new TraceViewModel
            {
                Page = pager,
                Data = list
            };

            return View("Index", model);
        }

        private IList<SampleData> Search(String traceId, Pager p)
        {
            // 指定追踪标识后，分页500
            if (!traceId.IsNullOrEmpty())
            {
                if (p.PageSize == 20) p.PageSize = 1000;
            }
            //if (p.Sort.IsNullOrEmpty()) p.OrderBy = SampleData._.Id.Desc();

            var start = DateTime.Today.AddDays(-30);
            var end = DateTime.Today;
            var list = SampleData.Search(-1, traceId, start, end, p);
            if (list.Count == 0)
            {
                // 如果是查看调用链，去备份表查一下
                var list2 = SampleData2.Search(traceId, null, p);
                if (list2.Count > 0)
                {
                    foreach (var item in list2)
                    {
                        var entity = new SampleData();
                        entity.CopyFrom(item);

                        list.Add(entity);
                    }
                }

                if (list.Count == 0) return list;
            }
            else
            {
                // 为临近边界的数据，查前一天
                var first = list.OrderBy(e => e.Id).First().CreateTime;
                if (first.Year > 2000 && first.Hour == 0 && first.Minute == 0 && first.Second <= 5)
                {
                    var date = first.Date.AddDays(-1);
                    var list2 = SampleData.Search(-1, traceId, date, date, p);
                    if (list2.Count > 0) (list as List<SampleData>).AddRange(list2);
                }

                if (list.Count < p.PageSize)
                {
                    var user = ManageProvider.User;

                    // 备份调用链，用于将来查询
                    SampleData2.Backup(traceId, list, user?.ID ?? 0, user + "");
                }
            }

            // 如果有traceId，则按照要求排序，深度搜索算法
            if (list.Count > 0)
            {
                var rs = new List<SampleData>();
                var stack = new Stack<SampleData>();

                // 找到所有parentId，包括空字符串，首次出现的parentId优先
                var pids = list.OrderByDescending(e => e.StartTime).Select(e => e.ParentId + "").Distinct().ToArray();
                // 找到顶级parentId，它所对应的span不存在，包括空字符串
                foreach (var item in pids.Where(e => !list.Any(y => y.SpanId == e)))
                {
                    // 这些parentId的子级，按照时间降序后入栈，它们作为一级树
                    foreach (var elm in list.Where(e => e.ParentId + "" == item).OrderByDescending(e => e.StartTime))
                    {
                        stack.Push(elm);
                    }
                }
                foreach (var item in stack)
                {
                    list.Remove(item);
                }

                // 依次弹出parentId，深度搜索
                var sd = stack.Pop();
                rs.Add(sd);
                var pid = sd.SpanId;
                while (true)
                {
                    // 当前span的下级，按时间降序入栈
                    var ps = list.Where(e => e.ParentId + "" == pid).OrderByDescending(e => e.StartTime).ToList();
                    foreach (var item in ps)
                    {
                        stack.Push(item);
                        list.Remove(item);
                    }

                    // 没有数据，跳出
                    if (stack.Count == 0) break;

                    // 出栈，加入结果，处理它的下级
                    if (stack.TryPop(out sd))
                    {
                        rs.Add(sd);
                        pid = sd.SpanId;
                    }
                }

                // 残留的异常数据
                rs.AddRange(list);

                list = rs;
            }

            return list;
        }
    }
}