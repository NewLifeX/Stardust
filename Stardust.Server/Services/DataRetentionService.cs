using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services;

/// <summary>数据保留与清理服务。定期清理过期数据，支持多级保留期配置</summary>
public class DataRetentionService(StarServerSetting setting, ITracer tracer) : IHostedService
{
    private TimerX _timer;

    /// <summary>启动服务，初始化定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(DoWork, null, DateTime.Now.AddSeconds(30), 600 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>停止服务，销毁定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    /// <summary>执行数据清理。按多级保留期删除节点数据、Redis数据、统计数据和历史记录</summary>
    /// <param name="state">定时器状态参数</param>
    private void DoWork(Object state)
    {
        if (setting.DataRetention <= 0) return;

        // 保留数据的起点
        var time = DateTime.Now.AddDays(-setting.DataRetention);
        var time2 = DateTime.Now.AddDays(-setting.DataRetention2);
        var time3 = DateTime.Now.AddDays(-setting.DataRetention3);

        using var span = tracer?.NewSpan("DataRetention", new { time, time2, time3 });
        try
        {
            // 逐表清理。单表失败（表缺失/库锁等）只记录异常，不影响后续表的清理
            // 格式串里的表名不能改用占位符：词表按“表名”逐条提供译文，通用模板在词表里没有对应条目，会退回英文
            Clear(NodeData.DeleteBefore, time2, "Deleted {1:n0} NodeData records before [{0}]");
            Clear(RedisData.DeleteBefore, time2, "Deleted {1:n0} RedisData records before [{0}]");
            Clear(AppMeter.DeleteBefore, time2, "Deleted {1:n0} AppMeter records before [{0}]");
            Clear(AppMinuteStat.DeleteBefore, time, "Deleted {1:n0} AppMinuteStat records before [{0}]");
            Clear(TraceMinuteStat.DeleteBefore, time, "Deleted {1:n0} TraceMinuteStat records before [{0}]");
            Clear(TraceHourStat.DeleteBefore, time2, "Deleted {1:n0} TraceHourStat records before [{0}]");
            Clear(TraceDayStat.DeleteBefore, time3, "Deleted {1:n0} TraceDayStat records before [{0}]");
            Clear(NodeHistory.DeleteBefore, time3, "Deleted {1:n0} NodeHistory records before [{0}]");
            Clear(AppHistory.DeleteBefore, time3, "Deleted {1:n0} AppHistory records before [{0}]");
            Clear(AppDeployHistory.DeleteBefore, time3, "Deleted {1:n0} AppDeployHistory records before [{0}]");
            Clear(AlarmHistory.DeleteBefore, time3, "Deleted {1:n0} AlarmHistory records before [{0}]");

            //// 删除监控明细数据
            //rs = TraceData.DeleteBefore(time);
            //XTrace.WriteLine("删除[{0}]之前的TraceData共：{1:n0}", time.ToFullString(), rs);
            //rs = SampleData.DeleteBefore(time);
            //XTrace.WriteLine("删除[{0}]之前的SampleData共：{1:n0}", time.ToFullString(), rs);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
    }

    /// <summary>清理单张表的过期数据。单表异常只记录，不中断整条清理链</summary>
    /// <param name="deleteBefore">删除方法，参数为时间阈值，返回删除行数</param>
    /// <param name="time">时间阈值</param>
    /// <param name="format">日志格式串。必须与词表中的逐表译文完全一致，否则命中不到译文而退回英文</param>
    private void Clear(Func<DateTime, Int32> deleteBefore, DateTime time, String format)
    {
        try
        {
            var rs = deleteBefore(time);

            // 没有删除任何数据时不写日志：本服务每 10 分钟跑一次，全零输出会把真正的变更洗没
            if (rs > 0) XTrace.WriteLine(format, time.ToFullString(), rs);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Failed to clean {0} before [{1}]: {2}", format, time.ToFullString(), ex.Message);
        }
    }
}