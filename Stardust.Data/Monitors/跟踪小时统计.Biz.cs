using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Caching;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors
{
    /// <summary>跟踪小时统计。每应用每接口每小时统计，用于分析接口健康状况</summary>
    public partial class TraceHourStat : Entity<TraceHourStat>
    {
        #region 对象操作
        static TraceHourStat()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            var df = Meta.Factory.AdditionalFields;
            df.Add(nameof(Total));
            df.Add(nameof(Errors));
            df.Add(nameof(TotalCost));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            Cost = Total == 0 ? 0 : (Int32)(TotalCost / Total);
        }
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
        public static TraceHourStat FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据应用、操作名、编号查找</summary>
        /// <param name="appId">应用</param>
        /// <param name="name">操作名</param>
        /// <param name="id">编号</param>
        /// <returns>实体列表</returns>
        public static IList<TraceHourStat> FindAllByAppIdAndNameAndID(Int32 appId, String name, Int32 id)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Name == name && e.ID == id);

            return FindAll(_.AppId == appId & _.Name == name & _.ID == id);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="name">操作名。接口名或埋点名</param>
        /// <param name="start">统计日期开始</param>
        /// <param name="end">统计日期结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<TraceHourStat> Search(Int32 appId, String name, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Name == name;
            exp &= _.StatTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key);

            return FindAll(exp, page);
        }

        /// <summary>查找一批统计</summary>
        /// <param name="time"></param>
        /// <param name="appIds"></param>
        /// <returns></returns>
        public static IList<TraceHourStat> Search(DateTime time, Int32[] appIds) => FindAll(_.StatTime == time & _.AppId.In(appIds));
        #endregion

        #region 业务操作
        private static ICache _cache = Cache.Default;
        private static TraceHourStat FindByTrace(TraceData td, Boolean cache)
        {
            var key = $"TraceHourStat:{td.StatHour}#{td.AppId}#{td.Name}";
            if (cache && _cache.TryGet<TraceHourStat>(key, out var st)) return st;

            // 查询数据库，即时空值也缓存，避免缓存穿透
            st = Find(_.StatTime == td.StatHour & _.AppId == td.AppId & _.Name == td.Name);
            _cache.Set(key, st, 600);

            return st;
        }

        /// <summary>查找统计行</summary>
        /// <param name="dayStats"></param>
        /// <param name="td"></param>
        /// <returns></returns>
        public static TraceHourStat FindOrAdd(IList<TraceHourStat> dayStats, TraceData td)
        {
            var st = dayStats.FirstOrDefault(e => e.StatTime == td.StatHour && e.AppId == td.AppId && e.Name == td.Name);
            if (st == null)
            {
                // 高并发下获取或新增对象
                st = GetOrAdd(td, FindByTrace, k => new TraceHourStat { StatTime = k.StatHour, AppId = k.AppId, Name = k.Name });

                dayStats.Add(st);
            }

            return st;
        }
        #endregion
    }
}