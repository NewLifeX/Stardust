using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Monitors;

/// <summary>跟踪项。应用下的多个埋点</summary>
public partial class TraceItem : Entity<TraceItem>
{
    #region 对象操作
    private static ICache _cache = Cache.Default;
    static TraceItem()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(AppId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        var sc = Meta.SingleCache;
        sc.Expire = 60;
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (Kind.IsNullOrEmpty() || Kind == "redismq") Kind = GetKind(Name);

        if (!Rules.IsNullOrEmpty() && !Rules.Contains("=")) Rules = $"name={Rules}";
    }

    /// <summary>
    /// 已重载。
    /// </summary>
    /// <returns></returns>
    public override String ToString() => !DisplayName.IsNullOrEmpty() ? DisplayName : Name;
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
    public static TraceItem FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>
    /// 根据应用查找
    /// </summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static IList<TraceItem> FindAllByApp(Int32 appId)
    {
        if (appId <= 0) return new List<TraceItem>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    public static Int32 FindCountByApp(Int32 appId)
    {
        if (appId <= 0) return 0;

        return (Int32)FindCount(_.AppId == appId);
    }

    /// <summary>根据应用、操作名查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="name">操作名</param>
    /// <returns>实体对象</returns>
    public static TraceItem FindByAppIdAndName(Int32 appId, String name)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.AppId == appId && e.Name.EqualIgnoreCase(name));

        return Find(_.AppId == appId & _.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="name">操作名。接口名或埋点名</param>
    /// <param name="kind">应用</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<TraceItem> Search(Int32 appId, String name, String kind, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (!name.IsNullOrEmpty()) exp &= _.Name == name;
        if (!kind.IsNullOrEmpty()) exp &= _.Kind == kind;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Rules.Contains(key) | _.CreateIP.Contains(key) | _.UpdateUser.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Kind From TraceItem Where CreateTime>'2020-01-24 00:00:00' Group By Kind Order By Id Desc limit 20
    static readonly FieldCache<TraceItem> _KindCache = new(nameof(Kind))
    {
        Where = _.CreateTime > DateTime.Today.AddYears(-1) & Expression.Empty
    };

    /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetKinds() => _KindCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>
    /// 获取应用下可用跟踪项
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="startTime"></param>
    /// <returns></returns>
    public static IList<TraceItem> GetValids(Int32 appId, DateTime startTime)
    {
        if (appId <= 0) return new List<TraceItem>();

        var key = $"TraceItem:GetValids:{appId}:{startTime.ToFullString()}";
        if (_cache.TryGetValue<IList<TraceItem>>(key, out var list) && list != null) return list;

        var exp = new WhereExpression();
        exp &= _.AppId == appId;
        exp &= _.Enable == true;
        if (startTime.Year > 2000) exp &= _.UpdateTime >= startTime;

        list = FindAll(exp);

        // 查询数据库，即使空值也缓存，避免缓存穿透
        _cache.Set(key, list, 60);

        return list;
    }

    /// <summary>
    /// 获取种类
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static String GetKind(String name)
    {
        if (name.IsNullOrEmpty()) return "other";

        if (name.StartsWithIgnoreCase("/")) return "api";
        if (name.StartsWithIgnoreCase("http://", "https://")) return "http";
        if (name.StartsWithIgnoreCase("redismq:")) return "mq";

        var p = name.IndexOf(':');
        if (p > 0) return name[..p];

        return "other";
    }

    /// <summary>
    /// 获取规则字典
    /// </summary>
    /// <returns></returns>
    public IDictionary<String, String[]> GetRules()
    {
        var dic = new Dictionary<String, String[]>(StringComparer.OrdinalIgnoreCase);
        if (Rules.IsNullOrEmpty()) return dic;

        // 简版规则（旧版）没有等于号，默认匹配到name上
        if (!Rules.Contains("="))
        {
            dic["name"] = Rules.Split(",", StringSplitOptions.RemoveEmptyEntries);

            return dic;
        }

        var dic2 = Rules.SplitAsDictionary("=", ";");
        var rules = dic2.ToDictionary(e => e.Key, e => e.Value.Split(",", StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        return rules;
    }

    private IDictionary<String, String[]> _rules;
    /// <summary>
    /// 是否匹配该规则
    /// </summary>
    /// <param name="name"></param>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public Boolean IsMatch(String name, String clientId)
    {
        //if (name.IsNullOrEmpty()) return false;
        //if (clientId.IsNullOrEmpty()) return false;

        _rules ??= GetRules();

        // 没有使用该规则，直接过
        if (_rules.TryGetValue("name", out var vs))
        {
            if (name.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(name, StringComparison.OrdinalIgnoreCase))) return false;
        }
        if (_rules.TryGetValue("clientId", out vs))
        {
            if (clientId.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(clientId, StringComparison.OrdinalIgnoreCase))) return false;
        }

        // 过滤其它项
        if (_rules.Keys.Any(e => !e.EqualIgnoreCase("name", "clientId"))) return false;

        return true;
    }

    /// <summary>
    /// 是否匹配该规则
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public Boolean IsMatch(String name)
    {
        if (name.IsNullOrEmpty()) return false;
        if (Rules.IsNullOrEmpty()) return false;

        _rules ??= GetRules();

        if (_rules.TryGetValue("name", out var vs))
        {
            if (name.IsNullOrEmpty() || !vs.Any(e => e.IsMatch(name, StringComparison.OrdinalIgnoreCase))) return false;
        }

        // 过滤其它项
        if (_rules.Keys.Any(e => !e.EqualIgnoreCase("name"))) return false;

        return true;
    }
    #endregion
}