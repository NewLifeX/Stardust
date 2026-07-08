using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using StarGateway.Proxy;
using Stardust;

namespace StarGateway;

class MyService : IHostedService
{
    private HttpReverseProxy _proxy;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var set = StarGatewaySetting.Current;

        // 使用配置的端口，默认8800（与 Setting.cs 默认值一致）
        var port = set.Port > 0 ? set.Port : 8800;

        // 从 StarFactory 获取 Tracer 和 StarServer 地址
        var star = Program.Star;
        var tracer = star?.Tracer ?? DefaultTracer.Instance;
        var serverUrl = star?.Server ?? set.StarServer ?? "http://star.newlifex.com";

        var proxy = new HttpReverseProxy
        {
            Port = port,
            RemoteServer = serverUrl,

            Tracer = tracer,
            Log = XTrace.Log,
        };

        if (set.Debug) proxy.SessionLog = XTrace.Log;
#if DEBUG
        proxy.SocketLog = XTrace.Log;
        proxy.LogSend = true;
        proxy.LogReceive = true;
#endif

        proxy.AdminLog = XTrace.Log;

        proxy.Start();

        _proxy = proxy;

        XTrace.WriteLine("StarGateway 已启动，监听端口 {0}，远程服务器 {1}", port, serverUrl);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _proxy.TryDispose();

        return Task.CompletedTask;
    }
}
