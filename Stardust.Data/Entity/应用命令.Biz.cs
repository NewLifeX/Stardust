using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting.Models;
using Stardust.Data.Nodes;
using XCode;

namespace Stardust.Data;

/// <summary>应用命令</summary>
public partial class AppCommand : Entity<AppCommand>
{
    #region 对象操作
    static AppCommand()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(AppId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        this.TrimExtraLong(__.Result);

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
    }
    #endregion

    #region 扩展属性
    /// <summary>应用</summary>
    [XmlIgnore, IgnoreDataMember]
    //[ScriptIgnore]
    public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(nameof(AppId), typeof(App), "Id")]
    public String AppName => App?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppCommand FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据应用、命令查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="command">命令</param>
    /// <returns>实体列表</returns>
    public static IList<AppCommand> FindAllByAppIdAndCommand(Int32 appId, String command)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Command.EqualIgnoreCase(command));

        return FindAll(_.AppId == appId & _.Command == command);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="command">命令</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppCommand> Search(Int32 appId, String command, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (!command.IsNullOrEmpty()) exp &= _.Command == command;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Command.Contains(key) | _.Argument.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From AppCommand Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<AppCommand> _CategoryCache = new FieldCache<AppCommand>(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>获取有效命令</summary>
    /// <param name="appId"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static IList<AppCommand> AcquireCommands(Int32 appId, Int32 count = 100)
    {
        var exp = new WhereExpression();
        if (appId > 0) exp &= _.AppId == appId;
        exp &= _.Status <= CommandStatus.处理中;

        return FindAll(exp, _.Id.Asc(), null, 0, count);
    }

    /// <summary>转为模型</summary>
    /// <returns></returns>
    public CommandModel ToModel()
    {
        return new CommandModel
        {
            Id = Id,
            Command = Command,
            Argument = Argument,
            Expire = Expire.Year > 2000 ? Expire.ToUniversalTime() : Expire,
            StartTime = StartTime.Year > 2000 ? StartTime.ToUniversalTime() : StartTime,
            TraceId = TraceId,
        };
    }
    #endregion
}