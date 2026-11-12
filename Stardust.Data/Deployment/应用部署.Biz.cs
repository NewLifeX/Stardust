using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Configs;
using Stardust.Data.Nodes;
using Stardust.Models;
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

    /// <summary>获取本部署集在目标节点上应使用的部署版本。支持多版本匹配（按操作系统/架构/框架运行时筛选）。
    /// 共享实现（自 Stardust.Server.Services.DeployService 下沉，2026-11-12）：服务端选包（Agent 拉取）与控制台 install 下发回填节点版本，必须同一口径。</summary>
    /// <param name="node">目标节点</param>
    /// <returns>匹配的部署版本，未找到时返回 null</returns>
    public AppDeployVersion GetDeployVersion(Node node)
    {
        if (MultiVersion)
        {
            // 查找最新的一批版本，挑选符合目标节点的最新版本
            var vers = AppDeployVersion.FindAllByDeployId(Id, 100);
            vers = vers.Where(e => e.Enable).OrderByDescending(e => e.Id).ToList();

            // 目标节点的操作系统和架构
            var (nodeOS, nodeArch) = GetNodePlatform(node);

            // 可能有多个版本，挑选最新的适合目标节点操作系统、指令集和框架运行时的版本
            var fms = node.Frameworks?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            foreach (var ver in vers)
            {
                // 检查操作系统和架构是否匹配
                if (!MatchPlatform(ver.OS, ver.Arch, nodeOS, nodeArch)) continue;

                if (!ver.TargetFramework.IsNullOrEmpty() && fms.Length > 0)
                {
                    var tfm = ver.TargetFramework.TrimPrefix("netcoreapp").TrimPrefix("net").TrimPrefix("v");

                    // 特殊处理4.x，例如net4.6.1可以运行在net4.7/net4.8上
                    if (tfm.StartsWith("4."))
                    {
                        var v = new Version(tfm);
                        if (!fms.Any(e => e.StartsWith("4.") && new Version(e) >= v))
                            continue;
                    }
                    else if (!fms.Any(e => e.StartsWith(tfm)))
                        continue;
                }

                return ver;
            }

            return null;
        }

        return AppDeployVersion.FindByDeployIdAndVersion(Id, Version);
    }

    /// <summary>获取节点的操作系统和架构</summary>
    private static (OSKind, CpuArch) GetNodePlatform(Node node)
    {
        var os = node.OSKind switch
        {
            >= OSKinds.iOS => OSKind.iOS,
            >= OSKinds.Android => OSKind.Android,
            >= OSKinds.MacOSX => OSKind.OSX,
            // Linux 各发行版（100+）属于 glibc，必须排在 Alpine（90）之前判断，
            // 否则 Ubuntu/CentOS 等会被误判为 musl，导致多版本部署选不到 Linux 包
            >= OSKinds.Linux or OSKinds.SmartOS => OSKind.Linux,
            >= OSKinds.Alpine => OSKind.LinuxMusl,
            >= OSKinds.Win10 => OSKind.Windows,
            _ => OSKind.Any,
        };

        var arch = node.Architecture?.ToLower() switch
        {
            "x86" => CpuArch.X86,
            "x64" => CpuArch.X64,
            "arm" => CpuArch.Arm,
            "arm64" => CpuArch.Arm64,
            "loongarch64" => CpuArch.LA64,
            "riscv64" => CpuArch.RiscV64,
            "mips64" => CpuArch.Mips64,
            _ => CpuArch.Any,
        };

        return (os, arch);
    }

    /// <summary>检查版本的平台是否匹配目标节点</summary>
    private static Boolean MatchPlatform(OSKind verOS, CpuArch verArch, OSKind nodeOS, CpuArch nodeArch)
    {
        // 版本未指定平台，匹配所有
        if (verOS == OSKind.Any && verArch == CpuArch.Any) return true;

        // 操作系统匹配：版本未指定或与节点相同
        if (verOS != OSKind.Any && verOS != nodeOS) return false;

        // 架构匹配：版本未指定或与节点相同
        if (verArch != CpuArch.Any && verArch != nodeArch) return false;

        return true;
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


    #endregion
}