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

namespace Stardust.Data.Nodes
{
    /// <summary>Redis消息队列。Redis消息队列状态监控</summary>
    public partial class RedisMessageQueue : Entity<RedisMessageQueue>
    {
        #region 对象操作
        static RedisMessageQueue()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(RedisId));

            // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
            Meta.Interceptors.Add<UserInterceptor>();
            Meta.Interceptors.Add<TimeInterceptor>();
            Meta.Interceptors.Add<IPInterceptor>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;
             
            var len = _.Groups.Length;
            if (Groups != null && len > 0 && Groups.Length > len) Groups = Groups[..len];
            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
        }
        #endregion

        #region 扩展属性
        /// <summary>节点</summary>
        [XmlIgnore, IgnoreDataMember]
        public RedisNode Redis => Extends.Get(nameof(Redis), k => RedisNode.FindById(RedisId));

        /// <summary>节点</summary>
        [XmlIgnore, IgnoreDataMember]
        [Map(nameof(RedisId), typeof(RedisNode), "Id")]
        public String RedisName => Redis?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static RedisMessageQueue FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据Redis节点查找</summary>
        /// <param name="redisId">Redis节点</param>
        /// <returns>实体列表</returns>
        public static IList<RedisMessageQueue> FindAllByRedisId(Int32 redisId)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RedisId == redisId);

            return FindAll(_.RedisId == redisId);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="redisId">Redis节点</param>
        /// <param name="category">分类</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<RedisMessageQueue> Search(Int32 redisId, String category, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (redisId >= 0) exp &= _.RedisId == redisId;
            if (!category.IsNullOrEmpty()) exp &= _.Category == category;

            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Type.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.UpdateUser.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,Category From RedisNode Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        static readonly FieldCache<RedisMessageQueue> _CategoryCache = new FieldCache<RedisMessageQueue>(nameof(Category));

        /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        #endregion
    }
}