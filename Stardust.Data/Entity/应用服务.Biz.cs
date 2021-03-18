using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using XCode;
using XCode.Membership;

namespace Stardust.Data
{
    /// <summary>应用服务。应用提供的服务</summary>
    public partial class AppService : EntityBase<AppService>
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
        /// <param name="serviceName">应用</param>
        /// <returns>实体对象</returns>
        public static IList<AppService> FindAllByService(String serviceName)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ServiceName == serviceName);

            return FindAll(_.ServiceName == serviceName);
        }
        #endregion

        #region 高级查询
        #endregion

        #region 业务操作

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
        #endregion
    }
}