using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Configs;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Deployment;

/// <summary>应用部署。应用部署的实例，每个应用在不同环境下有不同的部署集，关联不同的节点服务器组</summary>
public partial class AppDeploy : Entity<AppDeploy>
{
    #region 对象操作
    static AppDeploy()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(AppId));

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

        var app = App.FindByName(Name);
        if (app != null)
        {
            if (AppId == 0 && !Dirtys[nameof(AppId)]) AppId = app.Id;
            if (!app.Category.IsNullOrEmpty()) Category = app.Category;
        }

        //if (!isNew) Nodes = AppDeployNode.FindAllByAppId(Id).Count;
        //if (isNew && !Dirtys[nameof(AutoStart)]) AutoStart = true;

        base.Valid(isNew);
    }
    #endregion

    #region 扩展属性
    /// <summary>应用</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public App App => Extends.Get(nameof(App), k => App.FindById(AppId));

    /// <summary>应用</summary>
    [Map(__.AppId, typeof(App), "Id")]
    public String AppName => App?.Name;

    /// <summary>节点集合</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public IList<AppDeployNode> DeployNodes => Extends.Get(nameof(DeployNodes), k => AppDeployNode.FindAllByAppId(Id));
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

    ///// <summary>根据编号查找</summary>
    ///// <param name="appId">编号</param>
    ///// <returns>实体对象</returns>
    //public static AppDeploy FindByAppId(Int32 appId)
    //{
    //    if (appId < 0) return null;

    //    // 实体缓存
    //    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.AppId == appId);

    //    return Find(_.AppId == appId);
    //}

    /// <summary>根据名称查找</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static AppDeploy FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        return Find(_.Name == name);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static IList<AppDeploy> FindAllByAppId(Int32 appId)
    {
        if (appId <= 0) return new List<AppDeploy>();

        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.AppId == appId);

        return FindAll(_.AppId == appId);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeploy> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return new List<AppDeploy>();

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
    public static IList<AppDeploy> Search(Int32 projectId, Int32 appId, String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (appId >= 0) exp &= _.AppId == appId;
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
        var nodes = AppDeployNode.FindAllByAppId(Id);
        Nodes = nodes.Count(e => e.Enable);

        if (Version.IsNullOrEmpty())
        {
            var vers = AppDeployVersion.FindAllByDeployId(Id, 100);
            vers = vers.Where(e => e.Enable).ToList();
            if (vers.Count == 0) vers = AppDeployVersion.Search(Id, null, true, DateTime.MinValue, DateTime.MinValue, null, null);
            if (vers.Count > 0) Version = vers[0].Version;
        }
    }

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
                ad = new AppDeploy { Name = app.Name, Enable = true };

            ad.Copy(app);

            count += ad.Save();
        }

        return count;
    }

    public static AppDeploy GetOrAdd(String name) => GetOrAdd(name, k => Find(_.Name == k), k => new AppDeploy { Name = k });

    /// <summary>获取当前可用资源</summary>
    /// <returns></returns>
    public IList<AppResource> GetResources()
    {
        var rs = new List<AppResource>();
        var ms = AppDeployResource.FindAllByDeployId(Id);
        foreach (var item in ms)
        {
            if (item.Enable && !rs.Any(e => e.Id == item.ResourceId) && item.Resource != null)
                rs.Add(item.Resource);
        }
        var list = AppResource.FindAllWithCache().Where(e => e.Enable && (/*e.ProjectId == 0 ||*/ e.ProjectId == ProjectId)).ToList();
        foreach (var item in list)
        {
            if (item.Enable && !rs.Any(e => e.Id == item.Id))
                rs.Add(item);
        }

        return rs;
    }
    #endregion
}