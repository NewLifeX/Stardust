using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors;

/// <summary>跟踪小时统计。每应用每接口每小时统计，用于分析接口健康状况</summary>
public partial class TraceHourStat : Entity<TraceHourStat>
{
    #region 对象操作
    private static ICache _cache = Cache.Default;
    static TraceHourStat()
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

        if (Name.IsNullOrEmpty()) Name = TraceItem.FindById(ItemId) + "";

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
    public static TraceHourStat FindByID(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>查询某应用的所有统计</summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static IList<TraceHourStat> FindAllByAppId(Int32 appId) => FindAll(_.AppId == appId);

    /// <summary>查询某应用指定时间段的所有统计，带缓存</summary>
    /// <param name="appId"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public static IList<TraceHourStat> FindAllByAppIdWithCache(Int32 appId, DateTime start, DateTime end)
    {
        var key = $"TraceHourStat:FindAllByAppIdWithCache:{appId}#{start:yyyyMMddHHmm}#{end:yyyyMMddHHmm}";
        if (_cache.TryGetValue<IList<TraceHourStat>>(key, out var list) && list != null) return list;

        // 查询数据库，即使空值也缓存，避免缓存穿透
        list = FindAll(_.AppId == appId & _.StatTime >= start & _.StatTime < end);

        _cache.Set(key, list, 10);

        return list;
    }

/// <summary>根据统计小时、应用、跟踪项查找</summary>
/// <param name="statTime">统计小时</param>
/// <param name="appId">应用</param>
/// <param name="itemId">跟踪项</param>
/// <returns>实体列表</returns>
public static IList<TraceHourStat> FindAllByStatTimeAndAppIdAndItemId(DateTime statTime, Int32 appId, Int32 itemId)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.StatTime == statTime && e.AppId == appId && e.ItemId == itemId);

    return FindAll(_.StatTime == statTime & _.AppId == appId & _.ItemId == itemId);
}

/// <summary>根据应用、跟踪项查找</summary>
/// <param name="appId">应用</param>
/// <param name="itemId">跟踪项</param>
/// <returns>实体列表</returns>
public static IList<TraceHourStat> FindAllByAppIdAndItemId(Int32 appId, Int32 itemId)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.ItemId == itemId);

    return FindAll(_.AppId == appId & _.ItemId == itemId);
}
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="itemId">跟踪项</param>
    /// <param name="name">操作名。接口名或埋点名</param>
    /// <param name="start">统计日期开始</param>
    /// <param name="end">统计日期结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<TraceHourStat> Search(Int32 appId, Int32 itemId, String name, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (itemId > 0) exp &= _.ItemId == itemId;
        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        exp &= _.StatTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>查找一批统计</summary>
    /// <param name="time"></param>
    /// <param name="appIds"></param>
    /// <returns></returns>
    public static IList<TraceHourStat> Search(DateTime time, Int32[] appIds) => FindAll(_.StatTime == time & _.AppId.In(appIds));
    #endregion

    #region 业务操作
    private static TraceHourStat FindByTrace(TraceStatModel model, Boolean cache)
    {
        var key = $"TraceHourStat:FindByTrace:{model.Key}";
        if (cache && _cache.TryGetValue<TraceHourStat>(key, out var st)) return st;

        using var span = DefaultTracer.Instance?.NewSpan("TraceHourStat-FindByTrace", model.Key);

        st = FindAllByAppIdWithCache(model.AppId, model.Time, model.Time.AddHours(1))
            .FirstOrDefault(e => e.StatTime == model.Time && e.ItemId == model.ItemId);

        // 查询数据库
        st ??= Find(_.StatTime == model.Time & _.AppId == model.AppId & _.ItemId == model.ItemId);

        if (st != null) _cache.Set(key, st, 300);

        return st;
    }

    /// <summary>查找统计行</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public static TraceHourStat FindOrAdd(TraceStatModel model)
    {
        // 高并发下获取或新增对象
        return GetOrAdd(model, FindByTrace, m => new TraceHourStat { StatTime = m.Time, AppId = m.AppId, ItemId = m.ItemId });
    }

    /// <summary>删除指定时间之前的数据</summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime time) => Delete(_.StatTime < time);
    #endregion
}