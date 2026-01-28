using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using XCode;

namespace Stardust.Data.Deployment;

/// <summary>部署版本</summary>
public partial class AppDeployVersion : Entity<AppDeployVersion>
{
    #region 对象操作
    static AppDeployVersion()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DeployId));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<TraceInterceptor>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Version.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Version), "版本不能为空！");

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        // 在新插入数据或者修改了指定字段时进行修正
        // 处理当前已登录用户信息，可以由UserModule过滤器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (isNew && !Dirtys[nameof(CreateUserId)]) CreateUserId = user.ID;
            if (!Dirtys[nameof(UpdateUserId)]) UpdateUserId = user.ID;
        }*/
        //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(isNew, nameof(DeployId), nameof(Version));
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppDeployVersion FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据应用、版本查找</summary>
    /// <param name="deployId">应用</param>
    /// <param name="version">版本</param>
    /// <returns>实体对象</returns>
    public static AppDeployVersion FindByDeployIdAndVersion(Int32 deployId, String version)
    {
        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.DeployId == deployId && e.Version.EqualIgnoreCase(version));

        return Find(_.DeployId == deployId & _.Version == version);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="deployId">应用</param>
    /// <param name="count">个数</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployVersion> FindAllByDeployId(Int32 deployId, Int32 count = 20)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId).OrderByDescending(e => e.Id).Take(count).ToList();

        return FindAll(_.DeployId == deployId, _.Id.Desc(), null, 0, count);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deployId">应用</param>
    /// <param name="version">版本</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployVersion> Search(Int32 deployId, String version, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deployId >= 0) exp &= _.DeployId == deployId;
        if (!version.IsNullOrEmpty()) exp &= _.Version == version;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Url.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    #endregion
}