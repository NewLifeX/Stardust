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

/// <summary>跟踪每日统计。每应用每接口每日统计，用于分析接口健康状况</summary>
public partial class TraceDayStat : Entity<TraceDayStat>
{
    #region 对象操作
    private static readonly ICache _cache = Cache.Default;
    static TraceDayStat()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        var df = Meta.Factory.AdditionalFields;
        df.Add(nameof(Total));
        df.Add(nameof(Errors));
        df.Add(nameof(TotalCost));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
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

        // 识别操作类型
        if (Type.IsNullOrEmpty() && !Name.IsNullOrEmpty())
        {
            if (Name.StartsWithIgnoreCase("/", "rps:"))
                Type = "api";
            else if (Name.StartsWithIgnoreCase("net:"))
                Type = "net";
            else if (Name.StartsWithIgnoreCase("http:", "https:", "rpc:"))
                Type = "http";
            else if (Name.StartsWithIgnoreCase("db:"))
                Type = "db";
            else if (Name.StartsWithIgnoreCase("mq:", "mqtt:", "rmq:", "redismq:", "rocketmq:", "kafka:", "mns:", "emq:"))
                Type = "mq";
            else if (Name.StartsWithIgnoreCase("redis:"))
                Type = "redis";
            else if (Name.Contains(":"))
                Type = Name.Substring(null, ":").ToLower();
            else
                Type = "other";
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
    public static TraceDayStat FindByID(Int32 id)
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
    public static IList<TraceDayStat> FindAllByAppId(Int32 appId) => FindAll(_.AppId == appId);

    /// <summary>
    /// 根据应用和跟踪项查询
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> FindAllByAppAndItem(Int32 appId, Int32 itemId)
    {
        if (appId == 0 && itemId == 0) throw new ArgumentNullException(nameof(appId));

        var where = new WhereExpression();
        if (appId > 0) where &= _.AppId == appId;
        if (itemId > 0) where &= _.ItemId == itemId;

        return FindAll(where);
    }

    /// <summary>查询某应用某天的所有统计，带缓存</summary>
    /// <param name="appId"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> FindAllByAppIdWithCache(Int32 appId, DateTime date)
    {
        var key = $"TraceDayStat:FindAllByAppIdWithCache:{appId}#{date:yyyyMMdd}";
        if (_cache.TryGetValue<IList<TraceDayStat>>(key, out var list) && list != null) return list;

        // 查询数据库，即使空值也缓存，避免缓存穿透
        list = FindAll(_.AppId == appId & _.StatDate == date);

        _cache.Set(key, list, 10);

        return list;
    }

/// <summary>根据统计日期、应用、种类查找</summary>
/// <param name="statDate">统计日期</param>
/// <param name="appId">应用</param>
/// <param name="type">种类</param>
/// <returns>实体列表</returns>
public static IList<TraceDayStat> FindAllByStatDateAndAppIdAndType(DateTime statDate, Int32 appId, String type)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.StatDate == statDate && e.AppId == appId && e.Type.EqualIgnoreCase(type));

    return FindAll(_.StatDate == statDate & _.AppId == appId & _.Type == type);
}

/// <summary>根据统计日期、应用、跟踪项查找</summary>
/// <param name="statDate">统计日期</param>
/// <param name="appId">应用</param>
/// <param name="itemId">跟踪项</param>
/// <returns>实体列表</returns>
public static IList<TraceDayStat> FindAllByStatDateAndAppIdAndItemId(DateTime statDate, Int32 appId, Int32 itemId)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.StatDate == statDate && e.AppId == appId && e.ItemId == itemId);

    return FindAll(_.StatDate == statDate & _.AppId == appId & _.ItemId == itemId);
}

/// <summary>根据应用、跟踪项查找</summary>
/// <param name="appId">应用</param>
/// <param name="itemId">跟踪项</param>
/// <returns>实体列表</returns>
public static IList<TraceDayStat> FindAllByAppIdAndItemId(Int32 appId, Int32 itemId)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.ItemId == itemId);

    return FindAll(_.AppId == appId & _.ItemId == itemId);
}

