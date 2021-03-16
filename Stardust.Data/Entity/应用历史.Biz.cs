using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data
{
    /// <summary>应用历史</summary>
    public partial class AppHistory : EntityBase<AppHistory>
    {
        #region 对象操作
        static AppHistory()
        {
            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore]
        //[ScriptIgnore]
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
        #endregion

        #region 高级查询
        /// <summary>查询</summary>
        /// <param name="appId"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="key"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public static IList<AppHistory> Search(Int32 appId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            exp &= _.Id.Between(start, end, Meta.Factory.Snow);

            if (!key.IsNullOrEmpty()) exp &= _.Action == key;

            return FindAll(exp, page);
        }
        #endregion

        #region 业务操作
        /// <summary>创建历史</summary>
        /// <param name="app"></param>
        /// <param name="action"></param>
        /// <param name="success"></param>
        /// <param name="remark"></param>
        /// <returns></returns>
        public static AppHistory Create(App app, String action, Boolean success, String remark)
        {
            var history = new AppHistory
            {
                AppId = app.Id,
                Version = app.Name,

                Action = action,
                Success = success,
                Remark = remark,

                CreateTime = DateTime.Now,
            };

            return history;
        }
        #endregion
    }
}