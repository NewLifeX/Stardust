using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data;

/// <summary>应用服务。应用提供的服务</summary>
public partial class AppService : Entity<AppService>
{
    #region 对象操作
    static AppService()
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
        //var len = _.CheckResult.Length;
        //if (len > 0 && !CheckResult.IsNullOrEmpty() && CheckResult.Length > len) CheckResult = CheckResult[..len];

        this.TrimExtraLong(_.Address, _.OriginAddress, _.CheckResult);

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

    /// <summary>服务</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public Service Service => Extends.Get(nameof(Service), k => Service.FindById(ServiceId));

    /// <summary>节点</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeId));

    /// <summary>节点</summary>
    [Map(__.NodeId)]
    public String NodeName => Node?.Name;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppService FindById(Int32 id)
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
    public static IList<AppService> FindAllByAppId(Int32 appId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据服务名查找</summary>
    /// <param name="serviceName">服务</param>
    /// <returns>实体对象</returns>
    public static IList<AppService> FindAllByService(String serviceName)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ServiceName == serviceName);

        return FindAll(_.ServiceName == serviceName);
    }

    /// <summary>根据服务查找</summary>
    /// <param name="serviceId">服务</param>
    /// <returns>实体对象</returns>
    public static IList<AppService> FindAllByService(Int32 serviceId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ServiceId == serviceId);

        return FindAll(_.ServiceId == serviceId);
    }

    /// <summary>根据服务查找</summary>
    /// <param name="serviceId">服务</param>
    /// <returns>实体列表</returns>
    public static IList<AppService> FindAllByServiceId(Int32 serviceId)
    {
        if (serviceId <= 0) return new List<AppService>();

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
    public static IList<AppService> Search(Int32 appId, Int32 serviceId, String client, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (serviceId > 0) exp &= _.ServiceId == serviceId;
        if (!client.IsNullOrEmpty()) exp &= _.Client == client;
        if (enable != null) exp &= _.Enable == enable;
        if (!key.IsNullOrEmpty()) exp &= _.ServiceName.Contains(key) | _.Client.Contains(key) | _.Address.Contains(key) | _.Tag.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>匹配版本和数据标签</summary>
    /// <param name="minVersion"></param>
    /// <param name="scope"></param>
    /// <param name="tags"></param>
    /// <returns></returns>
    public Boolean Match(String minVersion, String scope, String[] tags)
    {
        if (!minVersion.IsNullOrEmpty() && String.Compare(Version, minVersion) < 0) return false;

        // 应用服务没有Scope时，谁都可以消费，否则必须匹配
        if (Service != null && Service.UseScope && !Scope.IsNullOrEmpty() && Scope != scope) return false;

        if (tags != null && tags.Length > 0)
        {
            if (Tag.IsNullOrEmpty()) return false;

            var ts = Tag.Split(",");
            if (tags.Any(e => !ts.Contains(e))) return false;
        }

        return true;
    }

    /// <summary>删除过期，指定过期时间</summary>
    /// <param name="expire">超时时间，秒</param>
    /// <returns></returns>
    public static IList<AppService> ClearExpire(TimeSpan expire)
    {
        if (Meta.Count == 0) return null;

        {
            // 短时间不活跃设置为禁用
            var exp = _.UpdateTime < DateTime.Now.Subtract(TimeSpan.FromMinutes(5));
            var list = FindAll(exp, null, null, 0, 0);
            foreach (var item in list)
            {
                item.Enable = false;
            }
            list.Save();
        }

        {
            // 长时间不活跃将会被删除
            var exp = _.UpdateTime < DateTime.Now.Subtract(expire);
            var list = FindAll(exp, null, null, 0, 0);
            list.Delete();

            return list;
        }
    }

    /// <summary>
    /// 转为服务模型
    /// </summary>
    /// <returns></returns>
    public ServiceModel ToModel()
    {
        return new ServiceModel
        {
            ServiceName = ServiceName,
            DisplayName = Service?.DisplayName,
            Client = Client,
            Version = Version,
            Address = Address,
            //Address2 = Address2,
            Scope = Scope,
            Tag = Tag,
            Weight = Weight,
            CreateTime = CreateTime,
            UpdateTime = UpdateTime,
        };
    }
    #endregion
}