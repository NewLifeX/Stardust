using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;

namespace Stardust.Data.Monitors;

/// <summary>采样数据2。采样备份，用于链路分析以及异常追踪</summary>
public partial class SampleData2 : Entity<SampleData2>
{
    #region 对象操作
    static SampleData2()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DataId));

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

        var len = _.TraceId.Length;
        if (len > 0 && !TraceId.IsNullOrEmpty() && TraceId.Length > len) TraceId = TraceId[..len];

        len = _.Tag.Length;
        if (len > 0 && !Tag.IsNullOrEmpty() && Tag.Length > len) Tag = Tag[..len];

        len = _.Error.Length;
        if (len > 0 && !Error.IsNullOrEmpty() && Error.Length > len) Error = Error[..len];

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);
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
    public static SampleData2 FindById(Int64 id)
    {
        if (id <= 0) return null;

        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        //// 单对象缓存
        //return Meta.SingleCache[id];

        return Find(_.Id == id);
    }

    /// <summary>根据追踪标识查找</summary>
    /// <param name="traceId">追踪标识</param>
    /// <returns>实体列表</returns>
    public static IList<SampleData2> FindAllByTraceId(String traceId) => FindAll(_.TraceId == traceId);
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="traceId">追踪标识。可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SampleData2> Search(String traceId, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!traceId.IsNullOrEmpty()) exp &= _.TraceId == traceId;
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.SpanId.Contains(key) | _.ParentId.Contains(key) | _.Tag.Contains(key) | _.Error.Contains(key) | _.CreateIP.Contains(key);

        return FindAll(exp, page);
    }

    public static IList<SampleData2> Search(String[] traceIds, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (traceIds != null && traceIds.Length > 0) exp &= _.TraceId.In(traceIds);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.SpanId.Contains(key) | _.ParentId.Contains(key) | _.Tag.Contains(key) | _.Error.Contains(key) | _.CreateIP.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>备份采样数据到备份表</summary>
    /// <param name="traceId"></param>
    /// <param name="samples"></param>
    /// <param name="userId"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public static IList<SampleData2> Backup(String traceId, IList<SampleData> samples, Int32 userId, String user)
    {
        var list = FindAllByTraceId(traceId);

        var list2 = new List<SampleData2>();
        foreach (var item in samples)
        {
            if (item.TraceId == traceId && !list.Any(e => e.Id == item.Id))
            {
                var entity = new SampleData2();
                entity.CopyFrom(item, true, false);

                entity.AppId = item.TraceItem?.AppId ?? 0;
                entity.ItemId = item.ItemId;
                entity.Name = item.Name;

                entity.CreateUserID = userId;
                entity.CreateUser = user;

                list.Add(entity);
                list2.Add(entity);
            }
        }

        list2.Insert(true);

        return list2;
    }
    #endregion
}