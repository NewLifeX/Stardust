using Microsoft.Extensions.Hosting;
using NewLife.Log;

namespace Stardust.Web.Services;

/// <summary>流水线队列后台服务。周期派发排队运行（同一流水线串行）并清理卡死运行。
/// Web 与 WebApi 都可运行；派发使用数据库条件更新抢占，避免多实例重复下发。</summary>
/// <param name="pipelineService">流水线编排服务</param>
/// <param name="tracer">追踪器</param>
public class PipelineRunner(PipelineService pipelineService, ITracer tracer) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 等主机完成启动与数据库初始化，避免与应用启动争抢
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var span = tracer?.NewSpan(nameof(PipelineRunner));
                await pipelineService.ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }
}
