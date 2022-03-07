using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Deployment
{
    /// <summary>部署历史。记录应用集部署历史</summary>
    public partial class AppDeployHistory : Entity<AppDeployHistory>
    {
        #region 对象操作
        static AppDeployHistory()
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

            // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            if (Action.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Action), "操作不能为空！");

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public AppDeploy App => Extends.Get(nameof(App), k => AppDeploy.FindById(AppId));

        /// <summary>应用</summary>
        [Map(__.AppId)]
        public String AppName => App?.Name;
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
        /// <param name="appId">应用</param>
        /// <param name="id">编号</param>
        /// <returns>实体列表</returns>
        public static IList<AppDeployHistory> FindAllByAppIdAndId(Int32 appId, Int64 id)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId && e.Id == id);

            return FindAll(_.AppId == appId & _.Id == id);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="action">操作</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppDeployHistory> Search(Int32 appId,  String action, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!action.IsNullOrEmpty()) exp &= _.Action == action;
            if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key) | _.CreateIP.Contains(key);

            return FindAll(exp, page);
        }
        #endregion

        #region 业务操作
        #endregion
    }
}