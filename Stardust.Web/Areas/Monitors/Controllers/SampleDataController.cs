using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers
{
    [MonitorsArea]
    public class SampleDataController : ReadOnlyEntityController<SampleData>
    {
        static SampleDataController()
        {
            MenuOrder = 50;

            ListFields.RemoveField("ID");
        }

        protected override IEnumerable<SampleData> Search(Pager p)
        {
            var dataId = p["dataId"].ToLong(-1);
            var traceId = p["traceId"];

            // 指定追踪标识后，分页500
            if (!traceId.IsNullOrEmpty())
            {
                if (p.PageSize == 20) p.PageSize = 500;
            }
            if (p.Sort.IsNullOrEmpty()) p.OrderBy = SampleData._.Id.Desc();

            var start = DateTime.Today.AddDays(-30);
            var end = DateTime.Today;
            var list = SampleData.Search(dataId, traceId, start, end, p);
            if (list.Count == 0)
            {
                // 如果是查看调用链，去备份表查一下
                if (!traceId.IsNullOrEmpty())
                {
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
                }

                if (list.Count == 0) return list;
            }
            else
            {
                if (!traceId.IsNullOrEmpty() && list.Count < p.PageSize)
                {
                    var user = ManageProvider.User;

                    // 备份调用链，用于将来查询
                    SampleData2.Backup(traceId, list, user?.ID ?? 0, user + "");
                }
            }

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

                //return rs;
                list = rs;
            }

            if (list.Count > 0)
            {
                var appId = list[0].AppId;
                var ar = AppTracer.FindByID(appId);
                if (ar != null) ViewBag.Title = $"{ar}采样";
            }

            return list;
        }

        protected override SampleData Find(Object key)
        {
            //var entity = base.Find(key);
            //var entity = SampleData.FindById(key.ToLong());
            var entity = SampleData.FindByKeyForEdit(key);
            if (entity != null) return entity;

            var entity2 = SampleData2.FindById(key.ToLong());
            if (entity2 != null)
            {
                entity = new SampleData();
                entity.CopyFrom(entity2, false);
                return entity;
            }

            throw new Exception($"无法找到数据[{key}]！");
        }
    }
}