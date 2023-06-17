using System.Net.NetworkInformation;
using NewLife;
using NewLife.Log;
using NewLife.Threading;

namespace NetworkDetect;

internal class Worker
{
    public ServiceItem Item { get; set; }

    public Int32 Period { get; set; }

    public ITracer Tracer { get; set; }

    private TimerX _timer;

    public void Start()
    {
        if (_timer == null)
        {
            XTrace.WriteLine("网络探测[{0}]：{1}", Item.Name, Item.Address);

            var set = NetworkDetectSetting.Current;
            var p = set.Period;
            if (p <= 0) p = 5;
            _timer = new TimerX(DoWork, null, 1000, p * 1000) { Async = true };
        }
    }

    public void Stop() => _timer.TryDispose();

    private void DoWork(Object state)
    {
        var ip = Item.Address;
        if (ip.IsNullOrEmpty()) return;

        // 埋点记录
        using var span = Tracer?.NewSpan($"ping:{Item.Name}", ip);
        try
        {
            var reply = new Ping().Send(ip, Item.Timeout);

            if (reply != null) span?.AppendTag($"{reply.Address} {reply.RoundtripTime}ms");

            if (reply.Status != IPStatus.Success)
                throw new Exception(reply.Status + "");
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
        }
    }
}