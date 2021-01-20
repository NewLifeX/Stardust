using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Serialization;
using XCode;
using XCode.Membership;

namespace Stardust.Data.ConfigCenter
{
    /// <summary>配置数据</summary>
    public partial class ConfigData : Entity<ConfigData>
    {
        #region 对象操作
        static ConfigData()
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
            if (Key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Key), "名称不能为空！");
            if (Scope.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Scope), "作用域不能为空！");

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            Key = Key?.Trim();
            Value = Value?.Trim();
            Scope = Scope?.Trim();
        }

        /// <summary>添加</summary>
        /// <returns></returns>
        protected override Int32 OnInsert()
        {
            var rs = base.OnInsert();

            ConfigHistory.Add(Id, "Insert", null, this.ToJson(), 0);

            return rs;
        }

        /// <summary>更新</summary>
        /// <returns></returns>
        protected override Int32 OnUpdate()
        {
            var cfg = Find(_.Id == Id);
            if (cfg != null)
            {
                var ns = new[] { __.Key, __.AppId, __.Scope, __.Value, __.Enable };
                foreach (var item in ns)
                {
                    if (Dirtys[item])
                    {
                        ConfigHistory.Add(Id, "Update", item, cfg[item] + "", 0);
                    }
                }
            }

            return base.OnUpdate();
        }

        /// <summary>删除</summary>
        /// <returns></returns>
        protected override Int32 OnDelete()
        {
            ConfigHistory.Add(Id, "Delete", null, this.ToJson(), 0);

            return base.OnDelete();
        }

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override String ToString() => Scope.IsNullOrEmpty() ? Key : $"{Key}-{Scope}";
        #endregion

        #region 扩展属性
        /// <summary>应用系统</summary>
        [XmlIgnore, ScriptIgnore]
        public App App => Extends.Get(nameof(App), k => App.FindByID(AppId));

        /// <summary>应用名称</summary>
        [XmlIgnore, ScriptIgnore]
        [Map(__.AppId, typeof(App), "ID")]
        public String AppName => App + "";
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static ConfigData FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据key查找</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static IList<ConfigData> FindAllByKey(String key)
        {
            key = key?.Trim();
            if (key.IsNullOrEmpty()) return new List<ConfigData>();

            if (Meta.Count < 1000) return Meta.Cache.FindAll(e => e.Key.EqualIgnoreCase(key));

            return FindAll(_.Key == key);
        }

        /// <summary>
        /// 根据应用查询所属配置，
        /// </summary>
        /// <param name="appid">=0查询全局</param>
        /// <returns></returns>
        public static IList<ConfigData> FindAllByApp(Int32 appid) => FindAll(_.AppId == appid);

        /// <summary>查找应用的有效配置</summary>
        /// <param name="appid"></param>
        /// <returns></returns>
        public static IList<ConfigData> FindAllValid(Int32 appid)
        {
            if (Meta.Count < 1000) return Meta.Cache.FindAll(_ => _.AppId == appid && _.Enable);

            return FindAll(_.AppId == appid & _.Enable == true);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="appId">应用</param>
        /// <param name="name">名称</param>
        /// <param name="scope">作用域</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<ConfigData> Search(Int32 appId, String name, String scope, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (appId >= 0) exp &= _.AppId == appId;
            if (!name.IsNullOrEmpty()) exp &= _.Key == name;
            if (!scope.IsNullOrEmpty()) exp &= _.Scope == scope;
            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Value.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

            return FindAll(exp, page);
        }
        #endregion

        #region 业务操作
        /// <summary>申请配置，优先本应用，其次共享应用，如有指定作用域则优先作用域</summary>
        /// <param name="appid">应用</param>
        /// <param name="key">键</param>
        /// <param name="scope">作用域</param>
        /// <returns></returns>
        public static ConfigData Acquire(Int32 appid, String key, String scope)
        {
            var locals = FindAllValid(appid).Where(_ => _.Key.EqualIgnoreCase(key)).ToList();

            // 混合应用配置表
            var qs = AppQuote.FindAllByAppId(appid);
            var shares = qs.SelectMany(_ => FindAllValid(_.TargetAppId).Where(_ => _.Key.EqualIgnoreCase(key))).ToList();

            if (locals.Count == 0 && shares.Count == 0) return null;

            // 如果未指定作用域
            if (scope.IsNullOrEmpty())
            {
                // 优先空作用域
                var cfg = locals.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
                if (cfg != null) return cfg;

                // 共享应用空作用域
                cfg = shares.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
                if (cfg != null) return cfg;

                // 任意作用域，最新优先
                if (locals.Count > 0) return locals.OrderByDescending(_ => _.Id).FirstOrDefault();
                if (shares.Count > 0) return shares.OrderByDescending(_ => _.Id).FirstOrDefault();
            }
            else
            {
                // 优先匹配作用域
                var cfg = locals.FirstOrDefault(_ => _.Scope.EqualIgnoreCase(scope));
                if (cfg != null) return cfg;

                // 共享应用该作用域
                cfg = shares.FirstOrDefault(_ => _.Scope.EqualIgnoreCase(scope));
                if (cfg != null) return cfg;

                // 没有找到匹配作用域，使用空作用域
                cfg = locals.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
                if (cfg != null) return cfg;

                // 共享应用空作用域
                cfg = shares.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
                if (cfg != null) return cfg;
            }

            // 都没有就返回空，要求去配置
            return null;
        }
        #endregion
    }
}