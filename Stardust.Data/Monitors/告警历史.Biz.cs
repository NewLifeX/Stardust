using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Monitors
{
    /// <summary>告警历史。记录告警内容</summary>
    public partial class AlarmHistory : Entity<AlarmHistory>
    {
        #region 对象操作
        static AlarmHistory()
        {
            Meta.Factory.Table.DataTable.InsertOnly = true;

            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(GroupId));

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

            //// 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            //if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

            var len = _.Content.Length;
            if (len > 0 && !Content.IsNullOrEmpty() && Content.Length > len) Content = Content[..len];

            len = _.Error.Length;
            if (len > 0 && !Error.IsNullOrEmpty() && Error.Length > len) Error = Error[..len];

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AlarmHistory FindById(Int64 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据告警组、编号查找</summary>
        /// <param name="groupId">告警组</param>
        /// <param name="id">编号</param>
        /// <returns>实体列表</returns>
        public static IList<AlarmHistory> FindAllByGroupIdAndId(Int32 groupId, Int64 id)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.GroupId == groupId && e.Id == id);

            return FindAll(_.GroupId == groupId & _.Id == id);
        }

    /// <summary>根据告警组查找</summary>
    /// <param name="groupId">告警组</param>
    /// <returns>实体列表</returns>
    public static IList<AlarmHistory> FindAllByGroupId(Int32 groupId)
    {
        if (groupId <= 0) return new List<AlarmHistory>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.GroupId == groupId);

        return FindAll(_.GroupId == groupId);
    }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="groupId">告警组</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AlarmHistory> Search(Int32 groupId, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (groupId >= 0) exp &= _.GroupId == groupId;
            exp &= _.Id.Between(start, end, Meta.Factory.Snow);
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.Category.Contains(key) | _.Action.Contains(key) | _.Content.Contains(key) | _.Creator.Contains(key);

            return FindAll(exp, page);
        }
        #endregion

        #region 业务操作
        /// <summary>删除指定日期之前的数据</summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static Int32 DeleteBefore(DateTime date) => Delete(_.Id < Meta.Factory.Snow.GetId(date));
        #endregion
    }
}