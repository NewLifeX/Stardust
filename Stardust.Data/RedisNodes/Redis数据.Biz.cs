using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using Stardust.Data.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Nodes
{
    /// <summary>Redis数据。Redis监控</summary>
    public partial class RedisData : Entity<RedisData>
    {
        #region 对象操作
        static RedisData()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(RedisId));

            // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
            Meta.Interceptors.Add<TimeInterceptor>();
            Meta.Interceptors.Add<IPInterceptor>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            // 在新插入数据或者修改了指定字段时进行修正
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        }

        ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected override void InitData()
        //{
        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    if (Meta.Session.Count > 0) return;

        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化RedisData[Redis数据]数据……");

        //    var entity = new RedisData();
        //    entity.Id = 0;
        //    entity.RedisId = 0;
        //    entity.Name = "abc";
        //    entity.Speed = 0;
        //    entity.InputKbps = 0;
        //    entity.OutputKbps = 0;
        //    entity.Uptime = 0;
        //    entity.ConnectedClients = 0;
        //    entity.UsedMemory = 0;
        //    entity.FragmentationRatio = 0.0;
        //    entity.Keys = 0;
        //    entity.ExpiredKeys = 0;
        //    entity.EvictedKeys = 0;
        //    entity.KeySpaceHits = 0;
        //    entity.KeySpaceMisses = 0;
        //    entity.Commands = 0;
        //    entity.Reads = 0;
        //    entity.Writes = 0;
        //    entity.AvgTtl = 0;
        //    entity.TopCommand = "abc";
        //    entity.Db0Keys = 0;
        //    entity.Db0Expires = 0;
        //    entity.Db1Keys = 0;
        //    entity.Db1Expires = 0;
        //    entity.Db2Keys = 0;
        //    entity.Db2Expires = 0;
        //    entity.Db3Keys = 0;
        //    entity.Db3Expires = 0;
        //    entity.CreateTime = DateTime.Now;
        //    entity.CreateIP = "abc";
        //    entity.Remark = "abc";
        //    entity.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化RedisData[Redis数据]数据！");
        //}

        ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
        ///// <returns></returns>
        //public override Int32 Insert()
        //{
        //    return base.Insert();
        //}

        ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
        ///// <returns></returns>
        //protected override Int32 OnDelete()
        //{
        //    return base.OnDelete();
        //}
        #endregion

        #region 扩展属性
        /// <summary>节点</summary>
        [XmlIgnore, IgnoreDataMember]
        public RedisNode Redis => Extends.Get(nameof(Redis), k => RedisNode.FindById(RedisId));

        /// <summary>节点</summary>
        [XmlIgnore, IgnoreDataMember]
        [Map(nameof(RedisId), typeof(RedisNode), "Id")]
        public String RedisName => Redis?.Name;

        /// <summary>开机时间</summary>
        [Map(nameof(Uptime))]
        public String UptimeName => TimeSpan.FromSeconds(Uptime).ToString().TrimEnd("0000").TrimStart("00:");

        /// <summary>平均存活时间</summary>
        [Map(nameof(AvgTtl))]
        public String AvgTtlName => TimeSpan.FromMilliseconds(AvgTtl).ToString().TrimEnd("0000").TrimStart("00:");
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static RedisData FindById(Int64 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据Redis节点、编号查找</summary>
        /// <param name="redisId">Redis节点</param>
        /// <param name="id">编号</param>
        /// <returns>实体列表</returns>
        public static IList<RedisData> FindAllByRedisIdAndId(Int32 redisId, Int64 id)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RedisId == redisId && e.Id == id);

            return FindAll(_.RedisId == redisId & _.Id == id);
        }

        /// <summary>获取最后一条Redis数据</summary>
        /// <param name="redisId"></param>
        /// <returns></returns>
        public static RedisData FindLast(Int32 redisId)
        {
            if (redisId <= 0) return null;

            return FindAll(_.RedisId == redisId, _.Id.Desc(), null, 0, 1).FirstOrDefault();
        }

    /// <summary>根据Redis节点查找</summary>
    /// <param name="redisId">Redis节点</param>
    /// <returns>实体列表</returns>
    public static IList<RedisData> FindAllByRedisId(Int32 redisId)
    {
        if (redisId <= 0) return new List<RedisData>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RedisId == redisId);

        return FindAll(_.RedisId == redisId);
    }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="redisId">Redis节点</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<RedisData> Search(Int32 redisId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (redisId >= 0) exp &= _.RedisId == redisId;
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.TopCommand.Contains(key) | _.Remark.Contains(key);
            exp &= _.Id.Between(start, end, Meta.Factory.Snow);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,Category From RedisData Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        //static readonly FieldCache<RedisData> _CategoryCache = new FieldCache<RedisData>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>从Redis信息填充字段</summary>
        /// <param name="inf"></param>
        public RedisDbEntry[] Fill(IDictionary<String, String> inf)
        {
            Speed = inf["instantaneous_ops_per_sec"].ToInt();
            InputKbps = inf["instantaneous_input_kbps"].ToDouble();
            OutputKbps = inf["instantaneous_output_kbps"].ToDouble();

            Uptime = inf["uptime_in_seconds"].ToInt();
            ConnectedClients = inf["connected_clients"].ToInt();

            UsedMemory = (Int32)(inf["used_memory_rss"].ToLong() / 1024 / 1024);

            FragmentationRatio = inf["mem_fragmentation_ratio"].ToDouble();

            ExpiredKeys = inf["expired_keys"].ToLong();
            EvictedKeys = inf["evicted_keys"].ToLong();
            KeySpaceHits = inf["keyspace_hits"].ToLong();
            KeySpaceMisses = inf["keyspace_misses"].ToLong();
            Commands = inf["total_commands_processed"].ToLong();
            Reads = inf["total_reads_processed"].ToLong();
            Writes = inf["total_writes_processed"].ToLong();

            // 命令统计。cmdstat_lpush:calls=40,usec=816,usec_per_call=20.40
            var cmds = inf.Where(e => e.Key.StartsWith("cmdstat_")).ToDictionary(e => e.Key.TrimStart("cmdstat_"), e => e.Value);
            if (cmds.Count > 0)
            {
                var dic = cmds.ToDictionary(e => e.Key, e => e.Value.Substring("calls=", ",").ToInt());
                var kv = dic.OrderByDescending(e => e.Value).First();
                TopCommand = $"{kv.Key}:{cmds[kv.Key]}";
            }

            // key统计
            var dbs = new RedisDbEntry[16];
            var sb = new StringBuilder();
            for (var i = 0; i < dbs.Length; i++)
            {
                if (inf.TryGetValue($"db{i}", out var db))
                {
                    var dic = db.SplitAsDictionary("=", ",");
                    dbs[i] = new RedisDbEntry
                    {
                        Keys = dic["keys"].ToInt(),
                        Expires = dic["expires"].ToInt(),
                        AvgTtl = dic["avg_ttl"].ToInt(),
                    };
                    sb.AppendLine($"db{i}:{db}");
                }
            }
            var dbs2 = dbs.Where(e => e != null).ToArray();
            Keys = dbs2.Sum(e => e.Keys);
            if (Keys > 0) AvgTtl = (Int32)(dbs2.Sum(e => (Int64)e.Keys * e.AvgTtl) / Keys);
            //if (dbs[0] != null)
            //{
            //    Db0Keys = dbs[0].Keys;
            //    Db0Expires = dbs[0].Expires;
            //}
            //if (dbs[1] != null)
            //{
            //    Db1Keys = dbs[1].Keys;
            //    Db1Expires = dbs[1].Expires;
            //}
            //if (dbs[2] != null)
            //{
            //    Db2Keys = dbs[2].Keys;
            //    Db2Expires = dbs[2].Expires;
            //}
            //if (dbs[3] != null)
            //{
            //    Db3Keys = dbs[3].Keys;
            //    Db3Expires = dbs[3].Expires;
            //}

            TraceId = DefaultSpan.Current?.TraceId;
            Remark = sb.ToString().Trim();

            return dbs;
        }

        /// <summary>删除指定日期之前的数据</summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
        #endregion
    }
}