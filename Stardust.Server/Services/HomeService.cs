using NewLife.Caching;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>首页概览服务。统计节点、应用与调用情况，供服务端首页展示</summary>
/// <remarks>
/// 统计结果缓存 30 秒：首页是公开页面，刷新频率远高于数据变化频率，
/// 缓存既降低数据库压力，也避免访客通过连续刷新反推实时数据。
/// 规模数据是否可见由 <see cref="StarServerSetting.HomeSummaryMode"/> 控制，隐藏模式下不读取统计。
/// </remarks>
public class HomeService
{
    /// <summary>概览缓存时间（秒）</summary>
    private const Int32 CacheExpire = 30;

    /// <summary>趋势天数</summary>
    private const Int32 TrendDays = 7;

    private const String CacheKey = "home:status";

    private readonly StarServerSetting _setting;
    private readonly ICacheProvider _cacheProvider;

    #region 构造
    /// <summary>实例化首页概览服务</summary>
    /// <param name="setting">服务端配置</param>
    /// <param name="cacheProvider">缓存提供者</param>
    public HomeService(StarServerSetting setting, ICacheProvider cacheProvider)
    {
        _setting = setting;
        _cacheProvider = cacheProvider;
    }
    #endregion

    #region 概览
    /// <summary>获取首页概览</summary>
    /// <param name="server">服务端地址</param>
    /// <param name="webUrl">管理控制台地址</param>
    /// <returns>首页概览</returns>
    public HomeSummary GetSummary(String server, String? webUrl)
    {
        var mode = _setting.HomeSummaryMode;
        var model = new HomeSummary
        {
            Server = server,
            WebUrl = webUrl,
            Version = typeof(HomeService).Assembly.GetName().Version + "",
            Mode = mode.ToString().ToLower(),
        };

        // 不展示模式下不读取统计，避免把规模数据发送到浏览器
        if (mode == HomeSummaryModes.Hidden) return model;

        var status = GetStatus();
        model.Uptime = Environment.TickCount64 / 1000;
        model.NodeOnline = status.NodeOnline;
        model.NodeTotal = status.NodeTotal;
        model.AppOnline = status.AppOnline;
        model.AppTotal = status.AppTotal;
        model.TodayCall = status.TodayCall;
        model.YesterdayCall = status.YesterdayCall;
        model.TodayError = status.TodayError;
        model.ErrorRate = status.TodayCall > 0 ? (Double)status.TodayError / status.TodayCall : 0;
        model.Trend = Normalize(status.Trend);

        return model;
    }

    /// <summary>把近7天调用量归一化到 0~100，只保留趋势形状</summary>
    /// <param name="trend">原始调用量</param>
    /// <returns>归一化后的趋势</returns>
    private static Double[] Normalize(Int64[]? trend)
    {
        if (trend == null || trend.Length == 0) return [];

        var max = trend.Max();
        if (max <= 0) return trend.Select(e => 0D).ToArray();

        return trend.Select(e => Math.Round(e * 100D / max, 1)).ToArray();
    }
    #endregion

    #region 统计
    /// <summary>取缓存的原始统计，过期后重新计算</summary>
    /// <returns>原始统计</returns>
    private HomeStatus GetStatus()
    {
        var cache = _cacheProvider.Cache;
        var status = cache.Get<HomeStatus>(CacheKey);
        if (status != null) return status;

        status = BuildStatus();
        cache.Set(CacheKey, status, CacheExpire);

        return status;
    }

    /// <summary>统计节点、应用与近7天调用量</summary>
    /// <returns>原始统计</returns>
    private static HomeStatus BuildStatus()
    {
        var trend = new Int64[TrendDays];
        for (var i = 0; i < TrendDays; i++)
        {
            var day = DateTime.Today.AddDays(i - TrendDays + 1);
            trend[i] = AppDayStat.Search(day, null).Sum(e => (Int64)e.Total);
        }

        var today = AppDayStat.Search(DateTime.Today, null);

        return new HomeStatus
        {
            Time = DateTime.Now,
            NodeOnline = NodeOnline.Meta.Count,
            NodeTotal = Node.Meta.Count,
            AppOnline = AppOnline.Meta.Count,
            AppTotal = App.Meta.Count,
            // 今日调用与趋势最后一天同源，保证卡片与曲线一致
            TodayCall = trend[^1],
            YesterdayCall = TrendDays > 1 ? trend[^2] : 0,
            TodayError = today.Sum(e => (Int64)e.Errors),
            Trend = trend,
        };
    }
    #endregion
}
