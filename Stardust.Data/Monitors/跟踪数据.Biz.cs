using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors
{
    /// <summary>跟踪数据。应用定时上报采样得到的埋点跟踪原始数据</summary>
    public partial class TraceData : Entity<TraceData>
    {
        #region 对象操作
        static TraceData()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(AppId));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

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

        /// <summary>开始时间</summary>
        [Map(nameof(StartTime))]
        public DateTime Start => StartTime.ToDateTime().ToLocalTime();

        /// <summary>结束时间</summary>
        [Map(nameof(EndTime))]
        public DateTime End => EndTime.ToDateTime().ToLocalTime();
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static TraceData FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据应用、操作名查找</summary>
        /// <param name="appId">应用</param>
        /// <param name="name">操作名</param>
        /// <returns>实体列表</returns>
        public static IList<TraceData> FindAllByAppIdAndName(Int32 appId, String name)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Name == name);

            return FindAll(_.AppId == appId & _.Name == name);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="name">操作名。接口名或埋点名</param>
        /// <param name="start">创建时间开始</param>
        /// <param name="end">创建时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<TraceData> Search(Int32 appId, String name, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Name == name;
            exp &= _.CreateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.ClientId.Contains(key) | _.Samples.Contains(key) | _.ErrorSamples.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(ID) as ID,Category From TraceData Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
        //static readonly FieldCache<TraceData> _CategoryCache = new FieldCache<TraceData>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>根据跟踪数据创建对象</summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static TraceData Create(ISpanBuilder builder)
        {
            var td = new TraceData
            {
                Name = builder.Name,
                StartTime = builder.StartTime,
                EndTime = builder.EndTime,

                Total = builder.Total,
                Errors = builder.Errors,
                TotalCost = builder.Cost,
                MaxCost = builder.MaxCost,
                MinCost = builder.MinCost,
            };

            if (builder.Samples != null) td.Samples = builder.Samples.Count;
            if (builder.ErrorSamples != null) td.ErrorSamples = builder.ErrorSamples.Count;

            return td;
        }
        #endregion
    }
}