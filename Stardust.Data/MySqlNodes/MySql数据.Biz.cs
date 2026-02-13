using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Stardust.Data.Nodes;

public partial class MySqlData : Entity<MySqlData>
{
    #region 对象操作
    static MySqlData()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(MySqlId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<TraceInterceptor>();

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 保留2位小数
        //InnodbBufferPoolHitRate = Math.Round(InnodbBufferPoolHitRate, 2);
        //KeyCacheHitRate = Math.Round(KeyCacheHitRate, 2);
        //QueryCacheHitRate = Math.Round(QueryCacheHitRate, 2);
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化MySqlData[MySql数据]数据……");

    //    var entity = new MySqlData();
    //    entity.Id = 0;
    //    entity.MySqlId = 0;
    //    entity.Name = "abc";
    //    entity.Uptime = 0;
    //    entity.Connections = 0;
    //    entity.MaxConnections = 0;
    //    entity.Qps = 0;
    //    entity.Tps = 0;
    //    entity.BytesReceived = 0;
    //    entity.BytesSent = 0;
    //    entity.InnodbBufferPoolSize = 0;
    //    entity.InnodbBufferPoolPages = 0;
    //    entity.InnodbBufferPoolHitRate = 0.0;
    //    entity.KeyCacheHitRate = 0.0;
    //    entity.QueryCacheHitRate = 0.0;
    //    entity.SlowQueries = 0;
    //    entity.OpenTables = 0;
    //    entity.TableLocksWaited = 0;
    //    entity.Threads = 0;
    //    entity.ThreadsRunning = 0;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化MySqlData[MySql数据]数据！");
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
    public MySqlNode MySql => Extends.Get(nameof(MySql), k => MySqlNode.FindById(MySqlId));

    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember]
    [Map(nameof(MySqlId), typeof(MySqlNode), "Id")]
    public String MySqlName => MySql?.Name;

    /// <summary>开机时间</summary>
    [Map(nameof(Uptime))]
    public String UptimeName => TimeSpan.FromSeconds(Uptime).ToString().TrimEnd("0000").TrimStart("00:");
    #endregion

    #region 扩展查询
    /// <summary>获取最后一条MySql数据</summary>
    /// <param name="mysqlId">MySql节点</param>
    /// <returns></returns>
    public static MySqlData FindLast(Int32 mysqlId)
    {
        if (mysqlId <= 0) return null;

        return FindAll(_.MySqlId == mysqlId, _.Id.Desc(), null, 0, 1).FirstOrDefault();
    }
    #endregion

    #region 高级查询
    #endregion

    #region 业务操作
    /// <summary>从MySQL状态信息填充字段</summary>
    /// <param name="status">MySQL状态信息</param>
    public void Fill(IDictionary<String, String> status)
    {
        if (status == null || status.Count == 0) return;

        Uptime = status.TryGetValue("Uptime", out var val) ? val.ToInt() : 0;
        Connections = status.TryGetValue("Threads_connected", out val) ? val.ToInt() : 0;
        MaxConnections = status.TryGetValue("Max_used_connections", out val) ? val.ToInt() : 0;

        // 计算QPS和TPS
        var questions = status.TryGetValue("Questions", out val) ? val.ToLong() : 0;
        var comCommit = status.TryGetValue("Com_commit", out val) ? val.ToLong() : 0;
        var comRollback = status.TryGetValue("Com_rollback", out val) ? val.ToLong() : 0;

        if (Uptime > 0)
        {
            Qps = (Int32)(questions / Uptime);
            Tps = (Int32)((comCommit + comRollback) / Uptime);
        }

        BytesReceived = status.TryGetValue("Bytes_received", out val) ? val.ToLong() : 0;
        BytesSent = status.TryGetValue("Bytes_sent", out val) ? val.ToLong() : 0;

        // InnoDB缓冲池统计
        InnodbBufferPoolSize = status.TryGetValue("Innodb_buffer_pool_bytes_data", out val) ? val.ToLong() / 1024 / 1024 : 0;
        InnodbBufferPoolPages = status.TryGetValue("Innodb_buffer_pool_pages_total", out val) ? val.ToInt() : 0;

        var poolReads = status.TryGetValue("Innodb_buffer_pool_reads", out val) ? val.ToLong() : 0;
        var poolReadRequests = status.TryGetValue("Innodb_buffer_pool_read_requests", out val) ? val.ToLong() : 0;
        if (poolReadRequests > 0)
            InnodbBufferPoolHitRate = Math.Round((Double)(poolReadRequests - poolReads) / poolReadRequests * 100, 2);

        // 键缓存命中率
        var keyReads = status.TryGetValue("Key_reads", out val) ? val.ToLong() : 0;
        var keyReadRequests = status.TryGetValue("Key_read_requests", out val) ? val.ToLong() : 0;
        if (keyReadRequests > 0)
            KeyCacheHitRate = Math.Round((Double)(keyReadRequests - keyReads) / keyReadRequests * 100, 2);

        // 查询缓存命中率
        var qcacheHits = status.TryGetValue("Qcache_hits", out val) ? val.ToLong() : 0;
        var comSelect = status.TryGetValue("Com_select", out val) ? val.ToLong() : 0;
        if (qcacheHits + comSelect > 0)
            QueryCacheHitRate = Math.Round((Double)qcacheHits / (qcacheHits + comSelect) * 100, 2);

        SlowQueries = status.TryGetValue("Slow_queries", out val) ? val.ToLong() : 0;
        OpenTables = status.TryGetValue("Open_tables", out val) ? val.ToInt() : 0;
        TableLocksWaited = status.TryGetValue("Table_locks_waited", out val) ? val.ToLong() : 0;
        Threads = status.TryGetValue("Threads_created", out val) ? val.ToInt() : 0;
        ThreadsRunning = status.TryGetValue("Threads_running", out val) ? val.ToInt() : 0;

        TraceId = DefaultSpan.Current?.TraceId;

        // 将关键状态信息存储到备注
        var sb = new StringBuilder();
        foreach (var kv in status.Where(e => e.Key.Contains("Innodb") || e.Key.Contains("Buffer") || e.Key.Contains("Cache")))
        {
            sb.AppendLine($"{kv.Key}:{kv.Value}");
        }
        Remark = sb.ToString().Trim().Cut(500);
    }

    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date">日期</param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
    #endregion
}
