using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class SampleDataController : EntityController<SampleData>
    {
        static SampleDataController()
        {
            MenuOrder = 50;

            ListFields.RemoveField("ID");
        }

        protected override IEnumerable<SampleData> Search(Pager p)
        {
            var dataId = p["dataId"].ToLong(-1);
            var appId = p["appId"].ToInt(-1);
            var name = p["name"] + "";
            var traceId = p["traceId"];
            var spanId = p["spanId"];
            var parentId = p["parentId"];
            var success = p["success"]?.ToBoolean();

            //var start = p["dtStart"].ToDateTime();
            //var end = p["dtEnd"].ToDateTime();
            var start = p["start"].ToLong(-1);
            var end = p["end"].ToLong(-1);

            // 指定跟踪标识后，分页100
            if (!traceId.IsNullOrEmpty())
            {
                if (p.PageSize == 20) p.PageSize = 100;
            }
            if (p.Sort.IsNullOrEmpty()) p.OrderBy = SampleData._.Id.Desc();

            var list = SampleData.Search(dataId, appId, name, traceId, spanId, parentId, success, start, end, p["Q"], p);
            if (list.Count == 0) return list;

            // 如果有traceId，则按照要求排序，深度搜索算法
            if (!traceId.IsNullOrEmpty() && list.Count > 0)
            {
                var rs = new List<SampleData>();
                var stack = new Stack<SampleData>();

                // 有些数据有pid，但是pid对应的span实际不存在
                var pids = list.Where(e => !e.ParentId.IsNullOrEmpty()).OrderByDescending(e => e.StartTime).Select(e => e.ParentId).Distinct().ToArray();
                foreach (var item in pids.Where(e => !list.Any(y => y.SpanId == e)))
                {
                    foreach (var elm in list.Where(e => e.ParentId == item))
                    {
                        stack.Push(elm);
                    }
                }
                foreach (var item in stack)
                {
                    list.Remove(item);
                }

                var pid = "";
                while (true)
                {
                    // 降序入栈
                    var ps = list.Where(e => e.ParentId + "" == pid).OrderByDescending(e => e.StartTime).ToList();
                    foreach (var item in ps)
                    {
                        stack.Push(item);
                        list.Remove(item);
                    }

                    // 没有数据，跳出
                    if (stack.Count == 0) break;

                    // 出栈，加入结果，处理它的下级
                    if (stack.TryPop(out var sd))
                    {
                        rs.Add(sd);
                        pid = sd.SpanId;
                    }
                }

                // 残留的异常数据
                rs.AddRange(list);

                return rs;
            }

            return list;
        }
    }
}