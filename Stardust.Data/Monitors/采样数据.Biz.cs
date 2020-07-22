using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Monitors
{
    /// <summary>采样数据。具体调用或异常详情</summary>
    public partial class SampleData : Entity<SampleData>
    {
        #region 对象操作
        static SampleData()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(DataId));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            Cost = (Int32)(EndTime - StartTime);
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
        public static SampleData FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据数据编号查找</summary>
        /// <param name="dataId"></param>
        /// <returns></returns>
        public static IList<SampleData> FindAllByDataId(Int32 dataId) => FindAll(_.DataId == dataId);

        /// <summary>根据跟踪标识查找</summary>
        /// <param name="traceId">跟踪标识</param>
        /// <returns>实体列表</returns>
        public static IList<SampleData> FindAllByTraceId(String traceId) => FindAll(_.TraceId == traceId);
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="dataId">数据</param>
        /// <param name="appId">应用</param>
        /// <param name="name">名称</param>
        /// <param name="traceId">跟踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</param>
        /// <param name="spanId">片段标识</param>
        /// <param name="parentId">父级标识</param>
        /// <param name="success">是否成功</param>
        /// <param name="start">时间开始</param>
        /// <param name="end">时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<SampleData> Search(Int32 dataId, Int32 appId, String name, String traceId, String spanId, String parentId, Boolean? success, Int64 start, Int64 end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (dataId >= 0) exp &= _.DataId == dataId;
            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Name == name;
            if (!traceId.IsNullOrEmpty()) exp &= _.TraceId == traceId;
            if (!spanId.IsNullOrEmpty()) exp &= _.SpanId == spanId;
            if (!parentId.IsNullOrEmpty()) exp &= _.ParentId == parentId;
            if (success != null) exp &= _.Success == success;
            if (start > 0) exp &= _.StartTime >= start;
            if (end > 0) exp &= _.StartTime <= end;
            //exp &= _.CreateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.TraceId == key | _.SpanId == key | _.ParentId == key | _.Tag.Contains(key) | _.Error.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(ID) as ID,TraceId From SampleData Where CreateTime>'2020-01-24 00:00:00' Group By TraceId Order By ID Desc limit 20
        static readonly FieldCache<SampleData> _TraceIdCache = new FieldCache<SampleData>(nameof(TraceId))
        {
            //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        };

        /// <summary>获取跟踪标识列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetTraceIdList() => _TraceIdCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>创建一批采样数据</summary>
        /// <param name="data"></param>
        /// <param name="spans"></param>
        /// <returns></returns>
        public static IList<SampleData> Create(ITraceData data, IList<ISpan> spans)
        {
            var list = new List<SampleData>();
            if (spans == null || spans.Count == 0) return list;

            foreach (var item in spans)
            {
                var sd = new SampleData
                {
                    DataId = data.ID,
                    AppId = data.AppId,
                    Name = data.Name,

                    TraceId = item.TraceId,
                    SpanId = item.Id,
                    ParentId = item.ParentId,

                    StartTime = item.StartTime,
                    EndTime = item.EndTime,

                    Tag = item.Tag,
                    Error = item.Error,

                    Success = item.Error.IsNullOrEmpty(),

                    CreateIP = data.CreateIP,
                    CreateTime = DateTime.Now,
                };
                list.Add(sd);
            }

            return list;
        }
        #endregion
    }
}