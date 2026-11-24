using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Data;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>应用包服务。应用包即发布中心的部署集，版本即部署版本；为 star CLI 提供查询、平台匹配、下载令牌与计数</summary>
public class PackageService
{
    private readonly DownloadTokenService _downloadTokenService;

    /// <summary>实例化</summary>
    /// <param name="downloadTokenService">附件下载短时令牌服务</param>
    public PackageService(DownloadTokenService downloadTokenService) => _downloadTokenService = downloadTokenService;

    /// <summary>搜索启用的应用包</summary>
    /// <param name="key">关键字</param>
    /// <param name="pageIndex">页码，从1开始</param>
    /// <param name="pageSize">页大小</param>
    /// <returns>包列表</returns>
    public virtual PackageInfo[] Search(String? key, Int32 pageIndex, Int32 pageSize)
    {
        if (pageIndex <= 0) pageIndex = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var page = new PageParameter { PageIndex = pageIndex, PageSize = pageSize };
        var list = AppDeploy.SearchPackages(key, page);

        return list.Select(ToPackageInfo).ToArray();
    }

    /// <summary>获取应用包信息</summary>
    /// <param name="name">完整包名（项目/应用）或唯一短名</param>
    /// <returns>包信息，未找到或未启用返回 null</returns>
    public virtual PackageInfo? Get(String? name)
    {
        var package = FindPackage(name);
        if (package == null || !package.Enable) return null;

        return ToPackageInfo(package);
    }

    /// <summary>获取应用包的启用版本列表</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <returns>版本列表</returns>
    public virtual PackageVersionInfo[] GetVersions(String? name)
    {
        var package = FindPackage(name);
        if (package == null || !package.Enable) return [];

        var versions = AppDeployVersion.FindAllByDeployId(package.Id, 1000).Where(e => e.Enable);
        return SortVersions(versions).Select(e => ToVersionInfo(package, e)).ToArray();
    }

    /// <summary>获取目标平台的当前版本。默认只读取部署集已应用的 Version，新上传未应用的版本不会被拉取</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <param name="os">目标操作系统。0表示未知，使用节点平台</param>
    /// <param name="arch">目标架构。0表示未知，使用节点平台</param>
    /// <param name="node">节点。用于补齐平台信息</param>
    /// <param name="version">指定版本。用于显式回滚；为空时取部署集当前版本</param>
    /// <returns>版本信息，未找到返回 null</returns>
    public virtual PackageVersionInfo? GetLatest(String? name, Int32 os, Int32 arch, Node? node, String? version = null)
    {
        var package = FindPackage(name);
        if (package == null || !package.Enable) return null;

        var targetVersion = version.IsNullOrEmpty() ? package.Version : version;
        if (targetVersion.IsNullOrEmpty()) return null;

        var osKind = (OSKind)os;
        var archKind = (CpuArch)arch;
        if (osKind == OSKind.Any && archKind == CpuArch.Any && node != null)
        {
            (osKind, archKind) = PlatformHelper.GetNodePlatform(node);
        }

        var frameworks = node?.Frameworks?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var ver = AppDeployVersion.FindAllByDeployIdAndVersion(package.Id, targetVersion)
            .Where(e => e.Enable)
            .Where(e => PlatformHelper.MatchPlatform(e.OS, e.Arch, osKind, archKind))
            .Where(e => AppDeployVersion.MatchFramework(e.TargetFramework, frameworks))
            .OrderByDescending(e => e.Id)
            .FirstOrDefault();
        if (ver == null) return null;

        return ToVersionInfo(package, ver);
    }

    /// <summary>获取指定版本的下载地址</summary>
    /// <param name="id">版本编号</param>
    /// <returns>版本信息，未找到返回 null</returns>
    public virtual PackageVersionInfo? Download(Int32 id)
    {
        var version = AppDeployVersion.FindById(id);
        if (version == null || !version.Enable) return null;

        var package = version.Deploy;
        if (package == null || !package.Enable) return null;

        return ToVersionInfo(package, version);
    }

