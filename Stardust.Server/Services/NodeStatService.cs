using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Nodes;
using XCode;
using static Stardust.Data.Nodes.Node;

namespace Stardust.Server.Services;

public class NodeStatService : IHostedService
{
    private readonly Setting _setting;
    private readonly ITracer _tracer;
    private TimerX _timer;
    public NodeStatService(Setting setting, ITracer tracer)
    {
        _setting = setting;
        _tracer = tracer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(DoOSKindStat, null, 5_000, 60 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoOSKindStat(Object state)
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

                OSKindStat(dt, selects);
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

    private void OSKindStat(DateTime date, ConcatExpression selects)
    {
        var category = "OSKind";
        var list = SearchGroup(selects & _.OSKind, _.OSKind);
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
}