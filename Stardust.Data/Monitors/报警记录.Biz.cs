using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Monitors;

/// <summary>报警状态</summary>
public enum AlarmStatuses
{
    /// <summary>报警中</summary>
    Alarming = 1,

    /// <summary>已恢复</summary>
    Recovered = 2,
}

/// <summary>报警记录。记录报警的开始、持续和恢复，统计报警持续时间</summary>
public partial class AlarmRecord : Entity<AlarmRecord>
{
    #region 对象操作
    static AlarmRecord()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(GroupId));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

        var len = _.Content.Length;
        if (len > 0 && !Content.IsNullOrEmpty() && Content.Length > len) Content = Content[..len];

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据告警组查找</summary>
    /// <param name="groupId">告警组</param>
    /// <returns>实体列表</returns>
    public static IList<AlarmRecord> FindAllByGroupId(Int32 groupId)
    {
        if (groupId <= 0) return new List<AlarmRecord>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.GroupId == groupId);

        return FindAll(_.GroupId == groupId);
    }
    #endregion

    #region 高级查询
    // Select Count(Id) as Id,Category From AlarmRecord Where UpdateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    static readonly FieldCache<AlarmRecord> _CategoryCache = new(nameof(Category));

    /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();

    /// <summary>高级查询</summary>
    /// <param name="groupId">告警组</param>
    /// <param name="category">类别</param>
    /// <param name="status">状态</param>
    /// <param name="start">时间开始</param>
    /// <param name="end">时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AlarmRecord> Search(Int32 groupId, String category, AlarmStatuses status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (groupId >= 0) exp &= _.GroupId == groupId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Category.Contains(key) | _.Action.Contains(key) | _.Content.Contains(key) | _.Creator.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
    #endregion
}
