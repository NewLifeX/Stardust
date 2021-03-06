﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Deployment
{
    /// <summary>部署在线。应用已部署的在运行中进程的在线记录</summary>
    public partial class AppDeployOnline : Entity<AppDeployOnline>
    {
        #region 对象操作
        static AppDeployOnline()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(AppId));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            // 在新插入数据或者修改了指定字段时进行修正
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public AppDeploy App => Extends.Get(nameof(App), k => AppDeploy.FindById(AppId));

        /// <summary>应用</summary>
        [Map(__.AppId)]
        public String AppName => App?.Name;

        /// <summary>节点</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeId));

        /// <summary>节点</summary>
        [Map(__.NodeId)]
        public String NodeName => Node?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppDeployOnline FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据应用查找</summary>
        /// <param name="appId">应用</param>
        /// <returns>实体列表</returns>
        public static IList<AppDeployOnline> FindAllByAppId(Int32 appId)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

            return FindAll(_.AppId == appId);
        }

        /// <summary>根据节点查找</summary>
        /// <param name="nodeId">节点</param>
        /// <returns>实体列表</returns>
        public static IList<AppDeployOnline> FindAllByNodeId(Int32 nodeId)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeId == nodeId);

            return FindAll(_.NodeId == nodeId);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用。原始应用</param>
        /// <param name="nodeId">节点。节点服务器</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppDeployOnline> Search(Int32 appId, Int32 nodeId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (nodeId >= 0) exp &= _.NodeId == nodeId;
            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.IP.Contains(key) | _.CreateIP.Contains(key);

            return FindAll(exp, page);
        }
        #endregion

        #region 业务操作
        #endregion
    }
}