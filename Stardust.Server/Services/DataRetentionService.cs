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
            Clear("NodeData", NodeData.DeleteBefore, time2);
            Clear("RedisData", RedisData.DeleteBefore, time2);
            Clear("AppMeter", AppMeter.DeleteBefore, time2);
            Clear("AppMinuteStat", AppMinuteStat.DeleteBefore, time);
            Clear("TraceMinuteStat", TraceMinuteStat.DeleteBefore, time);
            Clear("TraceHourStat", TraceHourStat.DeleteBefore, time2);
            Clear("TraceDayStat", TraceDayStat.DeleteBefore, time3);
            Clear("NodeHistory", NodeHistory.DeleteBefore, time3);
            Clear("AppHistory", AppHistory.DeleteBefore, time3);
            Clear("AppDeployHistory", AppDeployHistory.DeleteBefore, time3);
            Clear("AlarmHistory", AlarmHistory.DeleteBefore, time3);

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
    /// <param name="name">表名，仅用于日志</param>
    /// <param name="deleteBefore">删除方法，参数为时间阈值，返回删除行数</param>
    /// <param name="time">时间阈值</param>
    private void Clear(String name, Func<DateTime, Int32> deleteBefore, DateTime time)
    {
        try
        {
            var rs = deleteBefore(time);
            XTrace.WriteLine("Deleted {1:n0} {2} records before [{0}]", time.ToFullString(), rs, name);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("Failed to clean {0} before [{1}]: {2}", name, time.ToFullString(), ex.Message);
        }
    }
}