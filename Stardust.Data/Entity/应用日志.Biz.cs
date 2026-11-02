using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;
using XCode.Shards;

namespace Stardust.Data
{
    /// <summary>应用日志</summary>
    public partial class AppClientLog : Entity<AppClientLog>
    {
        #region 对象操作
        static AppClientLog()
        {
            // 分表分库
            Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
            {
                ConnPolicy = "{0}_{1:yyyyMM}",
                TablePolicy = "{0}_{1:dd}",
            };

            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(AppId));

            // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
            Meta.Interceptors.Add<TimeInterceptor>();
            Meta.Interceptors.Add<IPInterceptor>();

            //// 分表分库
            //Meta.ShardConnName = e => $"AppLog_{e.CreateTime:yyyyMM}";
            //Meta.ShardTableName = e => $"AppLog_{e.CreateTime:yyyyMMdd}";
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
        #endregion

        #region 扩展属性
        ///// <summary>应用</summary>
        //[XmlIgnore, IgnoreDataMember]
        ////[ScriptIgnore]
        //public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

        ///// <summary>应用</summary>
        //[XmlIgnore, IgnoreDataMember]
        ////[ScriptIgnore]
        //[DisplayName("应用")]
        //[Map(nameof(AppId), typeof(App), "ID")]
        //public String AppName => App?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppClientLog FindById(Int64 id)
        {
            if (id <= 0) return null;

            // 分表
            using var split = Meta.CreateShard(id);

            return Find(_.Id == id);
        }

    /// <summary>根据应用、编号查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<AppClientLog> FindAllByAppIdAndId(Int32 appId, Int64 id)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Id == id);

        return FindAll(_.AppId == appId & _.Id == id);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="appId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<AppClientLog> FindAllByAppId(Int32 appId)
    {
        if (appId <= 0) return new List<AppClientLog>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="clientId">客户端</param>
        /// <param name="threadId">线程</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppClientLog> Search(Int32 appId, String clientId, Int32 threadId, DateTime start, DateTime end, String key, PageParameter page)
            => Search(appId, clientId, threadId, start, end, key, null, page);

        /// <summary>高级查询（含日志级别过滤）</summary>
        /// <param name="appId">应用</param>
        /// <param name="clientId">客户端</param>
        /// <param name="threadId">线程</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="kind">日志级别（Info/Warn/Error/Fatal），空=不过滤</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppClientLog> Search(Int32 appId, String clientId, Int32 threadId, DateTime start, DateTime end, String key, String kind, PageParameter page)
            => Search(appId, clientId, threadId > 0 ? threadId + "" : null, start, end, key, kind, page);

        /// <summary>高级查询（含日志级别过滤）。线程编号按字符串比较，与实体 ThreadId 字符串类型一致</summary>
        /// <param name="appId">应用</param>
        /// <param name="clientId">客户端</param>
        /// <param name="threadId">线程</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="kind">日志级别（Info/Warn/Error/Fatal），空=不过滤</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppClientLog> Search(Int32 appId, String clientId, String threadId, DateTime start, DateTime end, String key, String kind, PageParameter page)
        {
            if (appId <= 0 || start.Year < 2000) return new List<AppClientLog>();

            var exp = new WhereExpression();

            // 分表实体（按月分库 StardustData_yyyyMM + 按天分表 AppClientLog_dd）：
            // 必须带 Id 时间窗，TimeShardPolicy 才能定位分库/分表；无时间条件会打到不存在的基础表
            exp &= _.Id.Between(start, end, Meta.Factory.Snow);

            if (appId >= 0) exp &= _.AppId == appId;
            if (!clientId.IsNullOrEmpty()) exp &= _.ClientId == clientId;
            if (!threadId.IsNullOrEmpty()) exp &= _.ThreadId == threadId;
            if (!kind.IsNullOrEmpty()) exp &= _.Kind == kind;
            if (!key.IsNullOrEmpty()) exp &= _.Message.Contains(key) | _.ClientId == key;

            return FindAll(exp, page);
        }

        /// <summary>分表感知安全查询（带时间窗），缺表环境返回空列表而非抛异常。
        /// 分表数据只落分表（写入时自动创建），基础表不存在；全新环境首次查询可能得到「表不存在」错误。</summary>
        /// <param name="note">空数据原因说明；正常查询时为 null</param>
        public static IList<AppClientLog> SearchSafe(Int32 appId, DateTime start, DateTime end, String key, String kind, PageParameter page, out String note)
            => SearchSafe(appId, null, null, start, end, key, kind, page, out note);

        /// <summary>分表感知安全查询（带时间窗，支持客户端/线程过滤），缺表环境返回空列表而非抛异常。</summary>
        /// <param name="appId">应用</param>
        /// <param name="clientId">客户端</param>
        /// <param name="threadId">线程</param>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <param name="key">关键字</param>
        /// <param name="kind">日志级别（Info/Warn/Error/Fatal），空=不过滤</param>
        /// <param name="page">分页参数信息</param>
        /// <param name="note">空数据原因说明；正常查询时为 null</param>
        public static IList<AppClientLog> SearchSafe(Int32 appId, String clientId, String threadId, DateTime start, DateTime end, String key, String kind, PageParameter page, out String note)
        {
            note = null;
            try
            {
                return Search(appId, clientId, threadId, start, end, key, kind, page);
            }
            catch (Exception ex) when (IsTableMissing(ex))
            {
                note = "日志分表尚未创建（该应用在时间窗内没有上报过客户端日志）";
                return new List<AppClientLog>();
            }
        }

        /// <summary>判断异常是否为「分表/分库尚不存在」（兼容 SqlServer/SQLite/MySql/PgSql 等缺表话术）。</summary>
        public static Boolean IsTableMissing(Exception ex)
        {
            var msg = ex?.Message ?? "";
            return msg.Contains("Invalid object name")   // SqlServer：基础表/分表未创建
                || msg.Contains("Cannot open database")  // SqlServer：分库未创建
                || msg.Contains("no such table")         // SQLite
                || msg.Contains("doesn't exist")         // MySql
                || msg.Contains("does not exist");       // PostgreSql/Oracle
        }

        // Select Count(Id) as Id,Category From AppLog Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        //static readonly FieldCache<AppLog> _CategoryCache = new FieldCache<AppLog>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>创建日志</summary>
        /// <param name="appId">应用编号</param>
        /// <param name="clientId">客户端标识</param>
        /// <param name="ss">日志行头部的四段，依次为时间、线程、类型、名称</param>
        /// <param name="message">日志消息，含后续续行</param>
        /// <param name="ip">来源地址</param>
        /// <returns>日志对象</returns>
        public static AppClientLog Create(Int32 appId, String clientId, String[] ss, String message, String ip)
        {
            var log = new AppClientLog
            {
                AppId = appId,
                ClientId = clientId,
                Time = ss[0],
                ThreadId = ss[1],
                Kind = ss[2],
                Name = ss[3],
                Message = message,
                CreateTime = DateTime.Now,
                CreateIP = ip
            };

            //// 分表
            //using var split = Meta.CreateSplit($"AppLog_{log.CreateTime:yyyyMMdd}", $"AppLog_{appId}");

            log.SaveAsync();
            //log.Insert();

            return log;
        }
        #endregion
    }
}