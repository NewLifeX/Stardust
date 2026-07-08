using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;

namespace StarGateway
{
    public interface IHostedService
    {
        Task StartAsync(CancellationToken cancellationToken);

        Task StopAsync(CancellationToken cancellationToken);
    }

    public class Host
    {
        #region 服务集合
        public IList<IHostedService> Services { get; } = new List<IHostedService>();

        public void Add<TService>() where TService : IHostedService, new() => Services.Add(new TService());

        public void Add(IHostedService service) => Services.Add(service);
        #endregion

        #region 开始停止
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var item in Services)
            {
                await item.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            XTrace.WriteLine("正在优雅关闭服务……");

            // 反向顺序停止服务（先启动的后停止）
            for (var i = Services.Count - 1; i >= 0; i--)
            {
                var item = Services[i];
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(10_000); // 每个服务最多10秒

                    await item.StopAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    XTrace.WriteLine("服务 [{0}] 停止超时，强制关闭", item.GetType().Name);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }
        }
        #endregion

        #region 运行大循环
        private TaskCompletionSource<Object> _life;
        public void Run() => RunAsync().GetAwaiter().GetResult();

        public async Task RunAsync()
        {
            XTrace.WriteLine("Starting......");

            using var source = new CancellationTokenSource();

            _life = new TaskCompletionSource<Object>();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => _life.TrySetResult(null);
            Console.CancelKeyPress += (s, e) => _life.TrySetResult(null);

            await StartAsync(source.Token);
            XTrace.WriteLine("Application started. Press Ctrl+C to shut down.");

            await _life.Task;

            XTrace.WriteLine("Application is shutting down...");

            await StopAsync(source.Token);

            XTrace.WriteLine("Stopped!");
        }
        #endregion
    }
}