using System.Globalization;
using NewLife;

namespace Stardust.Web.Models.Dashboard;

/// <summary>看板聚合纯函数。不依赖数据库与 Web，便于单元测试</summary>
public static class DashboardMath
{
    /// <summary>计算百分比。分母为 0 时返回 0</summary>
    /// <param name="part">分子</param>
    /// <param name="total">分母</param>
    /// <returns>百分比。0-100</returns>
    public static Double Percent(Double part, Double total) => total <= 0 ? 0 : part * 100d / total;

    /// <summary>计算错误率百分比</summary>
    /// <param name="errors">错误数</param>
    /// <param name="total">总数</param>
    /// <returns>百分比。0-100</returns>
    public static Double ErrorRate(Int64 errors, Int64 total) => total <= 0 ? 0 : errors * 100d / total;

    /// <summary>把秒数格式化为人类可读时长</summary>
    /// <param name="seconds">秒数</param>
    /// <returns>如「3 小时 12 分」</returns>
    public static String Duration(Int32 seconds)
    {
        if (seconds <= 0) return "—";

        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return $"{(Int32)ts.TotalDays} 天 {ts.Hours} 小时";
        if (ts.TotalHours >= 1) return $"{(Int32)ts.TotalHours} 小时 {ts.Minutes} 分";
        if (ts.TotalMinutes >= 1) return $"{(Int32)ts.TotalMinutes} 分 {ts.Seconds} 秒";
        return $"{ts.Seconds} 秒";
    }

    /// <summary>计算项目健康度。在线率与错误率越低扣分越多，告警额外扣分</summary>
    /// <param name="apps">应用总数</param>
    /// <param name="appsOnline">在线应用数</param>
    /// <param name="nodes">节点总数</param>
    /// <param name="nodesOnline">在线节点数</param>
    /// <param name="errorRate">错误率百分比</param>
    /// <param name="alarms">活跃告警数</param>
    /// <returns>健康度。0-100</returns>
    public static Int32 Health(Int32 apps, Int32 appsOnline, Int32 nodes, Int32 nodesOnline, Double errorRate, Int32 alarms)
    {
        var score = 100d;
        if (apps > 0) score -= (1 - Clamp(appsOnline * 1d / apps, 0, 1)) * 30;
        if (nodes > 0) score -= (1 - Clamp(nodesOnline * 1d / nodes, 0, 1)) * 30;
        score -= Clamp(errorRate, 0, 5) * 4;
        score -= Clamp(alarms, 0, 10) * 2;

        return (Int32)Math.Round(Clamp(score, 0, 100));
    }

    /// <summary>健康度等级文本</summary>
    /// <param name="health">健康度</param>
    /// <returns>优秀/良好/一般/较差</returns>
    public static String HealthLevel(Int32 health) => health >= 90 ? "优秀" : health >= 75 ? "良好" : health >= 60 ? "一般" : "较差";

    /// <summary>健康度配色</summary>
    /// <param name="health">健康度</param>
    /// <returns>ok/warn/bad</returns>
    public static String HealthTone(Int32 health) => health >= 75 ? "ok" : health >= 60 ? "warn" : "bad";

    /// <summary>资源水位配色。≥80 危险，≥65 警戒</summary>
    /// <param name="usage">使用率百分比</param>
    /// <returns>ok/warn/bad</returns>
    public static String Level(Double usage) => usage >= 80 ? "bad" : usage >= 65 ? "warn" : "ok";

    /// <summary>判定节点是否处于资源告警。任一指标超过各自阈值即告警</summary>
    /// <param name="cpu">CPU 使用率</param>
    /// <param name="memory">内存使用率</param>
    /// <param name="disk">磁盘使用率</param>
    /// <param name="cpuLimit">CPU 阈值</param>
    /// <param name="memoryLimit">内存阈值</param>
    /// <param name="diskLimit">磁盘阈值</param>
    /// <returns>是否告警</returns>
    public static Boolean IsResourceAlarm(Double cpu, Double memory, Double disk, Int32 cpuLimit, Int32 memoryLimit, Int32 diskLimit)
    {
        if (cpuLimit > 0 && cpu >= cpuLimit) return true;
        if (memoryLimit > 0 && memory >= memoryLimit) return true;
        if (diskLimit > 0 && disk >= diskLimit) return true;

        return false;
    }

