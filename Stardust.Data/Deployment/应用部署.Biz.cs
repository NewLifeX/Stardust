using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Deployment
{
    /// <summary>应用部署。应用部署的实例，每个应用在不同环境下有不同的部署集，关联不同的节点服务器组</summary>
    public partial class AppDeploy : Entity<AppDeploy>
    {
        #region 对象操作
        static AppDeploy()
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

            var app = App.FindByName(Name);
            if (app != null)
            {
                if (!app.Category.IsNullOrEmpty()) Category = app.Category;
            }

            //if (!isNew) Nodes = AppDeployNode.FindAllByAppId(Id).Count;
        }
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppDeploy FindById(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据名称查找</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static AppDeploy FindByName(String name)
        {
            if (name.IsNullOrEmpty()) return null;

            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

            return Find(_.Name == name);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="category">分类</param>
        /// <param name="enable">启用</param>
        /// <param name="start">更新时间开始</param>
        /// <param name="end">更新时间结束</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppDeploy> Search(String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (!category.IsNullOrEmpty()) exp &= _.Category == category;
            if (enable != null) exp &= _.Enable == enable;
            exp &= _.UpdateTime.Between(start, end);
            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.FileName.Contains(key) | _.Arguments.Contains(key) | _.WorkingDirectory.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(Id) as Id,Category From AppDeploy Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        static readonly FieldCache<AppDeploy> _CategoryCache = new(nameof(Category));

        /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>修正数据</summary>
        /// <returns></returns>
        public Int32 Fix()
        {
            var rs = 0;

            Refresh();

            rs += Update();

            return rs;
        }

        /// <summary>刷新</summary>
        public void Refresh()
        {
            var list = AppDeployNode.FindAllByAppId(Id);
            Nodes = list.Count(e => e.Enable);

            if (Version.IsNullOrEmpty())
            {
                var list2 = AppDeployVersion.FindAllByAppId(Id, 100);
                list2 = list2.Where(e => e.Enable).ToList();
                if (list2.Count == 0) list2 = AppDeployVersion.Search(Id, null, true, DateTime.MinValue, DateTime.MinValue, null, null);
                if (list2.Count > 0) Version = list2[0].Version;
            }
        }
        #endregion
    }
}