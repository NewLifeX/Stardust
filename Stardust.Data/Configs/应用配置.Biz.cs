using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Serialization;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Configs;

/// <summary>应用配置。需要管理配置的应用系统列表</summary>
public partial class AppConfig : Entity<AppConfig>
{
    #region 对象操作
    static AppConfig()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(Version));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (Version <= 0) Version = 1;

        var qs = (Quotes + "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        Quotes = qs.Distinct().OrderBy(e => e).Join();
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

            Remark = "全局通用配置，其它应用启动合并该应用下的配置项",
        };
        entity.Insert();
    }
    #endregion

    #region 扩展属性
    /// <summary>本应用正在使用的配置数据，不包括未发布的新增和修改，借助扩展属性缓存</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public IList<ConfigData> Configs => Extends.Get(nameof(Configs), k => ConfigData.FindAllLastRelease(Id, Version));

    /// <summary>应用密钥。用于web端做预览</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public String AppSecret => App.FindByName(Name)?.Secret;

    /// <summary>依赖应用</summary>
    [Map(nameof(Quotes))]
    public String QuoteNames => Extends.Get(nameof(QuoteNames), k => Quotes?.SplitAsInt().Select(FindById).Join());

    /// <summary>应用</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(__.AppId, typeof(App), "Id")]
    public String AppName => App?.Name;
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

    /// <summary>根据编号查找</summary>
    /// <param name="appId">编号</param>
    /// <returns>实体对象</returns>
    public static AppConfig FindByAppId(Int32 appId)
    {
        if (appId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.AppId == appId);

        return Find(_.AppId == appId);
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

    /// <summary>根据应用查找</summary>
    /// <param name="appId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<AppConfig> FindAllByAppId(Int32 appId)
    {
        if (appId <= 0) return new List<AppConfig>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<AppConfig> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return new List<AppConfig>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
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
    public static IList<AppConfig> Search(Int32 projectId, Int32 appId, String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (appId >= 0) exp &= _.AppId == appId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }

    // Select Count(Id) as Id,Category From AppDeploy Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    static readonly FieldCache<AppConfig> _CategoryCache = new(nameof(Category));

    /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>获取可用节点</summary>
    /// <returns></returns>
    public static IList<AppConfig> GetValids() => FindAllWithCache().Where(e => e.Enable).ToList();

    /// <summary>获取所有引用的应用（父级）</summary>
    /// <returns></returns>
    public IList<AppConfig> GetQuotes()
    {
        // 遍历当前应用的Quotes引用，找到它们对应的应用
        var vs = GetValids();
        var list = new List<AppConfig>();
        foreach (var item in (Quotes + "").Split(',').Distinct())
        {
            // 同时找编号和名称
            var id = item.ToInt();
            var app = vs.FirstOrDefault(e => e.Id == id || e.Name.EqualIgnoreCase(item));
            if (app != null)
            {
                list.Add(app);

                // 递归找到所有引用的应用
                var qs = app.GetQuotes();
                list.AddRange(qs);
            }
        }

        return list;
    }

    /// <summary>获取所有依赖当前应用的应用（子级）</summary>
    /// <returns></returns>
    public IList<AppConfig> GetChilds()
    {
        // 遍历所有应用，找到其Quotes引用中包含当前应用的
        var vs = GetValids();
        var list = new List<AppConfig>();
        foreach (var app in vs)
        {
            var qs = (app.Quotes + "").Split(',').Distinct().ToArray();
            if (Name.EqualIgnoreCase(qs))
            {
                list.Add(app);

                // 递归找到所有依赖当前应用的应用
                var cs = app.GetChilds();
                list.AddRange(cs);
            }
        }

        return list;
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

        var rs = 0;
        using var tran = Meta.CreateTrans();
        try
        {
            Version = NextVersion;
            PublishTime = DateTime.MinValue;

            var list = ConfigData.FindAllByApp(Id);
            rs += ConfigData.Publish(list, Version);

            rs += Update();

            tran.Commit();
        }
        catch (Exception ex)
        {
            ConfigHistory.Add(Id, "Publish", false, ex.GetMessage());

            throw;
        }

        return rs;
    }

    /// <summary>
    /// 清理旧数据。把原来多行的配置数据，修改为单行
    /// </summary>
    /// <returns></returns>
    public Int32 TrimOld()
    {
        var rs = 0;
        var list = ConfigData.FindAllByApp(Id);
        var dic = list.GroupBy(e => $"{e.Key}#{e.Scope}").ToDictionary(e => e.Key, e => e.ToList());
        foreach (var item in dic)
        {
            // 多行，保留最大一行
            if (item.Value.Count > 1)
            {
                var ds = item.Value.OrderByDescending(e => e.Version).ToList();

                // 如果最新行未发布，则特殊处理
                var cd = ds[0];
                if (cd.Version > Version)
                {
                    cd.NewValue = cd.Value;
                    cd.Value = ds[1].Value;
                    cd.Update();
                }
                for (var i = 1; i < ds.Count; i++)
                {
                    rs += ds[i].Delete();
                }
            }
        }

        return rs;
    }

    /// <summary>
    /// 清理旧数据。把原来多行的配置数据，修改为单行
    /// </summary>
    /// <returns></returns>
    public static Int32 TrimAll()
    {
        var rs = 0;
        var list = FindAll();
        foreach (var item in list)
        {
            rs += item.TrimOld();
        }

        return rs;
    }

    ///// <summary>更新信息</summary>
    ///// <param name="app"></param>
    ///// <param name="ip"></param>
    ///// <param name="key"></param>
    //public ConfigOnline UpdateInfo(App app, String ip, String key)
    //{
    //    // 修复数据
    //    if (app != null)
    //    {
    //        if (!app.DisplayName.IsNullOrEmpty()) DisplayName = app.DisplayName;
    //        if (!app.Category.IsNullOrEmpty()) Category = app.Category;

    //        //// 更新应用信息
    //        //var clientId = $"{ip}#{key}";
    //        //var online = ConfigOnline.GetOrAddClient(clientId);
    //        //online.Category = Category;

    //        //// 找到第一个应用在线，拷贝它的信息
    //        //var list = online.ProcessId > 0 ? null : AppOnline.FindAllByApp(app.Id);

    //        //if (online.CreateIP.IsNullOrEmpty()) online.CreateIP = ip;
    //        //online.UpdateTime = DateTime.Now;
    //        //online.UpdateInfo(this, list?.FirstOrDefault(e => e.Client.StartsWith($"{ip}@")));

    //        //return online;
    //    }

    //    return null;
    //}

    /// <summary>复制应用数据</summary>
    /// <param name="app"></param>
    public void Copy(App app)
    {
        AppId = app.Id;
        Name = app.Name;
        Category = app.Category;

        if (!app.Enable) Enable = false;
    }

    /// <summary>
    /// 从应用表同步数据到发布表
    /// </summary>
    /// <returns></returns>
    public static Int32 Sync()
    {
        var count = 0;
        var apps = App.FindAll();
        var list = FindAll();
        foreach (var app in apps)
        {
            var ad = list.FirstOrDefault(e => e.AppId == app.Id);
            ad ??= list.FirstOrDefault(e => e.Name.EqualIgnoreCase(app.Name));
            if (ad != null)
                list.Remove(ad);
            else
                ad = new AppConfig { Name = app.Name, Enable = true };

            ad.Copy(app);

            count += ad.Save();
        }

        return count;
    }
    #endregion
}