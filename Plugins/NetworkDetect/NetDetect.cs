using System.ComponentModel;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.Threading;
using Stardust.Plugins;

namespace NetworkDetect;

[DisplayName("网络检测")]
public class NetDetect : AgentPlugin
{
    private TimerX _timer;
    private ITracer _tracer;
    private IList<Worker> _workers;

    /// <summary>开始工作</summary>
    public override void Start()
    {
        _tracer = Provider.GetService<ITracer>();

        if (_timer == null)
        {
            _timer = new TimerX(CheckWorker, null, 1000, 15000);

            NetworkDetectSetting.Provider.Changed += Provider_Changed;
        }
    }

    /// <summary>停止工作</summary>
    /// <param name="reason"></param>
    public override void Stop(String reason)
    {
        _timer.TryDispose();

        NetworkDetectSetting.Provider.Changed -= Provider_Changed;
    }

    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Stop(disposing ? "Dispose" : "GC");
    }

    private void Provider_Changed(Object sender, EventArgs e)
    {
        // 配置改变，重新加载
        var ws = _workers;
        if (ws != null)
        {
            _workers = null;

            foreach (var item in ws)
            {
                item.Stop();
            }
        }

        _timer?.SetNext(-1);
    }

    private void CheckWorker(Object state)
    {
        //var flag = NetworkInterface.GetIsNetworkAvailable();
        //XTrace.WriteLine("网络：{0}", flag ? "可用" : "不可用");

        var ws = _workers;
        if (ws == null || ws.Count == 0)
        {
            var set = NetworkDetectSetting.Current;
            if (set.Services != null && set.Services.Length > 0)
            {
                ws = [];
                foreach (var item in set.Services)
                {
                    if (item.Enable)
                    {
                        var wrk = new Worker
                        {
                            Item = item,
                            Period = set.Period,
                            Tracer = _tracer
                        };

                        wrk.Start();

                        ws.Add(wrk);
                    }
                }

                _workers = ws;
            }
        }
    }
}