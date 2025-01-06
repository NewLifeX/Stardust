using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using XCode.Shards;

namespace Stardust.Data.Monitors;

/// <summary>采样数据。具体调用或异常详情</summary>
public partial class SampleData : Entity<SampleData>
{
    #region 对象操作
    static SampleData()
    {
        // 配置自动分表策略，一般在实体类静态构造函数中配置
        Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
        {
            ConnPolicy = "{0}",
            TablePolicy = "{0}_{1:dd}",
            Step = TimeSpan.FromDays(1),
        };

        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DataId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();

        // 针对Mysql启用压缩表
        var table = Meta.Table.DataTable;
        table.Properties["ROW_FORMAT"] = "COMPRESSED";
        table.Properties["KEY_BLOCK_SIZE"] = "4";
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        var len = _.TraceId.Length;
        if (len > 0 && !TraceId.IsNullOrEmpty() && TraceId.Length > len) TraceId = TraceId[..len];

        len = _.Tag.Length;
        if (len > 0 && !Tag.IsNullOrEmpty() && Tag.Length > len) Tag = Tag[..len];

        len = _.Error.Length;
        if (len > 0 && !Error.IsNullOrEmpty() && Error.Length > len) Error = Error[..len];

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        Cost = (Int32)(EndTime - StartTime);
    }
    #endregion

    #region 扩展属性
    /// <summary>跟踪项</summary>
    [XmlIgnore, IgnoreDataMember]
    public TraceItem TraceItem => Extends.Get(nameof(TraceItem), k => TraceItem.FindById(ItemId));

    /// <summary>跟踪项</summary>
    [Map(nameof(ItemId))]
    public String Name => TraceItem + "";

    /// <summary>应用编号</summary>
    [XmlIgnore, IgnoreDataMember]
    public Int32 AppId => TraceItem?.AppId ?? 0;

    /// <summary>应用</summary>
    public String AppName => TraceItem?.AppName;

    /// <summary>开始时间</summary>
    [Map(nameof(StartTime))]
    public DateTime Start => StartTime.ToDateTime().ToLocalTime();

    /// <summary>结束时间</summary>
    [Map(nameof(EndTime))]
    public DateTime End => EndTime.ToDateTime().ToLocalTime();

    /// <summary>层次深度</summary>
    [XmlIgnore, IgnoreDataMember]
    public Int32 Level { get; set; }
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static SampleData FindById(Int64 id)
    {
        if (id <= 0) return null;

        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        //// 单对象缓存
        //return Meta.SingleCache[id];

        return Find(_.Id == id);
    }

    /// <summary>根据数据编号查找</summary>
    /// <param name="dataId"></param>
    /// <returns></returns>
    public static IList<SampleData> FindAllByDataId(Int64 dataId) => FindAll(_.DataId == dataId);

    /// <summary>根据数据编号查找</summary>
    /// <param name="dataIds"></param>
    /// <param name="date">分表日期</param>
    /// <returns></returns>
    public static IList<SampleData> FindAllByDataIds(Int64[] dataIds, DateTime date)
    {
        if (date.Year < 2000) throw new ArgumentOutOfRangeException(nameof(date));

        using var split = Meta.CreateShard(date);

        return FindAll(_.DataId.In(dataIds));
    }

