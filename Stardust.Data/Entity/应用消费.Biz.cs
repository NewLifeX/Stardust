using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Data;

/// <summary>应用消费。应用消费的服务</summary>
public partial class AppConsume : Entity<AppConsume>
{
    #region 对象操作
    static AppConsume()
    {
        var df = Meta.Factory.AdditionalFields;
        df.Add(__.PingCount);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>验证数据</summary>
    /// <param name="isNew"></param>
    public override void Valid(Boolean isNew)
    {
        this.TrimExtraLong(_.Address);

        base.Valid(isNew);
    }
    #endregion

    #region 扩展属性
    /// <summary>应用</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(__.AppId, typeof(App), "Id")]
    public String AppName => App?.Name;

    /// <summary>节点</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeId));

    /// <summary>节点</summary>
    [Map(__.NodeId)]
    public String NodeName => Node?.Name;

    /// <summary>服务</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Service Service => Extends.Get(nameof(Service), k => Service.FindById(ServiceId));
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppConsume FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="appId">应用</param>
    /// <returns>实体对象</returns>
    public static IList<AppConsume> FindAllByAppId(Int32 appId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据服务名查找</summary>
    /// <param name="serviceName">服务</param>
    /// <returns>实体对象</returns>
    public static IList<AppConsume> FindAllByService(String serviceName)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ServiceName == serviceName);

        return FindAll(_.ServiceName == serviceName);
    }

    /// <summary>根据服务查找</summary>
    /// <param name="serviceId">服务</param>
    /// <returns>实体对象</returns>
    public static IList<AppConsume> FindAllByService(Int32 serviceId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ServiceId == serviceId);

        return FindAll(_.ServiceId == serviceId);
    }

    /// <summary>根据服务查找</summary>
    /// <param name="serviceId">服务</param>
    /// <returns>实体列表</returns>
    public static IList<AppConsume> FindAllByServiceId(Int32 serviceId)
    {
        if (serviceId <= 0) return new List<AppConsume>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ServiceId == serviceId);

        return FindAll(_.ServiceId == serviceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="appId"></param>
    /// <param name="serviceId"></param>
    /// <param name="enable"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<AppConsume> Search(Int32 appId, Int32 serviceId, String client, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (serviceId > 0) exp &= _.ServiceId == serviceId;
        if (!client.IsNullOrEmpty()) exp &= _.Client == client;
        if (enable != null) exp &= _.Enable == enable;
        if (!key.IsNullOrEmpty()) exp &= _.ServiceName.Contains(key) | _.Client.Contains(key) | _.Tag.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作

    /// <summary>删除过期，指定过期时间</summary>
    /// <param name="expire">超时时间，秒</param>
    /// <returns></returns>
    public static IList<AppConsume> ClearExpire(TimeSpan expire)
    {
        if (Meta.Count == 0) return null;

        // 10分钟不活跃将会被删除
        var exp = _.UpdateTime < DateTime.Now.Subtract(expire);
        var list = FindAll(exp, null, null, 0, 0);
        list.Delete();

        return list;
    }
    #endregion
}