using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace Stardust.Data.Deployment;

/// <summary>部署历史。记录应用集部署历史</summary>
public partial class AppDeployHistory : Entity<AppDeployHistory>
{
    #region 对象操作
    static AppDeployHistory()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DeployId));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 截断日志
        var len = _.Remark.Length;
        if (len > 0 && !Remark.IsNullOrEmpty() && len > 0 && Remark.Length > len) Remark = Remark[..len];

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (Action.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Action), "操作不能为空！");

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppDeployHistory FindById(Int64 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据应用、编号查找</summary>
    /// <param name="deployId">应用</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployHistory> FindAllByDeployIdAndId(Int32 deployId, Int64 id)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId && e.Id == id);

        return FindAll(_.DeployId == deployId & _.Id == id);
    }

    /// <summary>根据节点、编号查找</summary>
    /// <param name="nodeId">节点</param>
    /// <param name="id">编号</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployHistory> FindAllByNodeIdAndId(Int32 nodeId, Int64 id)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeId == nodeId && e.Id == id);

        return FindAll(_.NodeId == nodeId & _.Id == id);
    }

    /// <summary>根据应用部署集查找</summary>
    /// <param name="deployId">应用部署集</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployHistory> FindAllByDeployId(Int32 deployId)
    {
        if (deployId <= 0) return new List<AppDeployHistory>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId);

        return FindAll(_.DeployId == deployId);
    }

    /// <summary>根据应用部署集、操作查找</summary>
    /// <param name="deployId">应用部署集</param>
    /// <param name="action">操作</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployHistory> FindAllByDeployIdAndAction(Int32 deployId, String action)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId && e.Action.EqualIgnoreCase(action));

        return FindAll(_.DeployId == deployId & _.Action == action);
    }

    /// <summary>根据节点查找</summary>
    /// <param name="nodeId">节点</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployHistory> FindAllByNodeId(Int32 nodeId)
    {
        if (nodeId <= 0) return new List<AppDeployHistory>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeId == nodeId);

        return FindAll(_.NodeId == nodeId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="deployId">应用</param>
    /// <param name="nodeId">应用</param>
    /// <param name="action">操作</param>
    /// <param name="key">关键字</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployHistory> Search(Int32 deployId, Int32 nodeId, String action, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (deployId >= 0) exp &= _.DeployId == deployId;
        if (!action.IsNullOrEmpty()) exp &= _.Action == action;
        if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key) | _.CreateIP.Contains(key);

        exp &= _.Id.Between(start, end, Meta.Factory.Snow);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>创建历史</summary>
    /// <param name="deployId"></param>
    /// <param name="nodeId"></param>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static AppDeployHistory Create(Int32 deployId, Int32 nodeId, String action, Boolean success, String remark, String ip)
    {
        var history = new AppDeployHistory
        {
            DeployId = deployId,
            NodeId = nodeId,

            Action = action,
            Success = success,
            Remark = remark,

            TraceId = DefaultSpan.Current?.TraceId,
            CreateTime = DateTime.Now,
            CreateIP = ip,
        };

        return history;
    }

    /// <summary>删除指定日期之前的数据</summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
    #endregion
}