using System;
using System.Xml.Serialization;
using NewLife;
using NewLife.Reflection;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data
{
    /// <summary>应用在线。一个应用有多个部署，每个在线会话对应一个服务地址</summary>
    public partial class AppOnline : EntityBase<AppOnline>
    {
        #region 对象操作
        static AppOnline()
        {
            var df = Meta.Factory.AdditionalFields;
            df.Add(__.PingCount);

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
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
        [XmlIgnore]
        //[ScriptIgnore]
        public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

        /// <summary>应用</summary>
        [Map(__.AppId, typeof(App), "Id")]
        public String AppName => App?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppOnline FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据会话查找</summary>
        /// <param name="client">会话</param>
        /// <returns>实体对象</returns>
        public static AppOnline FindByClient(String client)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Client == client);

            return Find(_.Client == client);
        }
        #endregion

        #region 高级查询
        #endregion

        #region 业务操作
        /// <summary>获取 或 创建 会话</summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static AppOnline GetOrAddClient(String client)
        {
            return GetOrAdd(client, (k, c) => Find(_.Client == k), k => new AppOnline { Client = k });
        }

        /// <summary>更新信息</summary>
        /// <param name="app"></param>
        /// <param name="info"></param>
        public void UpdateInfo(App app, AppInfo info)
        {
            AppId = app.Id;
            Name = app.Name;
            Version = info.Version;
            UserName = info.UserName;
            ProcessId = info.Id;
            ProcessName = info.Name;
            StartTime = info.StartTime;

            SaveAsync();
        }
        #endregion
    }
}