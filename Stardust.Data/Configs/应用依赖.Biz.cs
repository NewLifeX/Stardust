using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Configs
{
    /// <summary>应用依赖。应用可以引用依赖另一个应用</summary>
    public partial class AppQuote : Entity<AppQuote>
    {
        #region 对象操作
        static AppQuote()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(AppId));

            // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
            Meta.Interceptors.Add<UserInterceptor>();
            Meta.Interceptors.Add<TimeInterceptor>();
            Meta.Interceptors.Add<IPInterceptor>();
        }

        /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override void InitData()
        {
            // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
            //if (Meta.Count > 0) return;

            //if (XTrace.Debug) XTrace.WriteLine("开始初始化AppQuote[应用依赖]数据……");

            //}
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppQuote FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];
        }
        #endregion

        #region 高级查询
        /// <summary>按高级条件搜索（内存过滤，小表）</summary>
        public static IList<AppQuote> Search(Int32 appId, Int32 targetAppId, DateTime start, DateTime end, String key, PageParameter p)
        {
            var list = FindAll();
            if (appId > 0) list = list.Where(e => e.AppId == appId).ToList();
            if (targetAppId > 0) list = list.Where(e => e.TargetAppId == targetAppId).ToList();
            if (start > DateTime.MinValue) list = list.Where(e => e.CreateTime >= start).ToList();
            if (end > DateTime.MinValue) list = list.Where(e => e.CreateTime < end.Date.AddDays(1)).ToList();
            return list;
        }
        #endregion

        #region 业务操作
        #endregion
    }
}
