using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Remoting;
using Stardust.Data.Monitors;
using Stardust.Server;
using Stardust.Web.Models;
using XCode.Membership;

namespace Stardust.Web.Controllers;

/// <summary>调用链查看。按追踪编号查看完整调用链与关系图</summary>
/// <remarks>
/// 对照 MVC 真源 <c>Stardust.Web/Controllers/TraceController.cs</c> 抽取数据查询成 JSON 接口（2026-11-04 监控调用链块 WebApi 补齐）：
/// MVC 的 Index（调用链时间轴/日志详情）本质是「数据查询+页面渲染」，新控制台已有等价页面
/// （/trace/:traceId 全局路由，由 GET /api/Monitors/SampleData?traceId= 承载），不造重复接口；
/// Graph（关系图）前端无等价页面，这里把关系图节点边数据（GraphViewModel）抽成 GET /api/trace/graph。
/// 私有 Search 与 MVC 逐行一致：主表空时回查备份表 SampleData2、临近边界补前一天、
/// 登录态（archive）时写备份 Backup 并更新埋点最后访问 TraceId、深度搜索排序。
/// 访问判定等价 MVC TraceAuthorizeAttribute：登录用户通过；未登录且 TraceAnonymous 开启时只读放行（不写备份）；否则 401。
/// 分享令牌（Share）不补：依赖 MVC 宿主登录会话与分享页面体系，令牌消费端为 MVC /trace?token= 页面。
/// </remarks>
[Route("api/[controller]/[action]")]
public class TraceController : ControllerBase
{
    /// <summary>调用链关系图。按 TraceId（可逗号分隔多个，支持 traceparent 四段格式）返回关系图节点边数据</summary>
    /// <param name="id">追踪编号，多个以逗号分隔</param>
    /// <param name="layout">布局：force（力导向，默认）/ circular（关系环）</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">页大小。默认20时放大为10000（与 MVC Pager 默认一致）</param>
    /// <returns>{code:0, data:{traceId,title,layout,categories,nodes,links}}</returns>
    public ActionResult Graph(String id, String layout, Int32 pageIndex = 1, Int32 pageSize = 20)
    {
        // 访问判定：登录用户 > 匿名开关；API 无登录页重定向，未登录且未开启匿名直接 401
        var signedIn = ManageProvider.User != null;
        if (!signedIn && !StarServerSetting.Current.TraceAnonymous)
            return Unauthorized(new ApiResponse<Object> { Code = ApiCode.Unauthorized, Message = L.T("Not logged in.") });

        // 未登录时只读：不写备份表，也不更新埋点最后访问的追踪编号
        var archive = signedIn;

        var traceIds = ParseTraceIds(id);
        if (traceIds.Length == 0) throw new ArgumentNullException(nameof(id));
        var traceId = traceIds.Join(",");

        if (pageSize <= 0) pageSize = 20;
        var p = new PageParameter { PageIndex = pageIndex, PageSize = pageSize };

        var list = Search(traceId, p, archive);

        // 每个应用的第一个节点，必须以App分类展示，其它节点才可以定制化
        var appids = new List<Int32>();

        // 解析得到关系数据
        var cats = new List<String>();
        var nodes = new List<GraphNode>();
        var links = new Dictionary<String, GraphLink>();
        var traceDatas = new Dictionary<Int64, TraceData>();
        foreach (var item in list)
        {
            var cat = "App";
            var name = item.AppName;
            var ti = item.TraceItem;
            if (appids.Contains(item.AppId) && ti != null && ti.Kind.EqualIgnoreCase("api", "http", "rps", "rpc", "db", "redis", "mq", "event", "mqtt", "modbus", "biz", "other"))
            {
                cat = ti.Kind;
                name = ti.DisplayName ?? ti.Name;
                var ns = name.Split(':');

                // 特殊处理
                switch (ti.Kind)
                {
                    case "http":
                        var pos = name.IndexOf('/', "https://".Length);
                        name = pos > 0 ? name[..pos] : name;
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

            name ??= item.AppName;

            // 关联TraceData，获取NodeName等信息
            if (!traceDatas.TryGetValue(item.DataId, out var td))
            {
                td = TraceData.FindById(item.DataId);
                traceDatas.Add(item.DataId, td);
            }
            if (td != null && !td.NodeName.IsNullOrEmpty())
                name = name + "@" + td.NodeName;

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
        var maxCost = nodes.Count == 0 ? 0 : nodes.Max(e => e.Value);
        var minCost = nodes.Count == 0 ? 0 : nodes.Min(e => e.Value);
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
                "http" or "api" or "rps" or "rpc" => "image:///icons/http.svg",
                "db" => "image:///icons/db.svg",
                "redis" => "image:///icons/redis.svg",
                "mq" or "event" => "image:///icons/mq.svg",
                "mqtt" => "image:///icons/mqtt.svg",
                "modbus" => "image:///icons/modbus.svg",
                _ => "circle",
            };
            cts.Add(cat);
        }

        var model = new GraphViewModel
        {
            TraceId = traceId,
            Title = "关系图",
            Layout = layout,
            Categories = cts.ToArray(),
            Links = links.Values.ToArray(),
            Nodes = nodes.ToArray(),
        };
        if (model.Layout.IsNullOrEmpty()) model.Layout = "force";

        model.Title = list.Count > 0 && !list[0].AppName.IsNullOrEmpty() ? $"{list[0].AppName}关系图" : "关系图";

        return new JsonResult(new ApiResponse<Object> { Code = 0, Data = model });
    }

    /// <summary>解析追踪编号。支持逗号分隔多个与W3C traceparent四段格式，去重；无有效编号返回空数组（对照 TraceAccessPolicy.ParseTraceIds）</summary>
    private static String[] ParseTraceIds(String id)
    {
        if (id.IsNullOrEmpty()) return [];

        var list = new List<String>();
        foreach (var item in id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // traceparent: 00-{traceId}-{spanId}-{flags}
            var ss = item.Split('-');
            var traceId = ss.Length == 4 ? ss[1] : item;
            if (!traceId.IsNullOrEmpty()) list.Add(traceId);
        }

        return list.Distinct().ToArray();
    }

    /// <summary>按追踪编号查询采样片段。与 MVC 根 TraceController.Search 逐行一致</summary>
    /// <param name="traceId">追踪编号，逗号分隔多个</param>
    /// <param name="p">分页参数</param>
    /// <param name="archive">是否归档。登录用户查看时写备份表并更新埋点最后访问编号</param>
    /// <returns></returns>
    private static IList<SampleData> Search(String traceId, PageParameter p, Boolean archive)
    {
        // 指定追踪标识后，分页放大（MVC 20→10000）
        if (!traceId.IsNullOrEmpty())
        {
            if (p.PageSize == 20) p.PageSize = 10_000;
        }
        //if (p.Sort.IsNullOrEmpty()) p.OrderBy = SampleData._.Id.Desc();

        var traceIds = traceId.Split(',').Distinct().ToArray();

        var start = DateTime.Today.AddDays(-30);
        var end = DateTime.Today;
        var list = SampleData.Search(-1, traceIds, null, start, end, p);
        if (list.Count == 0)
        {
            // 如果是查看调用链，去备份表查一下
            var list2 = SampleData2.Search(traceIds, null, p);
            if (list2.Count > 0)
            {
                foreach (var item in list2)
                {
                    var entity = new SampleData();
                    entity.CopyFrom(item, false, false);

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
                var list2 = SampleData.Search(-1, traceIds, null, date, date, p);
                if (list2.Count > 0) (list as List<SampleData>).AddRange(list2);
            }

            if (archive && list.Count < p.PageSize)
            {
                var user = ManageProvider.User;
                foreach (var item in traceIds)
                {
                    // 备份调用链，用于将来查询
                    SampleData2.Backup(item, list, user?.ID ?? 0, user + "");
                }
            }
        }

        // 记录最后一次访问，避免TraceId过多导致字段溢出
        if (archive && list.Count > 0)
        {
            var ti = list[0].TraceItem;
            if (ti != null)
            {
                ti.TraceId = traceIds.Take(5).Join(",");
                ti.Update();
            }
        }

        // 如果有traceId，则按照要求排序，深度搜索算法
        if (list.Count > 0)
        {
            var rs = new List<SampleData>();
            var stack = new Stack<SampleData>();

            // 整理排序，为了配合栈计算，采用反向顺序
            var list2 = list.OrderByDescending(e => e.StartTime).ThenByDescending(e => e.SpanId).ToList();

            // 找到所有parentId，包括空字符串，首次出现的parentId优先
            var pids = list2.Select(e => e.ParentId + "").Distinct().ToArray();
            // 找到顶级parentId，它所对应的span不存在，包括空字符串
            foreach (var item in pids.Where(e => !list2.Any(y => y.SpanId == e)))
            {
                // 这些parentId的子级，按照时间降序后入栈，它们作为一级树
                foreach (var elm in list2.Where(e => e.ParentId + "" == item))
                {
                    stack.Push(elm);
                }
            }
            foreach (var item in stack)
            {
                list2.Remove(item);
            }

            // 依次弹出parentId，深度搜索
            var parent = stack.Pop();
            rs.Add(parent);
            var pid = parent.SpanId;
            while (true)
            {
                // 当前span的下级，按时间降序入栈
                var ps = list2.Where(e => e.ParentId + "" == pid).ToList();
                foreach (var item in ps)
                {
                    stack.Push(item);
                    list2.Remove(item);

                    // 深度
                    item.Level = parent.Level + 1;
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
            rs.AddRange(list2);

            list = rs;
        }

        return list;
    }
}
