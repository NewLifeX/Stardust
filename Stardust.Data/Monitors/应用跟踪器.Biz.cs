using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;

namespace Stardust.Data.Monitors;

/// <summary>跟踪模式。跟踪所有项，或者新增项不跟踪</summary>
public enum TraceModes
{
    /// <summary>跟踪所有项</summary>
    All = 0,

    /// <summary>仅新增跟踪项但不跟踪</summary>
    CreateNew = 1,

    /// <summary>仅跟踪已有项</summary>
    Existing = 2
}

/// <summary>应用跟踪器。负责跟踪的应用管理</summary>
public partial class AppTracer : Entity<AppTracer>
{
    #region 对象操作
    static AppTracer()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(Period));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        // 单对象缓存
        var sc = Meta.SingleCache;
        sc.FindSlaveKeyMethod = k => Find(_.Name == k);
        sc.GetSlaveKeyMethod = e => e.Name;
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

        //var len = _.Nodes.Length;
        //if (!Nodes.IsNullOrEmpty() && Nodes.Length > len) Nodes = Nodes.Cut(len);

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (isNew)
        {
            if (!Dirtys[nameof(Period)]) Period = 60;
            if (!Dirtys[nameof(MaxSamples)]) MaxSamples = 1;
            if (!Dirtys[nameof(MaxErrors)]) MaxErrors = 10;
            if (!Dirtys[nameof(Timeout)]) Timeout = 5000;
            if (!Dirtys[nameof(MaxTagLength)]) MaxTagLength = 1024;
            if (!Dirtys[nameof(RequestTagLength)]) RequestTagLength = 1024;
            if (!Dirtys[nameof(EnableMeter)]) EnableMeter = true;
        }
        else
        {
            ItemCount = TraceItem.FindCountByApp(ID);
        }
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => !DisplayName.IsNullOrEmpty() ? DisplayName : Name;
    #endregion

    #region 扩展属性
    /// <summary>应用</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(__.AppId, typeof(App), "Id")]
    public String AppName => App?.Name;

    /// <summary>
    /// 有效跟踪项集合
    /// </summary>
    [XmlIgnore, IgnoreDataMember]
    public IList<TraceItem> TraceItems => Extends.Get(nameof(TraceItems), k => TraceItem.GetValids(ID, DateTime.Today.AddDays(-3)));
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppTracer FindByID(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据编号查找</summary>
    /// <param name="appId">编号</param>
    /// <returns>实体对象</returns>
    public static AppTracer FindByAppId(Int32 appId)
    {
        if (appId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.AppId == appId);

        return Find(_.AppId == appId);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static AppTracer FindByName(String name)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name == name);

        // 单对象缓存
        //return Meta.SingleCache.GetItemWithSlaveKey(name) as AppTracer;

        return Find(_.Name == name);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="appId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<AppTracer> FindAllByAppId(Int32 appId)
    {
        if (appId <= 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<AppTracer> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="category">分类</param>
    /// <param name="enable"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppTracer> Search(Int32 projectId, Int32 appId, String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (appId >= 0) exp &= _.AppId == appId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (enable != null) exp &= _.Enable == enable.Value;

        exp &= _.UpdateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Category.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.UpdateUser.Contains(key) | _.UpdateIP.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(ID) as ID,Category From AppTracer Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
    static readonly FieldCache<AppTracer> _CategoryCache = new(nameof(Category));

    /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>添加排除项</summary>
    /// <param name="value"></param>
    public void AddExclude(String value)
    {
        if (value.IsNullOrEmpty()) return;

        var es = new List<String>();
        var ss = Excludes?.Split(",");
        if (ss != null) es.AddRange(ss);

        if (!es.Contains(value))
        {
            es.Add(value);

            Excludes = es.Distinct().Join();
        }
    }

    /// <summary>
    /// 判断指定客户端是否Vip客户端
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public Boolean IsVip(String clientId)
    {
        if (VipClients.IsNullOrEmpty() || clientId.IsNullOrEmpty()) return false;

        foreach (var item in VipClients.Split(","))
        {
            if (item.IsMatch(item)) return true;
        }

        return false;
    }

    String[] _whites;
    Regex[] _regexes;
    /// <summary>是否匹配白名单</summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public Boolean IsWhite(String input)
    {
        if (input.IsNullOrWhiteSpace()) return false;

        var whites = WhiteList;
        if (whites.IsNullOrEmpty()) return false;

        if (_whites == null)
        {
            var ss = whites.Split(",");
            _whites = ss.Where(e => e[0] != '^').ToArray();
            _regexes = ss.Where(e => e[0] == '^').Select(e => new Regex(e)).ToArray();
        }

        foreach (var item in _whites)
        {
            if (item.EqualIgnoreCase(input)) return true;
            if (item.IsMatch(input)) return true;
        }

        foreach (var reg in _regexes)
        {
            if (reg.IsMatch(input)) return true;
        }

        return false;
    }

    private IList<TraceItem> _full;
    /// <summary>
    /// 获取或添加跟踪项
    /// </summary>
    /// <param name="name"></param>
    /// <param name="whiteOnApi"></param>
    /// <returns></returns>
    public TraceItem GetOrAddItem(String name, Boolean? whiteOnApi = null)
    {
        if (name.IsNullOrEmpty()) return null;

        // 先在有效集合中查找
        var list = TraceItems;
        var ti = list.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (ti != null) return ti;

        // 再查全量
        list = _full ??= TraceItem.FindAllByApp(ID);
        ti = list.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (ti != null)
        {
            // 修正历史数据
            if (!ti.Enable && whiteOnApi != null && whiteOnApi.Value)
            {
                ti.Enable = true;
                ti.Update();
            }

            return ti;
        }

        // 通过规则匹配，支持把多个埋点聚合到一起
        ti = list.FirstOrDefault(e => !e.Cloned && e.IsMatch(name));
        if (ti != null) return ti;

        var isApi = name.StartsWith('/');
        if (isApi)
        {
            // 黑白名单
            if (whiteOnApi != null)
            {
                if (!whiteOnApi.Value) return null;
            }
            // 如果只跟踪已存在埋点，则跳过。仅针对API
            else if (Mode == TraceModes.Existing)
            {
                // 本应用白名单判断
                if (!IsWhite(name)) return null;

                whiteOnApi = true;
            }
            else if (Mode == TraceModes.CreateNew)
            {
                if (IsWhite(name)) whiteOnApi = true;
            }
        }

        ti = new TraceItem
        {
            AppId = ID,
            Name = name,
            Enable = Mode == TraceModes.All || !isApi || whiteOnApi != null && whiteOnApi.Value,
        };
        ti.Insert();

        list.Add(ti);

        return ti;
    }

    /// <summary>根据操作名和客户端标识返回可克隆的跟踪项</summary>
    /// <param name="name"></param>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public IEnumerable<TraceItem> GetClones(String name, String clientId)
    {
        if (name.IsNullOrEmpty() && clientId.IsNullOrEmpty()) yield break;

        // 先在有效集合中查找
        var list = TraceItems.Where(e => e.Cloned).ToList();
        if (list.Count == 0) yield break;

        foreach (var item in list)
        {
            if (item.IsMatch(name, clientId)) yield return item;
        }
    }

    /// <summary>修正数据</summary>
    public void Fix()
    {
        var list = TraceDayStat.FindAllByAppId(ID);
        //Days = list.DistinctBy(e => e.StatDate.Date).Count();
        Days = list.Select(e => e.StatDate.ToFullString()).Distinct().Count();
        Total = list.Sum(e => (Int64)e.Total);

        //ItemCount = TraceItems.Count;
        ItemCount = TraceItem.FindCountByApp(ID);
    }
    #endregion
}