    /// <summary>解析节点分区信息。格式如 <c>/[ext4]=12G/100G,C:[NTFS]=50G/200G</c></summary>
    /// <param name="driveInfo">分区信息</param>
    /// <returns>分区列表</returns>
    public static IList<DiskItem> ParseDrives(String driveInfo)
    {
        var list = new List<DiskItem>();
        if (driveInfo.IsNullOrEmpty()) return list;

        foreach (var item in driveInfo.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var text = item.Trim();
            var idx = text.LastIndexOf('=');
            if (idx <= 0) continue;

            var head = text[..idx];
            var tail = text[(idx + 1)..];

            var name = head;
            var format = "";
            var bracket = head.IndexOf('[');
            if (bracket > 0)
            {
                name = head[..bracket];
                var end = head.IndexOf(']', bracket);
                format = end > bracket ? head[(bracket + 1)..end] : "";
            }

            var parts = tail.Split('/');
            if (parts.Length < 2) continue;

            var free = ParseSize(parts[0]);
            var total = ParseSize(parts[1]);
            if (total <= 0) continue;

            var used = total - free;
            if (used < 0) used = 0;

            list.Add(new DiskItem
            {
                Name = name,
                Format = format,
                Used = Math.Round(used, 1),
                Total = Math.Round(total, 1),
                Usage = Math.Round(Percent(used, total), 1),
            });
        }

        return list;
    }

