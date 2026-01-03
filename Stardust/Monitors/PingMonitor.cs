using System.Net.NetworkInformation;
using NewLife;

namespace Stardust.Monitors;

/// <summary>网络质量结果</summary>
public class PingResult
{
    /// <summary>综合评分。0-1之间</summary>
    public Double Score { get; set; }

    /// <summary>平均延迟。单位ms</summary>
    public Int32 Latency { get; set; }

    /// <summary>丢包率。0-1之间</summary>
    public Double LossRate { get; set; }
}

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
        var result = await GetResultAsync(host).ConfigureAwait(false);
        return result.Score;
    }

    /// <summary>执行多次Ping请求，获取网络质量详细结果</summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public async Task<PingResult> GetResultAsync(String? host)
    {
        var result = new PingResult();
        if (host.IsNullOrEmpty()) return result;

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

        // 计算丢包率
        result.LossRate = Math.Round(1.0 - (Double)rtTimes.Count / Times, 3);

        // 综合评估分数
        if (rtTimes.Count > 0)
        {
            // 计算平均延迟
            var latency = rtTimes.Average();
            result.Latency = (Int32)Math.Round(latency);

            // 延迟阈值1ms
            var threshold = 1f;
            var successRate = (Double)rtTimes.Count / Times;
            // 衰减系数λ=0.001。1ms为100%，10ms为99.1%，100ms为90.57%，500ms为60.71%，1000ms为36.82%
            var latencyScore = Math.Exp(-0.001 * (latency - threshold));

            // 确保得分在0-1之间
            result.Score = Math.Round(successRate * latencyScore, 3);
        }

        return result;
    }
    #endregion
}
