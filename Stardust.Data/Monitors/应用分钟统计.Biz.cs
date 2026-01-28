using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors;

/// <summary>应用分钟统计。每应用每5分钟统计，用于分析应用健康状况</summary>
public partial class AppMinuteStat : Entity<AppMinuteStat>
{
    #region 对象操作
    private static ICache _cache = Cache.Default;
    static AppMinuteStat()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        var df = Meta.Factory.AdditionalFields;
        df.Add(nameof(Total));
        df.Add(nameof(Errors));
        df.Add(nameof(TotalCost));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        //Cost = Total == 0 ? 0 : (Int32)(TotalCost / Total);
        ErrorRate = Total == 0 ? 0 : Math.Round((Double)Errors / Total, 4);

        if (!Dirtys[nameof(Cost)])
        {
            // 为了让平均值逼近TP99，避免毛刺干扰，减去最大值再取平均
            if (Total >= 50)
                Cost = (Int32)Math.Round((Double)(TotalCost - MaxCost) / (Total - 1));
            else
                Cost = Total == 0 ? 0 : (Int32)Math.Round((Double)TotalCost / Total);
        }
    }
    #endregion

    #region 扩展属性
    /// <summary>应用</summary>
    [XmlIgnore, IgnoreDataMember]
    public AppTracer App => Extends.Get(nameof(App), k => AppTracer.FindByID(AppId));

    /// <summary>应用</summary>
    [Map(nameof(AppId))]
    public String AppName => App + "";
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppMinuteStat FindByID(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据应用、编号查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<AppMinuteStat> FindAllByAppIdAndID(Int32 appId, Int32 id)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.ID == id);

        return FindAll(_.AppId == appId & _.ID == id);
    }

    /// <summary>根据应用和时间查找</summary>
    /// <param name="appId"></param>
    /// <param name="time"></param>
    /// <returns></returns>
    public static AppMinuteStat FindByAppIdAndTime(Int32 appId, DateTime time) => Find(_.AppId == appId & _.StatTime == time);

    /// <summary>查询某应用某天的所有统计，带缓存</summary>
    /// <param name="appId"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public static IList<AppMinuteStat> FindAllByAppIdWithCache(Int32 appId, DateTime date)
    {
        var key = $"AppMinuteStat:FindAllByAppIdWithCache:{appId}#{date:yyyyMMdd}";
        if (_cache.TryGetValue<IList<AppMinuteStat>>(key, out var list) && list != null) return list;

        // 查询数据库，即使空值也缓存，避免缓存穿透
        list = FindAll(_.AppId == appId & _.StatTime >= date & _.StatTime < date.AddDays(1));

        _cache.Set(key, list, 10);

        return list;
    }

/// <summary>根据统计分钟、应用查找</summary>
/// <param name="statTime">统计分钟</param>
/// <param name="appId">应用</param>
/// <returns>实体对象</returns>
public static AppMinuteStat FindByStatTimeAndAppId(DateTime statTime, Int32 appId)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.StatTime == statTime && e.AppId == appId);

    return Find(_.StatTime == statTime & _.AppId == appId);
}

/// <summary>根据应用查找</summary>
/// <param name="appId">应用</param>
/// <returns>实体列表</returns>
public static IList<AppMinuteStat> FindAllByAppId(Int32 appId)
{
    if (appId <= 0) return new List<AppMinuteStat>();

    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

    return FindAll(_.AppId == appId);
}
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="minError">最小错误数。指定后，只返回错误数大于等于该值的数据</param>
    /// <param name="start">统计分钟开始</param>
    /// <param name="end">统计分钟结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppMinuteStat> Search(Int32 appId, Int32 minError, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (minError > 0) exp &= _.Errors >= minError;
        exp &= _.StatTime.Between(start, end);

        return FindAll(exp, page);
    }

    // Select Count(ID) as ID,Category From AppMinuteStat Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
    //static readonly FieldCache<AppMinuteStat> _CategoryCache = new FieldCache<AppMinuteStat>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    private static AppMinuteStat FindByTrace(TraceStatModel model, Boolean cache)
    {
        var key = $"AppMinuteStat:FindByTrace:{model.Key}";
        if (cache && _cache.TryGetValue<AppMinuteStat>(key, out var st)) return st;

        using var span = DefaultTracer.Instance?.NewSpan("AppMinuteStat-FindByTrace", model.Key);

        st = FindAllByAppIdWithCache(model.AppId, model.Time.Date)
            .FirstOrDefault(e => e.StatTime == model.Time);

        // 查询数据库
        st ??= Find(_.StatTime == model.Time & _.AppId == model.AppId);

        if (st != null) _cache.Set(key, st, 60);

        return st;
    }

    /// <summary>查找统计行</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public static AppMinuteStat FindOrAdd(TraceStatModel model)
    {
        // 高并发下获取或新增对象
        return GetOrAdd(model, FindByTrace, m => new AppMinuteStat { StatTime = m.Time, AppId = m.AppId });
    }

    /// <summary>删除指定时间之前的数据</summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime time) => Delete(_.StatTime < time);
    #endregion
}