using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using StarGateway.Proxy;

namespace StarGateway;

class MyService : IHostedService
{
    private HttpReverseProxy _proxy;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var set = StarGatewaySetting.Current;

        var server = new HttpReverseProxy
        {
            Port = set.Port > 0 ? set.Port : 8080,
            RemoteServer = "http://star.newlifex.com",

            Tracer = DefaultTracer.Instance,
            Log = XTrace.Log,
        };

        if (set.Debug) server.SessionLog = XTrace.Log;
#if DEBUG
        server.SocketLog = XTrace.Log;
        server.LogSend = true;
        server.LogReceive = true;
#endif

        server.Start();

        _proxy = server;

        XTrace.WriteLine("StarGateway 已启动，监听端口 {0}，远程服务器 {1}", set.Port, server.RemoteServer);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _proxy.TryDispose();

        return Task.CompletedTask;
    }
}
