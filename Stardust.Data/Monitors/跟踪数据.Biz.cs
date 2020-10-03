﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
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

            //StatDate = StartTime.ToDateTime().ToLocalTime().Date;
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
        public static TraceData FindById(Int64 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

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
        /// <param name="kind">时间种类。day/hour/minute</param>
        /// <param name="start">创建时间开始</param>
        /// <param name="end">创建时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<TraceData> Search(Int32 appId, String name, String kind, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Name == name;
            if (!key.IsNullOrEmpty()) exp &= _.ClientId == key | _.Name == key;

            if (appId > 0 && start.Year > 2000)
            {
                var fi = kind switch
                {
                    "day" => _.StatDate,
                    "hour" => _.StatHour,
                    "minute" => _.StatMinute,
                    _ => _.StatDate,
                };

                if (start == end)
                    exp &= fi == start;
                else
                    exp &= fi.Between(start, end);
            }
            else
                exp &= _.Id.Between(start, end, Meta.Factory.Snow);

            return FindAll(exp, page);
        }

        /// <summary>根据时间类型搜索</summary>
        /// <param name="appId"></param>
        /// <param name="name"></param>
        /// <param name="kind"></param>
        /// <param name="time"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static IList<TraceData> Search(Int32 appId, String name, String kind, DateTime time, Int32 count)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Name == name;

            var fi = kind switch
            {
                "day" => _.StatDate,
                "hour" => _.StatHour,
                "minute" => _.StatMinute,
                _ => _.StatDate,
            };
            exp &= fi == time;

            return FindAll(exp, new PageParameter { PageSize = count });
        }

        /// <summary>根据应用接口分组统计</summary>
        /// <param name="kind"></param>
        /// <param name="time"></param>
        /// <param name="appIds"></param>
        /// <returns></returns>
        public static IList<TraceData> SearchGroupAppAndName(String kind, DateTime time, Int32[] appIds)
        {
            var exp = new WhereExpression();
            var selects = _.Total.Sum() & _.Errors.Sum() & _.TotalCost.Sum() & _.MaxCost.Max() & _.MinCost.Min() & _.AppId & _.Name;

            var fi = kind switch
            {
                "day" => _.StatDate,
                "hour" => _.StatHour,
                "minute" => _.StatMinute,
                _ => _.StatDate,
            };
            exp &= fi == time;

            if (appIds != null && appIds.Length > 0) exp &= _.AppId.In(appIds);

            return FindAll(exp.GroupBy(_.AppId, _.Name), null, selects);
        }

        /// <summary>查询指定应用根据操作名分组统计</summary>
        /// <param name="appId"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static IList<TraceData> SearchGroupAppAndName(Int32 appId, DateTime start, DateTime end)
        {
            var selects = _.Total.Sum() & _.Errors.Sum() & _.TotalCost.Sum() & _.MaxCost.Max() & _.MinCost.Min() & _.AppId & _.Name & _.StatMinute;

            var exp = new WhereExpression();
            exp &= _.AppId == appId;
            exp &= _.StatMinute >= start & _.StatMinute <= end;

            return FindAll(exp.GroupBy(_.AppId, _.Name, _.StatMinute), null, selects);
        }

        /// <summary>根据应用分组统计</summary>
        /// <param name="date"></param>
        /// <param name="appIds"></param>
        /// <returns></returns>
        public static IList<TraceData> SearchGroupApp(DateTime date, Int32[] appIds)
        {
            var selects = _.Total.Sum() & _.Errors.Sum() & _.TotalCost.Sum() & _.MaxCost.Max() & _.MinCost.Min() & _.AppId;
            var where = new WhereExpression() & _.StatDate == date;
            if (appIds != null && appIds.Length > 0) where &= _.AppId.In(appIds);

            return FindAll(where.GroupBy(_.AppId), null, selects);
        }

        /// <summary>修正当天数据</summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static Int32 FixStatDate(DateTime date)
        {
            return Update(_.StatDate == date, _.Id.Between(date, date, Meta.Factory.Snow));
        }
        #endregion

        #region 业务操作
        /// <summary>根据跟踪数据创建对象</summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static TraceData Create(ISpanBuilder builder)
        {
            var time = builder.StartTime.ToDateTime().ToLocalTime();

            var td = new TraceData
            {
                Id = Meta.Factory.Snow.NewId(),
                StatDate = time.Date,
                StatHour = time.Date.AddHours(time.Hour),
                StatMinute = time.Date.AddHours(time.Hour).AddMinutes(time.Minute / 5 * 5),
                Name = builder.Name.Cut(_.Name.Length),
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