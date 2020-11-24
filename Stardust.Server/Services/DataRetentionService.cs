using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services
{
    public class DataRetentionService : IHostedService
    {
        private TimerX _timer;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // 每小时执行
            _timer = new TimerX(DoWork, null, DateTime.Today.AddMinutes(Rand.Next(60)), 3600 * 1000) { Async = true };

            // 临时来一次
            TimerX.Delay(DoWork, 10_000);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.TryDispose();

            return Task.CompletedTask;
        }

        void DoWork(Object state)
        {
            var set = Setting.Current;
            if (set.DataRetention <= 0) return;

            // 保留数据的起点
            var time = DateTime.Now.AddDays(-set.DataRetention);

            // 删除节点数据
            var rs = NodeData.DeleteBefore(time);
            XTrace.WriteLine("删除[{0}]之前的NodeData共：{1:n0}", time.ToFullString(), rs);

            // 删除应用性能数据
            rs = AppMeter.DeleteBefore(time);
            XTrace.WriteLine("删除[{0}]之前的AppMeter共：{1:n0}", time.ToFullString(), rs);

            // 删除监控明细数据
            rs = TraceData.DeleteBefore(time);
            XTrace.WriteLine("删除[{0}]之前的TraceData共：{1:n0}", time.ToFullString(), rs);
            rs = SampleData.DeleteBefore(time);
            XTrace.WriteLine("删除[{0}]之前的SampleData共：{1:n0}", time.ToFullString(), rs);
        }
    }
}