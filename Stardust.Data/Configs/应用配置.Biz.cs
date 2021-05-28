using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Serialization;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Configs
{
    /// <summary>应用配置。需要管理配置的应用系统列表</summary>
    public partial class AppConfig : Entity<AppConfig>
    {
        #region 对象操作
        static AppConfig()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(Version));

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

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);
        }

        /// <summary>初始化数据</summary>
        protected override void InitData()
        {
            if (Meta.Count > 0) return;

            var entity = new AppConfig
            {
                Name = "Common",

                Enable = true,
                CanBeQuoted = true,
                IsGlobal = true,

                Remark = "全局通用配置",
            };
            entity.Insert();
        }
        #endregion

        #region 扩展属性
        /// <summary>本应用最后发布的配置数据，借助扩展属性缓存</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public IList<ConfigData> LastRelease => Extends.Get(nameof(LastRelease), k => ConfigData.FindAllLastRelease(Id, Version));

        /// <summary>应用密钥。用于web端做预览</summary>
        [XmlIgnore, ScriptIgnore, IgnoreDataMember]
        public String AppSecret => App.FindByName(Name)?.Secret;

        /// <summary>依赖应用</summary>
        [Map(nameof(Quotes))]
        public String QuoteNames => Extends.Get(nameof(QuoteNames), k => Quotes?.SplitAsInt().Select(FindById).Join());
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppConfig FindById(Int32 id)
        {
            if (id < 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.Id == id);
        }

        /// <summary>根据名称查找</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static AppConfig FindByName(String name)
        {
            if (name.IsNullOrEmpty()) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

            return Find(_.Name == name);
        }
        #endregion

        #region 高级查询

        // Select Count(Id) as Id,Category From AppConfig Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
        //static readonly FieldCache<AppConfig> _CategoryCache = new FieldCache<AppConfig>(nameof(Category))
        //{
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        //};

        ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        ///// <returns></returns>
        //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>获取可用节点</summary>
        /// <returns></returns>
        public static IList<AppConfig> GetValids() => FindAllWithCache().Where(e => e.Enable).ToList();

        /// <summary>获取所有引用的应用</summary>
        /// <returns></returns>
        public IList<AppConfig> GetQuotes()
        {
            var ids = Quotes.SplitAsInt();
            return GetValids().Where(e => ids.Contains(e.Id)).ToList();
        }

        /// <summary>申请新版本，如果已有未发布版本，则直接返回</summary>
        /// <returns></returns>
        public Int32 AcquireNewVersion()
        {
            if (NextVersion <= Version) NextVersion = Version + 1;
            if (NextVersion == 0) NextVersion = 1;

            Update();

            return NextVersion;
        }

        /// <summary>申请可用版本，内置定时发布处理</summary>
        /// <returns></returns>
        public Int32 GetValidVersion()
        {
            if (NextVersion != Version && PublishTime.Year > 2000 & PublishTime < DateTime.Now)
            {
                Publish();
            }

            return Version;
        }

        /// <summary>发布</summary>
        /// <returns></returns>
        public Int32 Publish()
        {
            ConfigHistory.Add(Id, "Publish", true, this.ToJson());

            Version = NextVersion;
            PublishTime = DateTime.MinValue;

            return Update();
        }

        /// <summary>更新信息</summary>
        /// <param name="app"></param>
        /// <param name="ip"></param>
        public void UpdateInfo(App app, String ip)
        {
            // 修复数据
            if (app != null)
            {
                if (!app.DisplayName.IsNullOrEmpty()) DisplayName = app.DisplayName;
                if (!app.Category.IsNullOrEmpty()) Category = app.Category;

                // 更新应用信息
                var clientId = $"{ip}#{Id}";
                var online = ConfigOnline.GetOrAddClient(clientId);
                online.Category = Category;

                // 找到第一个应用在线，拷贝它的信息
                var list = online.ProcessId > 0 ? null : AppOnline.FindAllByApp(app.Id);

                online.UpdateInfo(app, list?.FirstOrDefault(e => e.Client.StartsWith($"{ip}@")));
            }
        }
        #endregion
    }
}