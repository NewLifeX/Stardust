using System.ComponentModel;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Threading;
using Stardust.Plugins;

namespace MySqlAgent;

[DisplayName("MySql助手")]
public class MySqlPlugin : AgentPlugin
{
    private TimerX _timer;
    private ITracer _tracer;
    private BinlogClear _clear;

    /// <summary>开始工作</summary>
    public override void Start()
    {
        _tracer = Provider.GetService<ITracer>();

        _clear = new BinlogClear
        {
            Event = Provider.GetService<IEventProvider>(),
        };
        _clear.Start();
    }

    /// <summary>停止工作</summary>
    /// <param name="reason"></param>
    public override void Stop(String reason)
    {
        _clear.Stop();
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop(disposing ? "Dispose" : "GC");
    }
}