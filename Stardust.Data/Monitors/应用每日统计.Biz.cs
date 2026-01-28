using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors;

/// <summary>应用每日统计。每应用每日统计，用于分析应用健康状况</summary>
public partial class AppDayStat : Entity<AppDayStat>
{
    #region 对象操作
    static AppDayStat()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        var df = Meta.Factory.AdditionalFields;
        df.Add(nameof(Total));
        df.Add(nameof(Errors));
        df.Add(nameof(TotalCost));

        df.Add(nameof(Apis));
        df.Add(nameof(Https));
        df.Add(nameof(Dbs));
        df.Add(nameof(Mqs));
        df.Add(nameof(Redis));
        df.Add(nameof(Others));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        base.Valid(isNew);

        //Cost = Total == 0 ? 0 : (Int32)(TotalCost / Total);
        ErrorRate = Total == 0 ? 0 : Math.Round((Double)Errors / Total, 4);

        if (!Dirtys[nameof(Cost)])
        {
            // 为了让平均值逼近TP99，避免毛刺干扰，减去最大值再取平均
            if (Total >= 50)
                Cost = (Int32)Math.Round((Double)(TotalCost - MaxCost) / (Total - 1));
            else
                Cost = Total == 0 ? 0 : (Int32)Math.Round((Double)TotalCost / Total);
        }
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
    public static AppDayStat FindByID(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>
    /// 根据应用查找
    /// </summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static IList<AppDayStat> FindAllByAppId(Int32 appId)
    {
        if (appId <= 0) return new List<AppDayStat>();

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据统计日期、应用查找</summary>
    /// <param name="statDate">统计日期</param>
    /// <param name="appId">应用</param>
    /// <returns>实体对象</returns>
    public static AppDayStat FindByStatDateAndAppId(DateTime statDate, Int32 appId)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.StatDate == statDate && e.AppId == appId);

        return Find(_.StatDate == statDate & _.AppId == appId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="start">统计日期开始</param>
    /// <param name="end">统计日期结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppDayStat> Search(Int32 appId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        exp &= _.StatDate.Between(start, end);

        return FindAll(exp, page);
    }

    /// <summary>查找一批统计</summary>
    /// <param name="date"></param>
    /// <param name="appIds"></param>
    /// <returns></returns>
    public static IList<AppDayStat> Search(DateTime date, Int32[] appIds)
    {
        var exp = new WhereExpression();
        if (date.Year > 2000) exp &= _.StatDate == date;
        if (appIds != null && appIds.Length > 0) exp &= _.AppId.In(appIds);

        return FindAll(exp, new PageParameter { PageSize = 1000 });
    }
    #endregion

    #region 业务操作
    #endregion
}