/// <summary>根据应用、种类、统计日期查找</summary>
/// <param name="appId">应用</param>
/// <param name="type">种类</param>
/// <param name="statDate">统计日期</param>
/// <returns>实体列表</returns>
public static IList<TraceDayStat> FindAllByAppIdAndTypeAndStatDate(Int32 appId, String type, DateTime statDate)
{
    // 实体缓存
    if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Type.EqualIgnoreCase(type) && e.StatDate == statDate);

    return FindAll(_.AppId == appId & _.Type == type & _.StatDate == statDate);
}
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="itemId">跟踪项</param>
    /// <param name="name">操作名。接口名或埋点名</param>
    /// <param name="type">操作类型</param>
    /// <param name="start">统计日期开始</param>
    /// <param name="end">统计日期结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<TraceDayStat> Search(Int32 appId, Int32 itemId, String name, String type, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (itemId > 0) exp &= _.ItemId == itemId;
        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        if (!type.IsNullOrEmpty()) exp &= _.Type == type;

        if (start.Year > 2000 && start == end)
            exp &= _.StatDate == start;
        else
            exp &= _.StatDate.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>查找一批统计</summary>
    /// <param name="date"></param>
    /// <param name="appIds"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> Search(DateTime date, Int32[] appIds) => FindAll(_.StatDate == date & _.AppId.In(appIds));

    /// <summary>根据应用和类型分组统计</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> SearchGroupAppAndType(DateTime date)
    {
        var selects = _.Total.Sum() & _.Errors.Sum() & _.TotalCost.Sum() & _.MaxCost.Max() & _.MinCost.Min() & _.AppId & _.Type;
        var where = new WhereExpression() & _.StatDate == date;

        return FindAll(where.GroupBy(_.AppId, _.Type), null, selects);
    }

    /// <summary>根据应用和名称分组统计</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> SearchGroupAppAndName(DateTime date)
    {
        var selects = _.ID.Count() & _.AppId & _.Name;
        var where = new WhereExpression() & _.StatDate == date;

        return FindAll(where.GroupBy(_.AppId, _.Name), null, selects);
    }

    /// <summary>根据应用和名称分组统计</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> SearchGroupAppAndItem(DateTime date)
    {
        var selects = _.ID.Count() & _.AppId & _.ItemId;
        var where = new WhereExpression() & _.StatDate == date;

        return FindAll(where.GroupBy(_.AppId, _.ItemId), null, selects);
    }

    /// <summary>根据应用分组统计</summary>
    /// <param name="appId"></param>
    /// <param name="startTime"></param>
    /// <returns></returns>
    public static IList<TraceDayStat> SearchGroupItemByApp(Int32 appId, DateTime startTime)
    {
        var selects = _.ID.Count() & _.Total.Sum() & _.Errors.Sum() & _.Cost.Avg() & _.ItemId;
        var where = new WhereExpression();
        where &= _.AppId == appId;
        if (startTime.Year > 2000) where &= _.StatDate >= startTime;

        return FindAll(where.GroupBy(_.ItemId), null, selects);
    }
    #endregion

    #region 业务操作
    private static TraceDayStat FindByTrace(TraceStatModel model, Boolean cache)
    {
        var key = $"TraceDayStat:FindByTrace:{model.Key}";
        if (cache && _cache.TryGetValue<TraceDayStat>(key, out var st)) return st;

        using var span = DefaultTracer.Instance?.NewSpan("TraceDayStat-FindByTrace", model.Key);

        st = FindAllByAppIdWithCache(model.AppId, model.Time.Date)
            .FirstOrDefault(e => e.StatDate == model.Time && e.ItemId == model.ItemId);

        // 查询数据库
        st ??= Find(_.StatDate == model.Time & _.AppId == model.AppId & _.ItemId == model.ItemId);

        if (st != null) _cache.Set(key, st, 300);

        return st;
    }

    /// <summary>查找统计行</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public static TraceDayStat FindOrAdd(TraceStatModel model) =>
        // 高并发下获取或新增对象
        GetOrAdd(model, FindByTrace, m => new TraceDayStat { StatDate = m.Time, AppId = m.AppId, ItemId = m.ItemId });

    /// <summary>删除指定时间之前的数据</summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime time) => Delete(_.StatDate < time);

    /// <summary>
    /// 按照应用和埋点删除
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public static Int32 DeleteByAppAndItem(Int32 appId, Int32 itemId)
    {
        if (appId == 0 && itemId == 0) return 0;

        var where = new WhereExpression();
        if (appId > 0) where &= _.AppId == appId;
        if (itemId > 0) where &= _.ItemId == itemId;

        return Delete(where);
    }
    #endregion
}