using System.ComponentModel;
using NewLife.Log;
using Stardust.Plugins;

namespace AgentExpansion;

[DisplayName("代理扩展")]
public class AgentExpansion : AgentPlugin
{
    private AgentExpansionService? _service;

    /// <summary>开始工作</summary>
    public override void Start()
    {
        if (_service != null) return;

        _service = new AgentExpansionService { Log = XTrace.Log };
        _service.Start();
    }

    /// <summary>停止工作</summary>
    /// <param name="reason"></param>
    public override void Stop(String reason)
    {
        _service?.Stop(reason);
        _service = null;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop(disposing ? "Dispose" : "GC");
    }
}
