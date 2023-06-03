using System;
using System.Collections.Generic;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Data
{
    /// <summary>服务信息。服务提供者发布的服务</summary>
    public partial class Service : Entity<Service>
    {
        #region 对象操作
        static Service()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(Providers));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();

            // 单对象缓存
            var sc = Meta.SingleCache;
            sc.FindSlaveKeyMethod = k => Find(_.Name == k);
            sc.GetSlaveKeyMethod = e => e.Name;
        }

        /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);
        }

        protected override Service CreateInstance(Boolean forEdit = false)
        {
            // 默认单例
            var entity = base.CreateInstance(forEdit);
            if (forEdit) entity.Singleton = true;

            return entity;
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static Service FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据名称查找</summary>
        /// <param name="name">名称</param>
        /// <returns>实体对象</returns>
        public static Service FindByName(String name)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

            // 单对象缓存
            //return Meta.SingleCache.GetItemWithSlaveKey(name) as ServiceInfo;

            return Find(_.Name == name);
        }

        /// <summary>根据项目查找</summary>
        /// <param name="projectId">项目</param>
        /// <returns>实体列表</returns>
        public static IList<Service> FindAllByProjectId(Int32 projectId)
        {
            if (projectId <= 0) return new List<Service>();

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

            return FindAll(_.ProjectId == projectId);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="name">名称。服务名，提供一个地址，包含多个接口</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<Service> Search(String name, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (!name.IsNullOrEmpty()) exp &= _.Name == name;
            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.DisplayName.Contains(key) | _.Category.Contains(key) | _.Remark.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.UpdateUser.Contains(key) | _.UpdateIP.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,Category From ServiceInfo Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        //static readonly FieldCache<ServiceInfo> _CategoryCache = new FieldCache<ServiceInfo>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        #endregion
    }
}