    /// <summary>根据追踪查找</summary>
    /// <param name="traceId">追踪</param>
    /// <returns>实体列表</returns>
    public static IList<SampleData> FindAllByTraceId(String traceId)
    {
        if (traceId.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TraceId.EqualIgnoreCase(traceId));

        return FindAll(_.TraceId == traceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="dataId">数据</param>
    /// <param name="traceId">追踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</param>
    /// <param name="success">是否成功</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SampleData> Search(Int64 dataId, String traceId, Int32 itemId, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (dataId > 0) exp &= _.DataId == dataId;
        if (!traceId.IsNullOrEmpty()) exp &= _.TraceId == traceId;
        if (success != null) exp &= _.Success == success;

        if (!key.IsNullOrEmpty()) exp &= _.Tag.Contains(key) | _.Error.Contains(key);

        // 时间区间倒序，为了从后往前查
        return Meta.AutoShard(end.AddSeconds(1), start, () => FindAll(exp, page))
            .FirstOrDefault(e => e.Count > 0) ?? [];
    }

    /// <summary>高级查询</summary>
    /// <param name="dataId">数据</param>
    /// <param name="traceId">追踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</param>
    /// <param name="success">是否成功</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SampleData> Search(Int64 dataId, String traceId, Boolean? success, DateTime start, DateTime end, PageParameter page)
    {
        var exp = new WhereExpression();

        if (dataId > 0) exp &= _.DataId == dataId;
        if (!traceId.IsNullOrEmpty()) exp &= _.TraceId == traceId;
        if (success != null) exp &= _.Success == success;

        // 时间区间倒序，为了从后往前查
        return Meta.AutoShard(end.AddSeconds(1), start, () => FindAll(exp, page))
            .FirstOrDefault(e => e.Count > 0) ?? [];
    }

    public static IList<SampleData> Search(Int64 dataId, String[] traceIds, Boolean? success, DateTime start, DateTime end, PageParameter page)
    {
        var exp = new WhereExpression();

        if (dataId > 0) exp &= _.DataId == dataId;
        if (traceIds != null && traceIds.Length > 0) exp &= _.TraceId.In(traceIds);
        if (success != null) exp &= _.Success == success;

        // 时间区间倒序，为了从后往前查
        return Meta.AutoShard(end.AddSeconds(1), start, () => FindAll(exp, page))
            .FirstOrDefault(e => e.Count > 0) ?? [];
    }

    public static SelectBuilder SearchSql(Int32 itemId, String clientId, DateTime start, DateTime end, String key)
    {
        var exp = new WhereExpression();

        if (itemId > 0) exp &= _.ItemId == itemId;
        if (!clientId.IsNullOrEmpty()) exp &= _.ClientId == clientId;

        if (!key.IsNullOrEmpty()) exp &= _.Tag.Contains(key) | _.Error.Contains(key);

        exp &= _.Id.Between(start, end, Meta.Factory.Snow);

        // 时间区间倒序，为了从后往前查
        return Meta.AutoShard(end.AddSeconds(1), start, () => FindSQL(exp, null, _.DataId))
            .FirstOrDefault();
    }
    #endregion

    #region 业务操作
    /// <summary>创建一批采样数据</summary>
    /// <param name="data"></param>
    /// <param name="spans"></param>
    /// <param name="success"></param>
    /// <returns></returns>
    public static IList<SampleData> Create(TraceData data, IList<ISpan> spans, Boolean success)
    {
        var list = new List<SampleData>();
        if (spans == null || spans.Count == 0) return list;

        var snow = Meta.Factory.Snow;
        foreach (var item in spans)
        {
            var sd = new SampleData
            {
                Id = snow.NewId(),
                DataId = data.Id,
                //AppId = data.AppId,
                ItemId = data.ItemId,
                ClientId = data.ClientId,
                //Name = data.Name,

                TraceId = item.TraceId,
                SpanId = item.Id,
                ParentId = item.ParentId,

                StartTime = item.StartTime,
                EndTime = item.EndTime,
                Value = item.Value,

                Tag = item.Tag,
                Error = item.Error,

                Success = success,

                CreateIP = data.CreateIP,
                CreateTime = DateTime.Now,
            };
            list.Add(sd);
        }

        return list;
    }

    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date)
    {
        //Delete(_.Id < Meta.Factory.Snow.GetId(date));

        var snow = Meta.Factory.Snow;
        var whereExp = _.Id < snow.GetId(date);

        // 使用底层接口，分批删除
        return Delete(whereExp);
    }
    #endregion
}