using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data;

/// <summary>应用在线。一个应用有多个部署，每个在线会话对应一个服务地址</summary>
public partial class AppOnline : Entity<AppOnline>
{
    #region 对象操作
    static AppOnline()
    {
        var df = Meta.Factory.AdditionalFields;
        df.Add(__.PingCount);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        // 单对象缓存
        var sc = Meta.SingleCache;
        sc.FindSlaveKeyMethod = k => Find(_.Client == k);
        sc.GetSlaveKeyMethod = e => e.Client;
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        if (!Version.IsNullOrEmpty() && !Dirtys[nameof(Compile)])
        {
            var dt = AssemblyX.GetCompileTime(Version);
            if (dt.Year > 2000) Compile = dt;
        }

        if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;

        var len = _.IP.Length;
        if (len > 0 && !IP.IsNullOrEmpty() && IP.Length > len) IP = IP[..len];

        len = _.ProcessName.Length;
        if (len > 0 && !ProcessName.IsNullOrEmpty() && ProcessName.Length > len) ProcessName = ProcessName[..len];

        len = _.CommandLine.Length;
        if (len > 0 && !CommandLine.IsNullOrEmpty() && CommandLine.Length > len) CommandLine = CommandLine[..len];

        len = _.Listens.Length;
        if (len > 0 && !Listens.IsNullOrEmpty() && Listens.Length > len) Listens = Listens[..len];

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
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppOnline FindById(Int32 id)
    {
        if (id <= 0) return null;

        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    ///// <summary>根据会话查找</summary>
    ///// <param name="client">会话</param>
    ///// <param name="cache">是否走缓存</param>
    ///// <returns></returns>
    //public static AppOnline FindByClient(String client, Boolean cache = true)
    //{
    //    if (client.IsNullOrEmpty()) return null;

    //    if (!cache) return Find(_.Client == client);

    //    return Meta.SingleCache.GetItemWithSlaveKey(client) as AppOnline;
    //}

    /// <summary>根据令牌查找</summary>
    /// <param name="token">令牌</param>
    /// <returns>实体对象</returns>
    public static AppOnline FindByToken(String token)
    {
        if (token.IsNullOrEmpty()) return null;

        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Token == token);

        return Find(_.Token == token);
    }

    /// <summary>根据应用查找所有在线记录</summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static IList<AppOnline> FindAllByApp(Int32 appId)
    {
        if (appId == 0) return new List<AppOnline>();

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据IP查找所有在线记录</summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static IList<AppOnline> FindAllByIP(String ip)
    {
        if (ip.IsNullOrEmpty()) return new List<AppOnline>();

        return FindAll(_.IP == ip);
    }

    /// <summary>根据应用和本地IP查找在线记录</summary>
    /// <param name="appId"></param>
    /// <param name="localIp"></param>
    /// <returns></returns>
    public static IList<AppOnline> FindAllByAppAndIP(Int32 appId, String localIp)
    {
        if (appId == 0) return new List<AppOnline>();
        if (localIp.IsNullOrEmpty()) return new List<AppOnline>();

        return FindAll(_.AppId == appId & _.IP == localIp);
    }

    /// <summary>根据应用、本地IP查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="ip">本地IP</param>
    /// <returns>实体列表</returns>
    public static IList<AppOnline> FindAllByAppIdAndIP(Int32 appId, String ip)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.IP.EqualIgnoreCase(ip));

        return FindAll(_.AppId == appId & _.IP == ip);
    }

    /// <summary>根据令牌查找</summary>
    /// <param name="token">令牌</param>
    /// <returns>实体列表</returns>
    public static IList<AppOnline> FindAllByToken(String token)
    {
        if (token.IsNullOrEmpty()) return new List<AppOnline>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Token.EqualIgnoreCase(token));

        return FindAll(_.Token == token);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<AppOnline> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return new List<AppOnline>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="appId"></param>
    /// <param name="nodeId"></param>
    /// <param name="category"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<AppOnline> Search(Int32 projectId, Int32 appId, Int32 nodeId, String category, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (appId >= 0) exp &= _.AppId == appId;
        if (nodeId >= 0) exp &= _.NodeId == nodeId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Client.Contains(key) | _.Version.Contains(key) | _.ProcessName.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>获取 或 创建 会话</summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public static AppOnline GetOrAddClient(String client)
    {
        if (client.IsNullOrEmpty()) return null;

        return GetOrAdd(client, FindByClient, k => new AppOnline { Client = k, Creator = Environment.MachineName });
    }

    /// <summary>
    /// 获取 或 创建  会话
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static AppOnline GetOrAddClient(String ip, String token)
    {
        var key = token.GetBytes().Crc16().GetBytes().ToHex();
        var client = $"{ip}#{key}";

        return GetOrAddClient(client);
    }

    /// <summary>更新信息</summary>
    /// <param name="app"></param>
    /// <param name="info"></param>
    public void Fill(App app, AppInfo info)
    {
        if (app != null)
        {
            ProjectId = app.ProjectId;
            AppId = app.Id;
            Name = app.ToString();
            Category = app.Category;
            if (info != null && !info.Version.IsNullOrEmpty())
            {
                if (app.Version.IsNullOrEmpty())
                    app.Version = info.Version;
                // 比较版本，只要最新版。要求30天内未登录，此时可能是借助埋点数据上传来更新版本信息，并没有登录动作
                else if (app.Compile.AddDays(30) < DateTime.Now &&
                    new Version(info.Version) > new Version(app.Version))
                    app.Version = info.Version;

                if (Version.IsNullOrEmpty()) Version = info.Version;
            }
        }

        if (info != null)
        {
            //Name = info.MachineName;
            Version = info.Version;
            UserName = info.UserName;
            MachineName = info.MachineName;
            ProcessId = info.Id;
            ProcessName = info.Name;
            CommandLine = info.CommandLine;
            Listens = info.Listens;
            StartTime = info.StartTime;
        }
    }

    /// <summary>删除过期，指定过期时间</summary>
    /// <param name="expire">超时时间</param>
    /// <param name="expire2">大颗粒超时时间，为单例应用准备</param>
    /// <returns></returns>
    public static IList<AppOnline> ClearExpire(TimeSpan expire, TimeSpan expire2)
    {
        if (Meta.Count == 0) return null;

        // 10分钟不活跃将会被删除
        var end = DateTime.Now.Subtract(expire);
        var exp = _.UpdateTime < end;
        var list = FindAll(exp, null, null, 0, 0);

        // 单例应用使用大颗粒超时时间
        var end2 = DateTime.Now.Subtract(expire2);

        var list2 = new List<AppOnline>();
        foreach (var item in list.OrderByDescending(e => e.UpdateTime))
        {
            if (item.App == null || !item.App.Singleton || item.IP.IsNullOrEmpty())
                list2.Add(item);
            else if (item.UpdateTime < end2)
                list2.Add(item);
            //else
            //{
            //    // 单例应用，又没有达到最大时间，如果有活跃，则删除当前
            //    var list3 = FindAllByApp(item.AppId);
            //    if (list3.Any(e => e.IP == item.IP && e.UpdateIP == item.UpdateIP && e.UpdateTime >= end))
            //        list2.Add(item);
            //}
        }
        list2.Delete();

        return list2;
    }
    #endregion
}