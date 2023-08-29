using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Reflection;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Configs
{
    /// <summary>配置在线。一个应用有多个部署，每个在线会话对应一个服务地址</summary>
    public partial class ConfigOnline : Entity<ConfigOnline>
    {
        #region 对象操作
        static ConfigOnline()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            var df = Meta.Factory.AdditionalFields;
            df.Add(nameof(PingCount));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();

            // 单对象缓存
            var sc = Meta.SingleCache;
            sc.FindSlaveKeyMethod = k => Find(_.Client == k);
            sc.GetSlaveKeyMethod = e => e.Client;
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            if (!Version.IsNullOrEmpty() && !Dirtys[nameof(Compile)]) Compile = AssemblyX.GetCompileTime(Version);
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public AppConfig App => Extends.Get(nameof(App), k => AppConfig.FindById(AppId));

        /// <summary>应用</summary>
        [Map(__.AppId, typeof(AppConfig), "Id")]
        public String AppName => App + "";
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static ConfigOnline FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据客户端查找</summary>
        /// <param name="client">客户端</param>
        /// <returns>实体对象</returns>
        public static ConfigOnline FindByClient(String client)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Client.EqualIgnoreCase(client));

            //return Find(_.Client == client);

            return Meta.SingleCache.GetItemWithSlaveKey(client) as ConfigOnline;
        }

        /// <summary>根据令牌查找</summary>
        /// <param name="token">令牌</param>
        /// <returns>实体对象</returns>
        public static ConfigOnline FindByToken(String token)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Token == token);

            return Find(_.Token == token);
        }

        /// <summary>根据应用查找</summary>
        /// <param name="appId">应用</param>
        /// <returns>实体列表</returns>
        public static IList<ConfigOnline> FindAllByAppId(Int32 appId)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

            return FindAll(_.AppId == appId);
        }

    /// <summary>根据令牌查找</summary>
    /// <param name="token">令牌</param>
    /// <returns>实体列表</returns>
    public static IList<ConfigOnline> FindAllByToken(String token)
    {
        if (token.IsNullOrEmpty()) return new List<ConfigOnline>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Token.EqualIgnoreCase(token));

        return FindAll(_.Token == token);
    }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="client">客户端。IP加进程</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<ConfigOnline> Search(Int32 appId, String client, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!client.IsNullOrEmpty()) exp &= _.Client == client;
            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Category.Contains(key) | _.Name.Contains(key) | _.ProcessName.Contains(key) | _.UserName.Contains(key) | _.Version.Contains(key) | _.Creator.Contains(key) | _.CreateIP.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,Category From ConfigOnline Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        //static readonly FieldCache<ConfigOnline> _CategoryCache = new FieldCache<ConfigOnline>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>获取 或 创建 会话</summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static ConfigOnline GetOrAddClient(String client)
        {
            if (client.IsNullOrEmpty()) return null;

            return GetOrAdd(client, (k, c) => Find(_.Client == k), k => new ConfigOnline { Client = k });
        }

        /// <summary>
        /// 获取 或 创建  会话
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static ConfigOnline GetOrAddClient(String ip, String token)
        {
            var key = token.GetBytes().Crc16().GetBytes().ToHex();
            var client = $"{ip}#{key}";
            var online = FindByClient(client);
            if (online == null) online = FindByToken(token);
            if (online != null) return online;

            return GetOrAddClient(client);
        }

        /// <summary>
        /// 更新在线状态
        /// </summary>
        /// <param name="app"></param>
        /// <param name="clientId"></param>
        /// <param name="ip"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static ConfigOnline UpdateOnline(AppConfig app, String clientId, String ip, String token)
        {
            var online = GetOrAddClient(clientId) ?? GetOrAddClient(ip, token);
            online.AppId = app.Id;
            //online.Name = app.Name;
            online.Category = app.Category;
            online.Token = token;
            online.PingCount++;
            if (online.CreateIP.IsNullOrEmpty()) online.CreateIP = ip;
            online.Creator = Environment.MachineName;

            var appOnline = AppOnline.FindByToken(token);
            if (appOnline != null) online.UpdateInfo(app, appOnline);

            online.SaveAsync(3_000);

            return online;
        }

        /// <summary>更新信息</summary>
        /// <param name="app"></param>
        /// <param name="online"></param>
        public void UpdateInfo(AppConfig app, AppOnline online)
        {
            //PingCount++;

            //AppId = app.Id;
            //Name = app.Name;

            if (online != null)
            {
                Name = online.Name;
                Version = online.Version;
                UserName = online.UserName;
                ProcessId = online.Id;
                ProcessName = online.ProcessName;
                StartTime = online.StartTime;
            }

            //Creator = Environment.MachineName;

            //SaveAsync();
        }

        /// <summary>删除过期，指定过期时间</summary>
        /// <param name="expire">超时时间，秒</param>
        /// <returns></returns>
        public static IList<ConfigOnline> ClearExpire(TimeSpan expire)
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
}