    /// <summary>查找应用包。支持 项目/名称 或唯一短名</summary>
    /// <param name="name">包名</param>
    /// <returns>部署集，未找到返回 null</returns>
    protected virtual AppDeploy? FindPackage(String? name)
    {
        if (name.IsNullOrEmpty()) return null;

        var text = name.Trim();
        var index = text.LastIndexOf('/');
        if (index > 0)
        {
            var publisher = text[..index];
            var shortName = text[(index + 1)..];
            var package = AppDeploy.FindByName(shortName);
            if (package == null || !package.ProjectName.EqualIgnoreCase(publisher)) return null;
            return package;
        }

        return AppDeploy.FindByName(text);
    }

    /// <summary>按版本号排序；版本号不可解析时按创建时间</summary>
    /// <param name="versions">版本列表</param>
    /// <returns>排序后的列表</returns>
    protected static List<AppDeployVersion> SortVersions(IEnumerable<AppDeployVersion> versions)
    {
        var list = versions.ToList();
        if (list.Count == 0) return list;

        if (list.All(e => System.Version.TryParse(e.Version, out _)))
            return list.OrderByDescending(e => System.Version.Parse(e.Version)).ThenByDescending(e => e.Id).ToList();

        return list.OrderByDescending(e => e.CreateTime).ThenByDescending(e => e.Id).ToList();
    }

    /// <summary>转换为包信息</summary>
    /// <param name="package">部署集</param>
    /// <returns>包信息</returns>
    protected virtual PackageInfo ToPackageInfo(AppDeploy package) => new()
    {
        Name = package.FullName,
        Publisher = package.ProjectName,
        ShortName = package.Name,
        DisplayName = package.App?.DisplayName ?? package.Name,
        Category = package.Category,
        Description = package.Remark,
        Enable = package.Enable,
        Version = package.Version,
        UpdateTime = package.UpdateTime.Year > 2000 ? package.UpdateTime : package.CreateTime,
    };

    /// <summary>转换为版本信息。Manifest 由部署集字段合成，作为 star CLI 的唯一运行配置</summary>
    /// <param name="package">部署集</param>
    /// <param name="version">部署版本</param>
    /// <returns>版本信息</returns>
    protected virtual PackageVersionInfo ToVersionInfo(AppDeploy package, AppDeployVersion version) => new()
    {
        Id = version.Id,
        Name = package.FullName,
        Version = version.Version,
        Enable = version.Enable,
        OS = version.OS,
        Arch = version.Arch,
        TargetFramework = version.TargetFramework,
        Size = version.Size,
        Hash = version.Hash,
        Url = version.Url.IsNullOrEmpty() ? null : _downloadTokenService.AppendToken(version.Url, StarServerSetting.Current.AttachmentTokenExpire),
        Manifest = BuildManifest(package, version),
        AppCount = 1,
        PublishTime = version.CreateTime.Year > 2000 ? version.CreateTime : version.UpdateTime,
    };

    /// <summary>把部署集与版本合成为单应用 star 清单</summary>
    /// <param name="package">部署集</param>
    /// <param name="version">部署版本</param>
    /// <returns>清单 JSON</returns>
    protected virtual String BuildManifest(AppDeploy package, AppDeployVersion version)
    {
        var app = new StarPackageApp
        {
            Name = package.Name,
            DisplayName = package.App?.DisplayName,
            FileName = package.FileName,
            Arguments = package.Arguments,
            WorkingDirectory = package.WorkingDirectory,
            UserName = package.UserName,
            Environments = package.Environments,
            HealthCheck = package.HealthCheck,
            MaxMemory = package.MaxMemory,
            Priority = package.Priority,
            Mode = package.Mode == DeployMode.Default ? DeployMode.Standard : package.Mode,
            ReloadOnChange = package.ReloadOnChange,
            AllowMultiple = package.AllowMultiple,
            AutoStop = package.AutoStop,
            Ports = package.Port > 0 ? [package.Port] : null,
            Main = true,
        };

        var manifest = new StarPackage
        {
            Schema = 1,
            Name = package.FullName,
            Version = version.Version,
            DisplayName = package.App?.DisplayName ?? package.Name,
            Description = package.Remark,
            Apps = [app],
        };

        return manifest.ToJson();
    }
}
