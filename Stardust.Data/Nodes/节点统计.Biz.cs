using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Nodes;

/// <summary>节点统计。每日统计</summary>
public partial class NodeStat : Entity<NodeStat>
{
    #region 对象操作
    static NodeStat()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.Total);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<TimeModule>();
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 在新插入数据或者修改了指定字段时进行修正
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;

        // 检查唯一索引
        // CheckExist(isNew, __.StatDate);
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected internal override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化NodeStat[节点统计]数据……");

    //    var entity = new NodeStat();
    //    entity.ID = 0;
    //    entity.StatDate = DateTime.Now;
    //    entity.Total = 0;
    //    entity.Actives = 0;
    //    entity.News = 0;
    //    entity.Registers = 0;
    //    entity.MaxOnline = 0;
    //    entity.MaxOnlineTime = DateTime.Now;
    //    entity.CreateTime = DateTime.Now;
    //    entity.UpdateTime = DateTime.Now;
    //    entity.Remark = "abc";
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化NodeStat[节点统计]数据！");
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
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static NodeStat FindByID(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>
    /// 根据日期查找
    /// </summary>
    /// <param name="category"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public static IList<NodeStat> FindAllByDate(String category, DateTime date)
    {
        if (date.Year < 2000) return new List<NodeStat>();

        date = date.Date;
        return FindAll(_.Category == category & _.StatDate == date);
    }

    /// <summary>按日期查</summary>
    /// <param name="category"></param>
    /// <param name="date"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static NodeStat FindByDate(String category, DateTime date, String key)
    {
        if (date.Year < 2000) return null;

        date = date.Date;
        return Find(_.Category == category & _.StatDate == date & _.Key == key);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="category">分类</param>
    /// <param name="key">统计项</param>
    /// <param name="start">统计日期开始</param>
    /// <param name="end">统计日期结束</param>
    /// <param name="keyWord">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<NodeStat> Search(String category, String key, DateTime start, DateTime end, String keyWord, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!key.IsNullOrEmpty()) exp &= _.Key == key;
        exp &= _.StatDate.Between(start, end);

        if (!keyWord.IsNullOrEmpty()) exp &= _.Remark.Contains(keyWord);

        return FindAll(exp, page);
    }

    /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
    static readonly Lazy<FieldCache<NodeStat>> CategoryCache = new(() => new FieldCache<NodeStat>(__.Category)
    {
        Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty,
        MaxRows = 50
    });

    /// <summary>获取所有类别名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllCategory() => CategoryCache.Value.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>
    /// 获取或添加指定天的统计
    /// </summary>
    /// <param name="category"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public static NodeStat GetOrAdd(String category, DateTime date, String key)
    {
        var kk = $"{category}#{date}#{key}";
        var entity = GetOrAdd(kk, k => FindByDate(category, date, key), k => new NodeStat
        {
            Category = category,
            StatDate = date,
            Key = key,
            CreateTime = DateTime.Now
        });

        entity.SaveAsync(5_000);

        return entity;
    }
    #endregion
}