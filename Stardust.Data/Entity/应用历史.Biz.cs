using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
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
            // 累加字段
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(__.AppID);

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 在新插入数据或者修改了指定字段时进行修正
            // 处理当前已登录用户信息，可以由UserModule过滤器代劳
            /*var user = ManageProvider.User;
            if (user != null)
            {
                if (isNew && !Dirtys[nameof(CreateUserID)) nameof(CreateUserID) = user.ID;
            }*/
            //if (isNew && !Dirtys[nameof(CreateTime)]) nameof(CreateTime) = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) nameof(CreateIP) = ManageProvider.UserHost;
        }
        #endregion

        #region 扩展属性
        /// <summary>应用</summary>
        [XmlIgnore]
        //[ScriptIgnore]
        public App App => Extends.Get(nameof(App), k => App.FindByID(AppID));

        /// <summary>应用</summary>
        [XmlIgnore]
        //[ScriptIgnore]
        [DisplayName("应用")]
        [Map(__.AppID, typeof(App), "ID")]
        public String AppName => App?.Name;
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppHistory FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据应用查找</summary>
        /// <param name="appid">应用</param>
        /// <returns>实体列表</returns>
        public static IList<AppHistory> FindAllByAppID(Int32 appid)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppID == appid);

            return FindAll(_.AppID == appid);
        }
        #endregion

        #region 高级查询
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
                AppID = app.ID,
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