using System.Net.NetworkInformation;
using NewLife;

namespace Stardust.Monitors;

/// <summary>心跳监控。通过发往目标IP的Ping命令来计算延迟和丢包率，进一步得到分数</summary>
public class PingMonitor
{
    #region 属性
    /// <summary>心跳次数</summary>
    public Int32 Times { get; set; } = 5;

    /// <summary>多次心跳的间隔</summary>
    public Int32 Interval { get; set; } = 1_000;
    #endregion

    #region 方法
    /// <summary>执行多次Ping请求，获取网络质量评分</summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public async Task<Double> GetScoreAsync(String? host)
    {
        //XTrace.WriteLine("GetScoreAsync：{0}", host);
        if (host.IsNullOrEmpty()) return 0;

        var rtTimes = new List<Double>();

        using var ping = new Ping();
        for (var i = 0; i < Times; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 5_000).ConfigureAwait(false);

                if (reply.Status == IPStatus.Success)
                    rtTimes.Add(reply.RoundtripTime);
            }
            catch { }

            if (i + 1 < Times) await Task.Delay(Interval).ConfigureAwait(false);
        }

        // 综合评估分数
        if (rtTimes.Count > 0)
        {
            // 延迟阈值50ms
            var threshold = 1f;
            var successRate = rtTimes.Count / Times;
            var latency = rtTimes.Average();
            //var latencyScore = 0d;
            //if (latency <= threshold)
            //    latencyScore = 1f;
            //else
            // 衰减系数λ=0.001。1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%
            var latencyScore = Math.Exp(-0.001 * (latency - threshold));

            // 确保得分在0-1之间
            var score = successRate * latencyScore;
            //XTrace.WriteLine($"Score: {score} successRate:{successRate} latency:{latency} latencyScore:{latencyScore}");
            return Math.Round(score, 3);
        }

        return 0;
    }
    #endregion
}
