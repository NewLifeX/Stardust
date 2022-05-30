using System.Net.NetworkInformation;
using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Plugins;

namespace NetworkDetect;

public class NetDetect : AgentPlugin
{
    private TimerX _timer;

    /// <summary>开始工作</summary>
    public override void Start()
    {
        if (_timer == null) _timer = new TimerX(DoWork, null, 1000, 15_000);
    }

    /// <summary>停止工作</summary>
    /// <param name="reason"></param>
    public override void Stop(String reason) => _timer.TryDispose();

    private void DoWork(Object state)
    {
        var flag = NetworkInterface.GetIsNetworkAvailable();
        XTrace.WriteLine("网络：{0}", flag ? "可用" : "不可用");
    }

    protected override void Dispose(Boolean disposing) { base.Dispose(disposing); Stop(disposing ? "Dispose" : "GC"); }
}