    /// <summary>解析进程名清单。节点在线上报的是逗号分隔的进程名</summary>
    /// <param name="processes">进程名清单</param>
    /// <returns>去重后的进程名</returns>
    public static IList<String> ParseProcesses(String processes)
    {
        if (processes.IsNullOrEmpty()) return [];

        return processes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>构建进程守护清单。配置的进程名与在线上报的进程名取交集判定运行状态</summary>
    /// <param name="alarmProcesses">守护配置。逗号分隔</param>
    /// <param name="onlineProcesses">在线上报。逗号分隔</param>
    /// <returns>守护项列表</returns>
    public static IList<ProcessItem> BuildGuard(String alarmProcesses, String onlineProcesses)
    {
        var online = ParseProcesses(onlineProcesses);
        var names = ParseProcesses(alarmProcesses);
        if (names.Count == 0) names = online;

        return names.Select(name => new ProcessItem
        {
            Name = name,
            Running = online.Any(e => e.EqualIgnoreCase(name)),
        }).ToList();
    }

    /// <summary>解析带单位的容量，例如 <c>12G</c>、<c>512M</c>、<c>1.5T</c>，统一换算为 GB</summary>
    /// <param name="text">容量文本</param>
    /// <returns>GB 数值</returns>
    public static Double ParseSize(String text)
    {
        if (text.IsNullOrEmpty()) return 0;

        var s = text.Trim();
        var unit = s.Length > 0 ? s[^1] : ' ';
        var number = s;
        var factor = 1024d * 1024 * 1024; // 默认字节

        switch (Char.ToUpperInvariant(unit))
        {
            case 'K':
                factor = 1024d;
                number = s[..^1];
                break;
            case 'M':
                factor = 1024d * 1024;
                number = s[..^1];
                break;
            case 'G':
                factor = 1024d * 1024 * 1024;
                number = s[..^1];
                break;
            case 'T':
                factor = 1024d * 1024 * 1024 * 1024;
                number = s[..^1];
                break;
            case 'B':
                number = s[..^1];
                break;
        }

        if (!Double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return 0;

        return value * factor / (1024d * 1024 * 1024);
    }

    /// <summary>计算健康雷达得分。各维度为 0-100 的优良度</summary>
    /// <param name="cpu">CPU 使用率</param>
    /// <param name="memory">内存使用率</param>
    /// <param name="disk">磁盘使用率</param>
    /// <param name="intranet">内网评分</param>
    /// <param name="temperature">温度。摄氏度</param>
    /// <param name="uptimeDays">在线天数</param>
    /// <returns>六维得分与综合分</returns>
    public static (String[] Indicators, Double[] Values, Int32 Score) Radar(Double cpu, Double memory, Double disk, Double intranet, Double temperature, Double uptimeDays)
    {
        var cpuScore = UsageScore(cpu);
        var memoryScore = UsageScore(memory);
        var diskScore = UsageScore(disk);
        var networkScore = Clamp(intranet, 0, 100);
        // 机房常态 40~65℃，超过 65℃ 才扣分，避免正常温度被判成不健康
        var tempScore = temperature <= 0 ? 100 : 100 - Clamp((temperature - 65) * 4, 0, 100);
        // 在线天数越长越稳定；新上线节点只扣基础分，不因刚部署被重罚
        var stableScore = 60 + Clamp(uptimeDays / 7, 0, 1) * 40;

        var values = new[]
        {
            Math.Round(cpuScore, 0),
            Math.Round(memoryScore, 0),
            Math.Round(diskScore, 0),
            Math.Round(networkScore, 0),
            Math.Round(tempScore, 0),
            Math.Round(stableScore, 0),
        };
        var score = (Int32)Math.Round(values.Average());

        return (["CPU", "内存", "磁盘", "网络", "温度", "稳定"], values, score);
    }

    /// <summary>限制数值范围</summary>
    /// <param name="value">数值</param>
    /// <param name="min">下限</param>
    /// <param name="max">上限</param>
    /// <returns>限制后的值</returns>
    public static Double Clamp(Double value, Double min, Double max) => value < min ? min : value > max ? max : value;

    /// <summary>资源使用率折算健康分。50% 以内视为健康，超出部分每百分点扣 2 分</summary>
    /// <param name="usage">使用率百分比</param>
    /// <returns>健康分。0-100</returns>
    public static Double UsageScore(Double usage) => 100 - Clamp((Clamp(usage, 0, 100) - 50) * 2, 0, 100);

    /// <summary>按数量级缩写数字。用于文案中的大数</summary>
    /// <param name="value">数值</param>
    /// <returns>如 1.28 亿、1286 万</returns>
    public static String Abbreviate(Double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 100000000) return Math.Round(value / 100000000, 2).ToString(CultureInfo.InvariantCulture) + " 亿";
        if (abs >= 10000) return Math.Round(value / 10000, 1).ToString(CultureInfo.InvariantCulture) + " 万";

        return Math.Round(value).ToString("n0", CultureInfo.InvariantCulture);
    }

    /// <summary>把时间序列按目标点数分桶，取每桶平均值</summary>
    /// <param name="values">原始值</param>
    /// <param name="buckets">目标桶数</param>
    /// <returns>分桶后的值</returns>
    public static Double[] BucketAverage(IList<Double> values, Int32 buckets)
    {
        if (values == null || values.Count == 0 || buckets <= 0) return [];

        var size = (Int32)Math.Ceiling(values.Count * 1d / buckets);
        if (size <= 1) return values.ToArray();

        var list = new List<Double>();
        for (var i = 0; i < values.Count; i += size)
        {
            var end = Math.Min(i + size, values.Count);
            var sum = 0d;
            for (var j = i; j < end; j++) sum += values[j];
            list.Add(sum / (end - i));
        }

        return list.ToArray();
    }

    /// <summary>在时间范围内均匀分布坐标。用于拓扑图预设坐标</summary>
    /// <param name="index">序号</param>
    /// <param name="count">总数</param>
    /// <param name="y">纵向位置。0-1</param>
    /// <returns>坐标</returns>
    public static (Double X, Double Y) Spread(Int32 index, Int32 count, Double y)
    {
        if (count <= 0) return (0.5, y);
        if (count == 1) return (0.5, y);

        var step = 0.8 / (count - 1);
        return (0.1 + step * index, y);
    }
}
