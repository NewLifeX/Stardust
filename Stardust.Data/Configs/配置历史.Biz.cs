using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Configs
{
    /// <summary>配置历史。记录配置变更历史</summary>
    public partial class ConfigHistory : Entity<ConfigHistory>
    {
        #region 对象操作
        static ConfigHistory()
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

            // 在新插入数据或者修改了指定字段时进行修正
            // 处理当前已登录用户信息，可以由UserModule过滤器代劳
            /*var user = ManageProvider.User;
            if (user != null)
            {
                if (isNew && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
            }*/
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static ConfigHistory FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="action">操作</param>
        /// <param name="success">成功</param>
        /// <param name="start">创建时间开始</param>
        /// <param name="end">创建时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<ConfigHistory> Search(Int32 appId, String action, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!action.IsNullOrEmpty()) exp &= _.Action == action;
            if (success != null) exp &= _.Success == success;
            exp &= _.CreateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Remark.Contains(key) | _.CreateIP.Contains(key);

            return FindAll(exp, page);
        }

        /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
        static Lazy<FieldCache<ConfigHistory>> ActionCache = new Lazy<FieldCache<ConfigHistory>>(() => new FieldCache<ConfigHistory>(__.Action)
        {
            MaxRows = 50
        });

        /// <summary>获取所有类别名称</summary>
        /// <returns></returns>
        public static IDictionary<String, String> FindAllActions() => ActionCache.Value.FindAllName().OrderByDescending(e => e.Key).ToDictionary(e => e.Key, e => e.Value);
        #endregion

        #region 业务操作
        /// <summary>新增历史</summary>
        /// <param name="appId"></param>
        /// <param name="action"></param>
        /// <param name="remark"></param>
        /// <returns></returns>
        public static ConfigHistory Add(Int32 appId, String action, String remark)
        {
            if (appId == 0) throw new ArgumentNullException(nameof(appId));
            if (action.IsNullOrEmpty()) throw new ArgumentNullException(nameof(action));

            var hi = new ConfigHistory
            {
                AppId = appId,
                Action = action,
                Remark = remark,
            };
            hi.Insert();

            return hi;
        }
        #endregion
    }
}