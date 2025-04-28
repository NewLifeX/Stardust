using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;
using XCode.Model;
using static Stardust.Data.Nodes.Node;

namespace Stardust.Server.Services;

public class NodeStatService : IHostedService
{
    private readonly ITracer _tracer;
    private TimerX _timer;
    public NodeStatService(ITracer tracer) => _tracer = tracer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(DoNodeStat, null, 15_000, 600 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoNodeStat(Object state)
    {
        // 无在线则不执行
        //if (_onlines == 0) return;

        using var span = _tracer?.NewSpan("NodeStat");

        // 减少Sql日志
        var dal = NodeStat.Meta.Session.Dal;
        var oldSql = dal.Session.ShowSQL;
#if !DEBUG
        dal.Session.ShowSQL = false;
#endif
        try
        {
            // 每天0点，补偿跑T-1
            var now = DateTime.Now;
            var start = now.Hour == 0 && now.Minute <= 10 ? now.Date.AddDays(-1) : now.Date;
            for (var dt = start; dt <= DateTime.Today; dt = dt.AddDays(1))
            {
                OSKindStat(dt, GetSelects(dt));
                ProductCodeStat(dt, GetSelects(dt));
                VersionStat(dt, GetSelects(dt));
                RuntimeStat(dt, GetSelects(dt));
                FrameworkStat(dt, GetSelects(dt));
                CityStat(dt, GetSelects(dt));
                ArchStat(dt, GetSelects(dt));
                VendorStat(dt, GetSelects(dt));
                ProductStat(dt, GetSelects(dt));
                BoardStat(dt, GetSelects(dt));
                ProcessorStat(dt, GetSelects(dt));
                ProjectStat(dt, GetSelects(dt));
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
        finally
        {
            dal.Session.ShowSQL = oldSql;
        }
    }

    ConcatExpression GetSelects(DateTime dt)
    {
        var t1 = dt.AddDays(-0);
        var t7 = dt.AddDays(-7);
        var t30 = dt.AddDays(-30);

        var selects = _.ID.Count();
        selects &= _.LastActive.SumLarge($"'{t1:yyyy-MM-dd}'", "activeT1");
        selects &= _.LastActive.SumLarge($"'{t7:yyyy-MM-dd}'", "activeT7");
        selects &= _.LastActive.SumLarge($"'{t30:yyyy-MM-dd}'", "activeT30");
        selects &= _.CreateTime.SumLarge($"'{t1:yyyy-MM-dd}'", "newT1");
        selects &= _.CreateTime.SumLarge($"'{t7:yyyy-MM-dd}'", "newT7");
        selects &= _.CreateTime.SumLarge($"'{t30:yyyy-MM-dd}'", "newT30");

        return selects;
    }

    private void OSKindStat(DateTime date, ConcatExpression selects)
    {
        var category = "操作系统";
        var list = SearchGroup(date.AddYears(-1), selects & _.OSKind, _.OSKind);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.OSKind + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = (Int32)node.OSKind + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void ProductCodeStat(DateTime date, ConcatExpression selects)
    {
        var category = "产品种类";
        var list = SearchGroup(date.AddYears(-1), selects & _.ProductCode, _.ProductCode);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.ProductCode + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = node.ProductCode + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void VersionStat(DateTime date, ConcatExpression selects)
    {
        var category = "版本";
        var list = SearchGroup(date.AddYears(-1), selects & _.Version, _.Version);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.Version + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = node.Version + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void RuntimeStat(DateTime date, ConcatExpression selects)
    {
        // 运行时的戏份版本比较多，需要取前三个字符
        var category = "运行时";
        //var func = NodeStat.Meta.Session.Dal.DbType switch
        //{
        //    DatabaseType.SqlServer => "substring",
        //    DatabaseType.Oracle => "substr",
        //    DatabaseType.MySql => "substr",
        //    DatabaseType.SQLite => "substr",
        //    _ => "substr",
        //};
        //var group = NodeStat.Meta.Session.Dal.DbType switch
        //{
        //    DatabaseType.SqlServer => "(Runtime, 1, 3)",
        //    _ => "Rt",
        //};
        //var list = SearchGroup(date.AddYears(-1), selects & $"{func}(Runtime, 1, 3) Rt", group);
        var list = SearchGroup(date.AddYears(-1), selects & _.Runtime, _.Runtime);
        var sts = NodeStat.FindAllByDate(category, date);
        // 先安装运行时版本全部取出来，再把截取版本然后二次聚合。避免了复杂的SQL兼容问题
        foreach (var item in list.GroupBy(e => (e.Runtime.IsNullOrEmpty() || e.Runtime.Length < 3) ? e.Runtime + "" : e.Runtime[..3]))
        {
            var key = item.Key;
            if (key.Length > 50) key = key[..50];
            var datas = item.ToList();
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = key + "";
            st.Total = datas.Sum(e => e.ID);
            st.Actives = datas.Sum(e => e["activeT1"].ToInt());
            st.ActivesT7 = datas.Sum(e => e["activeT7"].ToInt());
            st.ActivesT30 = datas.Sum(e => e["activeT30"].ToInt());
            st.News = datas.Sum(e => e["newT1"].ToInt());
            st.NewsT7 = datas.Sum(e => e["newT7"].ToInt());
            st.NewsT30 = datas.Sum(e => e["newT30"].ToInt());

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void FrameworkStat(DateTime date, ConcatExpression selects)
    {
        var category = "最高框架";
        var list = SearchGroup(date.AddYears(-1), selects & _.Framework, _.Framework);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.Framework + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = node.Framework + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void CityStat(DateTime date, ConcatExpression selects)
    {
        var category = "城市";
        var list = SearchGroup(date.AddYears(-1), selects & _.CityID, _.CityID);
        var finder = new BatchFinder<Int32, Area>(list.Select(e => e.CityID));
        //finder.Add(list.Select(e => e.CityID));
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            //if (node.CityID == 0) continue;

            //var key = Area.FindByID(node.CityID)?.Path;
            var key = finder[node.CityID]?.Path + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = node.CityID + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void ArchStat(DateTime date, ConcatExpression selects)
    {
        var category = "芯片架构";
        var list = SearchGroup(date.AddYears(-1), selects & _.Architecture, _.Architecture);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.Architecture + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = node.Architecture + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void VendorStat(DateTime date, ConcatExpression selects)
    {
        var category = "制造商";
        var list = SearchGroup(date.AddYears(-1), selects & _.Vendor, _.Vendor);
        var sts = NodeStat.FindAllByDate(category, date);

        // 太少数据的分类合并
        var count = 0;
        foreach (var node in list.OrderByDescending(e => e.ID))
        {
            if (node.Vendor.IsNullOrEmpty() || node.ID < 3 || count++ >= 200)
                node.Vendor = "Other";
        }

        foreach (var item in list.GroupBy(e => e.Vendor))
        {
            var key = item.Key + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = key;

            var nodes = item.ToList();
            st.Total = nodes.Sum(e => e.ID);
            st.Actives = nodes.Sum(e => e["activeT1"].ToInt());
            st.ActivesT7 = nodes.Sum(e => e["activeT7"].ToInt());
            st.ActivesT30 = nodes.Sum(e => e["activeT30"].ToInt());
            st.News = nodes.Sum(e => e["newT1"].ToInt());
            st.NewsT7 = nodes.Sum(e => e["newT7"].ToInt());
            st.NewsT30 = nodes.Sum(e => e["newT30"].ToInt());

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void ProductStat(DateTime date, ConcatExpression selects)
    {
        var category = "产品";
        var list = SearchGroup(date.AddYears(-1), selects & _.Product, _.Product);
        var sts = NodeStat.FindAllByDate(category, date);

        // 太少数据的分类合并
        var count = 0;
        foreach (var node in list.OrderByDescending(e => e.ID))
        {
            if (node.Product.IsNullOrEmpty() || node.ID < 3 || count++ >= 200)
                node.Product = "Other";
        }

        foreach (var item in list.GroupBy(e => e.Product))
        {
            var key = item.Key + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = key;

            var nodes = item.ToList();
            st.Total = nodes.Sum(e => e.ID);
            st.Actives = nodes.Sum(e => e["activeT1"].ToInt());
            st.ActivesT7 = nodes.Sum(e => e["activeT7"].ToInt());
            st.ActivesT30 = nodes.Sum(e => e["activeT30"].ToInt());
            st.News = nodes.Sum(e => e["newT1"].ToInt());
            st.NewsT7 = nodes.Sum(e => e["newT7"].ToInt());
            st.NewsT30 = nodes.Sum(e => e["newT30"].ToInt());

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void BoardStat(DateTime date, ConcatExpression selects)
    {
        var category = "型号";
        var list = SearchGroup(date.AddYears(-1), selects & _.Board, _.Board);
        var sts = NodeStat.FindAllByDate(category, date);

        // 太少数据的分类合并
        var count = 0;
        foreach (var node in list.OrderByDescending(e => e.ID))
        {
            if (node.Board.IsNullOrEmpty() || node.ID < 3 || count++ >= 200)
                node.Board = "Other";
        }

        foreach (var item in list.GroupBy(e => e.Board))
        {
            var key = item.Key + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = key;

            var nodes = item.ToList();
            st.Total = nodes.Sum(e => e.ID);
            st.Actives = nodes.Sum(e => e["activeT1"].ToInt());
            st.ActivesT7 = nodes.Sum(e => e["activeT7"].ToInt());
            st.ActivesT30 = nodes.Sum(e => e["activeT30"].ToInt());
            st.News = nodes.Sum(e => e["newT1"].ToInt());
            st.NewsT7 = nodes.Sum(e => e["newT7"].ToInt());
            st.NewsT30 = nodes.Sum(e => e["newT30"].ToInt());

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void ProcessorStat(DateTime date, ConcatExpression selects)
    {
        var category = "处理器";
        var list = SearchGroup(date.AddYears(-1), selects & _.Processor, _.Processor);
        var sts = NodeStat.FindAllByDate(category, date);

        // 处理器做二次合并，因为处理器太多，需要截取中间一部分
        var count = 0;
        foreach (var node in list.OrderByDescending(e => e.ID))
        {
#if !DEBUG
            if (node.Processor.IsNullOrEmpty() || node.ID < 3 || count++ >= 200)
            {
                node.Processor = "Other";
                continue;
            }
#endif

            var name = (node.Processor + "").Trim();
            var p = name.IndexOf('@');
            if (p > 0) name = name[..p].Trim().TrimEnd("CPU").Trim();

            // 双处理器
            p = name.IndexOf(',');
            if (p > 0) name = name[..p].Trim().Trim();

            p = name.IndexOf("with");
            if (p > 0) name = name[..p].Trim();

            p = name.IndexOf("w/");
            if (p > 0) name = name[..p].Trim();

            p = name.IndexOf("Core(TM)");
            if (p > 0) name = name.Substring(p + "Core(TM)".Length).Trim();

            p = name.IndexOf("CoreT");
            if (p > 0) name = name.Substring(p + "CoreT".Length).Trim();

            p = name.IndexOf("Platinum");
            if (p > 0) name = name[p..].Trim();

            // AMD Ryzen 7 2700 Eight-Core Processor
            p = name.IndexOf("-Core");
            if (p > 0) p = name.LastIndexOf(' ', p);
            if (p > 0) name = name[..p].Trim();

            var name2 = name
                  .TrimStart("AMD ", /*"Ryzen ",*/ /*"EPYC",*/ "Genuine ", "Intel(R) ", "Xeon(R) ", "Pentium(R) ", "Celeron(R) ", "CPU")
                  .TrimEnd(" Processor", /*"-Core",*/ " v2", " v3", " v4", " 0", " (Device Tree)")
                  .Trim();
            //if (name2.Contains("Ryzen"))
            //    XTrace.WriteLine("{0} -> {1}", name, name2);

            node.Processor = name2;
        }

        foreach (var item in list.GroupBy(e => e.Processor))
        {
            var key = item.Key + "";
            if (key.Length > 50) key = key[..50];

            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = key;

            var nodes = item.ToList();
            st.Total = nodes.Sum(e => e.ID);
            st.Actives = nodes.Sum(e => e["activeT1"].ToInt());
            st.ActivesT7 = nodes.Sum(e => e["activeT7"].ToInt());
            st.ActivesT30 = nodes.Sum(e => e["activeT30"].ToInt());
            st.News = nodes.Sum(e => e["newT1"].ToInt());
            st.NewsT7 = nodes.Sum(e => e["newT7"].ToInt());
            st.NewsT30 = nodes.Sum(e => e["newT30"].ToInt());

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }

    private void ProjectStat(DateTime date, ConcatExpression selects)
    {
        var category = "项目";
        var list = SearchGroup(date.AddYears(-1), selects & _.ProjectId, _.ProjectId);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.ProjectName + "";
            if (key.Length > 50) key = key[..50];
            var st = sts.FirstOrDefault(e => e.Key == key);
            if (st == null)
                st = NodeStat.GetOrAdd(category, date, key);
            else
                sts.Remove(st);

            st.LinkItem = node.ProjectId + "";
            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }

        // 删除多余统计项
        sts.Delete();
    }
}