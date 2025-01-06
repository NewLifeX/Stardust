using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace Stardust.Data;

/// <summary>应用历史</summary>
public partial class AppHistory : Entity<AppHistory>
{
    #region 对象操作
    static AppHistory()
    {
        Meta.Factory.Table.DataTable.InsertOnly = true;

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        // 针对Mysql启用压缩表
        var table = Meta.Table.DataTable;
        table.Properties["ROW_FORMAT"] = "COMPRESSED";
        table.Properties["KEY_BLOCK_SIZE"] = "4";
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        var len = _.Remark.Length;
        if (len > 0 && !Remark.IsNullOrEmpty() && Remark.Length > len) Remark = Remark[..len];

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
    }
    #endregion

    #region 扩展属性
    /// <summary>应用</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(__.AppId, typeof(App), "Id")]
    public String AppName => App?.Name;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppHistory FindById(Int64 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据应用、客户端查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="client">客户端</param>
    /// <returns>实体列表</returns>
    public static IList<AppHistory> FindAllByAppIdAndClient(Int32 appId, String client)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Client.EqualIgnoreCase(client));

        return FindAll(_.AppId == appId & _.Client == client);
    }

    /// <summary>根据应用、操作查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="action">操作</param>
    /// <returns>实体列表</returns>
    public static IList<AppHistory> FindAllByAppIdAndAction(Int32 appId, String action)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Action.EqualIgnoreCase(action));

        return FindAll(_.AppId == appId & _.Action == action);
    }
    #endregion

    #region 高级查询
    /// <summary>查询</summary>
    /// <param name="appId"></param>
    /// <param name="client"></param>
    /// <param name="action"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<AppHistory> Search(Int32 appId, String client, String action, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (!client.IsNullOrEmpty()) exp &= _.Client == client;
        if (!action.IsNullOrEmpty()) exp &= _.Action == action;

        exp &= _.Id.Between(start, end, Meta.Factory.Snow);

        if (!key.IsNullOrEmpty()) exp &= _.Action == key | _.Client.StartsWith(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>创建历史</summary>
    /// <param name="app"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="version"></param>
    /// <param name="creator"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static AppHistory Create(App app, String action, Boolean success, String remark, String version, String creator, String ip)
    {
        app ??= new App();
        if (version.IsNullOrEmpty()) version = app.Version;

        var history = new AppHistory
        {
            AppId = app.Id,

            Action = action,
            Success = success,
            Version = version,
            Remark = remark,

            TraceId = DefaultSpan.Current?.TraceId,
            Creator = creator,
            CreateTime = DateTime.Now,
            CreateIP = ip,
        };

        //history.SaveAsync();

        return history;
    }

    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
    #endregion
}