using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Security;
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

            // id可能不是traceId，而是traceParent
            var ss = id.Split('-');
            if (ss.Length == 4) id = ss[1];

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

        [Route("[action]")]
        public ActionResult Graph(String id, Pager pager)
        {
            if (id.IsNullOrEmpty()) throw new ArgumentNullException(nameof(id));

            // id可能不是traceId，而是traceParent
            var ss = id.Split('-');
            if (ss.Length == 4) id = ss[1];

            var list = Search(id, pager);

            // 每个应用的第一个节点，必须以App分类展示，其它节点才可以定制化
            var appids = new List<Int32>();

            // 解析得到关系数据
            var cats = new List<String>();
            //var cats = new List<String> { "App", "http", "db", "redis" };
            var nodes = new List<GraphNode>();
            var links = new Dictionary<String, GraphLink>();
            foreach (var item in list)
            {
                var cat = "App";
                var name = item.AppName;
                var ti = item.TraceItem;
                if (appids.Contains(item.AppId) && ti != null && ti.Kind.EqualIgnoreCase("http", "db", "redis", "mq", "mqtt", "modbus"))
                {
                    cat = ti.Kind;
                    name = ti.DisplayName ?? ti.Name;
                    var ns = name.Split(':');

                    // 特殊处理
                    switch (ti.Kind)
                    {
                        case "http":
                            var p = name.IndexOf('/', "https://".Length);
                            name = p > 0 ? name[..p] : name;
                            break;
                        case "db":
                            if (ns.Length >= 2) name = ns[1];
                            break;
                        case "mq":
                            if (ns.Length >= 2) name = $"{ns[0]}:{ns[1]}";
                            if (ns.Length >= 3 && ns[0] == "redismq" && ns[1] == "Add")
                                name = $"{ns[0]}:{ns[2]}";
                            else if (ns.Length >= 4 && ns[0] == "redismq" && ns[2] == "Add")
                                name = $"{ns[0]}:{ns[3]}";
                            break;
                        case "modbus":
                            name = ns.Length >= 3 ? ns[1] : "IoTDevice";
                            break;
                        default:
                            if (ns.Length >= 2) name = $"{ns[0]}:{ns[1]}";
                            break;
                    }
                }
                if (!appids.Contains(item.AppId)) appids.Add(item.AppId);

                if (name == null) name = item.AppName;
                item["node_name"] = name;

                // 分类
                var idx = cats.IndexOf(cat);
                if (idx < 0)
                {
                    cats.Add(cat);
                    idx = cats.Count - 1;
                }

                // 节点
                var node = nodes.FirstOrDefault(e => e.Name == name);
                if (node == null)
                {
                    node = new GraphNode
                    {
                        Id = item.Id + "",
                        Name = name,
                        Value = item.Cost,
                        Category = idx,

                        SymbolSize = item.Cost,
                        //X = Rand.Next(20, 100),
                        //Y = Rand.Next(20, 100),
                    };
                    nodes.Add(node);
                }
                else
                {
                    node.Value += item.Cost;
                    node.SymbolSize += item.Cost;
                }
                item["node_id"] = node.Id;

                // 关系
                var parent = list.FirstOrDefault(e => e.SpanId == item.ParentId);
                if (parent != null)
                {
                    var src = parent["node_id"] + "";
                    var dst = item["node_id"] + "";
                    var key = $"{src}-{dst}";
                    if (src != dst && !links.ContainsKey(key)) links.Add(key, new GraphLink { Source = src, Target = dst });
                }
            }

            // 处理图标大小
            var maxCost = nodes.Max(e => e.Value);
            var minCost = nodes.Min(e => e.Value);
            var len = maxCost - minCost;
            if (len <= 0) len = 1;
            foreach (var node in nodes)
            {
                var cost = (Int32)Math.Round(100 * (Double)(node.Value - minCost) / (maxCost - minCost));
                node.SymbolSize = cost < 40 ? 40 : cost;
            }

            // 分类图标
            var cts = new List<GraphCategory>();
            foreach (var item in cats)
            {
                var cat = new GraphCategory
                {
                    Name = item,
                };
                cat.Symbol = item switch
                {
                    "App" => "image:///icons/app.svg",
                    "http" => "image:///icons/http.svg",
                    "db" => "image:///icons/db.svg",
                    "redis" => "image:///icons/redis.svg",
                    "mq" => "image:///icons/mq.svg",
                    "mqtt" => "image:///icons/mqtt.svg",
                    "modbus" => "image:///icons/modbus.svg",
                    _ => "circle",
                };
                cts.Add(cat);
            }

            var model = new GraphViewModel
            {
                TraceId = id,
                Title = "关系图",
                Layout = pager["layout"],
                Categories = cts.ToArray(),
                Links = links.Values.ToArray(),
                Nodes = nodes.ToArray(),
            };
            if (model.Layout.IsNullOrEmpty()) model.Layout = "force";

            if (list.Count > 0)
            {
                var appName = list[0].AppName;
                if (appName.IsNullOrEmpty()) model.Title = $"{appName}关系图";
            }

            return View(model);
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

                //!!! 判断每个应用的第一次出现，根据父子关系，整体调整应用的时间偏移
                // 每个应用的时间偏移量
                var dic = new Dictionary<Int32, Int32>();

                // 依次弹出parentId，深度搜索
                var parent = stack.Pop();
                rs.Add(parent);
                var pid = parent.SpanId;
                while (true)
                {
                    // 当前span的下级，按时间降序入栈
                    var ps = list.Where(e => e.ParentId + "" == pid).OrderByDescending(e => e.StartTime).ToList();
                    foreach (var item in ps)
                    {
                        stack.Push(item);
                        list.Remove(item);

                        // 深度
                        item.Level = parent.Level + 1;

                        // 如果子级时间小于父级，可能是跨应用时间差，强行调整
                        if (parent.AppId != item.AppId)
                        {
                            if (!dic.TryGetValue(parent.AppId, out var parentTs)) parentTs = 0;

                            // 负数合理，正数不合理
                            var ts = (Int32)(parent.StartTime - item.StartTime);
                            if (ts > 0)
                            {
                                if (!dic.TryGetValue(item.AppId, out var itemTs) || ts > itemTs)
                                    dic[item.AppId] = itemTs = ts + parentTs;
                            }
                        }
                    }

                    // 没有数据，跳出
                    if (stack.Count == 0) break;

                    // 出栈，加入结果，处理它的下级
                    if (stack.TryPop(out parent))
                    {
                        rs.Add(parent);
                        pid = parent.SpanId;
                    }
                }

                // 残留的异常数据
                rs.AddRange(list);

                // 各应用整体后移
                foreach (var item in rs)
                {
                    if (dic.TryGetValue(item.AppId, out var ts))
                    {
                        item.StartTime += ts;
                        item.EndTime += ts;
                    }
                }

                list = rs;
            }

            return list;
        }
    }
}