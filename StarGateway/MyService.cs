using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using StarGateway.Proxy;

namespace StarGateway
{
    class MyService : IHostedService
    {
        private HttpReverseProxy _proxy;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var set = Setting.Current;

            var server = new HttpReverseProxy
            {
                Port = 8080,
                RemoteServer = "http://star.newlifex.com",

                Log = XTrace.Log,
            };

            if (set.Debug) server.SessionLog = XTrace.Log;

            server.Start();

            _proxy = server;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _proxy.TryDispose();

            return Task.CompletedTask;
        }
    }
}
