using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Reflection;
using Stardust.Monitors;
using XCode;
using XCode.Cache;

namespace Stardust.Data;

/// <summary>应用系统。服务提供者和消费者</summary>
[ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
public partial class App : Entity<App>
{
    #region 对象操作
    static App()
    {
        // 累加字段
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.Services);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        // 单对象缓存
        var sc = Meta.SingleCache;
        sc.FindSlaveKeyMethod = k => Find(__.Name, k);
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

        if (isNew && !Dirtys[__.AutoActive]) AutoActive = true;
        if (Period == 0) Period = 60;

        if (!Version.IsNullOrEmpty() && !Dirtys[nameof(Compile)])
        {
            var dt = AssemblyX.GetCompileTime(Version);
            if (dt.Year > 2000) Compile = dt;
        }

        base.Valid(isNew);
    }

    protected override App CreateInstance(Boolean forEdit = false)
    {
        // 默认单例
        var entity = base.CreateInstance(forEdit);
        if (forEdit) entity.Singleton = true;

        return entity;
    }

    /// <summary>
    /// 初始化数据
    /// </summary>
    protected override void InitData()
    {
        if (Meta.Count > 0) return;

        var entity = new App
        {
            Name = "StarServer",
            Enable = true,
        };
        entity.Insert();

        entity = new App
        {
            Name = "StarWeb",
            Enable = true,
            AllowControlNodes = "*",
        };
        entity.Insert();
    }

    /// <summary>
    /// 已重载。显示友好名称
    /// </summary>
    /// <returns></returns>
    public override String ToString() => !DisplayName.IsNullOrEmpty() ? DisplayName : Name;
    #endregion

    #region 扩展属性
    /// <summary>服务提供者</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public IList<AppService> Providers => Extends.Get(nameof(Providers), k => AppService.FindAllByAppId(Id));

    /// <summary>服务消费者</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public IList<AppConsume> Consumers => Extends.Get(nameof(Consumers), k => AppConsume.FindAllByAppId(Id));
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static App FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static App FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        //return Meta.SingleCache.GetItemWithSlaveKey(name) as App;

        return Find(_.Name == name);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<App> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return new List<App>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="category"></param>
    /// <param name="start"></param>
    /// <param name="enable"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<App> Search(Int32 projectId, String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.DisplayName.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
    private static readonly Lazy<FieldCache<App>> CategoryCache = new(() => new FieldCache<App>(__.Category)
    {
        Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty,
        MaxRows = 50
    });

    /// <summary>获取所有类别名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllCategory() => CategoryCache.Value.FindAllName().OrderByDescending(e => e.Key).ToDictionary(e => e.Key, e => e.Value);

    public static IList<App> SearchGroupByProject()
    {
        var selects = _.Id.Count("total") & _.ProjectId;
        var exp = new WhereExpression();

        return FindAll(exp.GroupBy(_.ProjectId), null, selects);
    }
    #endregion

    #region 业务操作
    /// <summary>更新信息</summary>
    /// <param name="model"></param>
    /// <param name="ip"></param>
    public static void WriteMeter(TraceModel model, String ip)
    {
        // 修复数据
        var app = FindByName(model.AppId);
        app ??= new App { Name = model.AppId, DisplayName = model.AppName, Enable = true };
        if (app.DisplayName.IsNullOrEmpty() || app.DisplayName == app.Name) app.DisplayName = model.AppName;
        app.Save();

        // 更新应用信息
        if (app != null && model.Info != null)
        {
            //// 严格来说，应该采用公网IP+内外IP+进程ID才能够保证比较高的唯一性，这里为了简单，直接使用公网IP
            //// 同时，还可能存在多层NAT网络的情况，很难保证绝对唯一
            //var clientId = ip;
            //var ss = model.ClientId.Split('@');
            //if (ss.Length >= 2) clientId = $"{ip}@{ss[1]}";

            //var online = Data.AppOnline.GetOrAddClient(clientId);
            //online.Category = app.Category;
            //online.PingCount++;
            //online.Version = model.Version;
            //if (online.CreateIP.IsNullOrEmpty()) online.CreateIP = ip;
            //online.UpdateInfo(app, model.Info);

            //// 优先使用clientId内部的内网本机IP作为跟踪数据客户端实例
            //var clientId = model.ClientId;
            //if (!clientId.IsNullOrEmpty())
            //{
            //    var p = clientId.IndexOf('@');
            //    if (p > 0) clientId = clientId[..p];
            //}
            // 直接使用ClientId来唯一标识应用实例，用于应用性能数据存储
            var clientId = model.ClientId;
            AppMeter.WriteData(app, model.Info, "Trace", clientId, ip);
        }
    }

    /// <summary>写应用历史</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="version"></param>
    /// <param name="ip"></param>
    /// <param name="clientId"></param>
    public void WriteHistory(String action, Boolean success, String remark, String version = null, String ip = null, String clientId = null)
    {
        var history = AppHistory.Create(this, action, success, remark, version ?? Version, Environment.MachineName, ip);
        history.Client = clientId;
        //history.SaveAsync();
        history.Insert();
    }

    /// <summary>获取 或 创建</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static App GetOrAdd(String name) => GetOrAdd(name, FindByName, k => new App { Name = name });

    /// <summary>检查IP白名单黑名单</summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Boolean MatchIp(String ip)
    {
        if (WhiteIPs.IsNullOrEmpty() && BlackIPs.IsNullOrEmpty()) return true;
        if (ip.IsNullOrEmpty()) return false;

        // 黑名单优先，黑名单里面有的，直接拒绝
        var bs = (BlackIPs + "").Split(",", ";");
        if (bs.Length > 0 && bs.Any(e => e.IsMatch(ip))) return false;

        // 白名单里面有的，直接通过
        var ws = (WhiteIPs + "").Split(",", ";");
        if (ws.Length > 0)
        {
            return ws.Any(e => e.IsMatch(ip));
        }

        // 未设置白名单，黑名单里面没有的，直接通过
        return true;
    }
    #endregion
}