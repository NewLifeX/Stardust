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
        _timer = new TimerX(DoNodeStat, null, 5_000, 600 * 1000) { Async = true };

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
                ProductStat(dt, GetSelects(dt));
                VersionStat(dt, GetSelects(dt));
                RuntimeStat(dt, GetSelects(dt));
                FrameworkStat(dt, GetSelects(dt));
                CityStat(dt, GetSelects(dt));
                ArchStat(dt, GetSelects(dt));
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
        selects &= _.LastLogin.SumLarge($"'{t1:yyyy-MM-dd}'", "activeT1");
        selects &= _.LastLogin.SumLarge($"'{t7:yyyy-MM-dd}'", "activeT7");
        selects &= _.LastLogin.SumLarge($"'{t30:yyyy-MM-dd}'", "activeT30");
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
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
    }

    private void ProductStat(DateTime date, ConcatExpression selects)
    {
        var category = "产品";
        var list = SearchGroup(date.AddYears(-1), selects & _.ProductCode, _.ProductCode);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.ProductCode + "";
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
    }

    private void VersionStat(DateTime date, ConcatExpression selects)
    {
        var category = "版本";
        var list = SearchGroup(date.AddYears(-1), selects & _.Version, _.Version);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.Version + "";
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
    }

    private void RuntimeStat(DateTime date, ConcatExpression selects)
    {
        // 运行时的戏份版本比较多，需要取前三个字符
        var category = "运行时";
        var list = SearchGroup(date.AddYears(-1), selects & "Substr(Runtime, 1, 3) Rt", "Rt");
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node["Rt"] + "";
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
    }

    private void FrameworkStat(DateTime date, ConcatExpression selects)
    {
        var category = "最高框架";
        var list = SearchGroup(date.AddYears(-1), selects & _.Framework, _.Framework);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.Framework + "";
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
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
            var key = finder[node.CityID]?.Path;
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
    }

    private void ArchStat(DateTime date, ConcatExpression selects)
    {
        var category = "芯片架构";
        var list = SearchGroup(date.AddYears(-1), selects & _.Architecture, _.Architecture);
        var sts = NodeStat.FindAllByDate(category, date);
        foreach (var node in list)
        {
            var key = node.Architecture + "";
            var st = sts.FirstOrDefault(e => e.Key == key);
            st ??= NodeStat.GetOrAdd(category, date, key);

            st.Total = node.ID;
            st.Actives = node["activeT1"].ToInt();
            st.ActivesT7 = node["activeT7"].ToInt();
            st.ActivesT30 = node["activeT30"].ToInt();
            st.News = node["newT1"].ToInt();
            st.NewsT7 = node["newT7"].ToInt();
            st.NewsT30 = node["newT30"].ToInt();

            st.Update();
        }
    }
}