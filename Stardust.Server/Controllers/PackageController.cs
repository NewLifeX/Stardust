using System;
using Microsoft.AspNetCore.Mvc;
using NewLife.Remoting.Extensions;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>星尘包服务。节点通过本接口查询、下载企业内部应用包</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class PackageController(NodeService nodeService, PackageService packageService, IServiceProvider serviceProvider) : BaseController(nodeService, null, serviceProvider)
{
    /// <summary>搜索应用包</summary>
    /// <param name="key">关键字</param>
    /// <param name="pageIndex">页码，从1开始</param>
    /// <param name="pageSize">页大小</param>
    /// <returns>包列表</returns>
    public PackageInfo[] Search(String? key, Int32 pageIndex = 1, Int32 pageSize = 20) => packageService.Search(key, pageIndex, pageSize);

    /// <summary>获取应用包信息</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <returns>包信息</returns>
    public PackageInfo? Get(String name) => packageService.Get(name);

    /// <summary>获取应用包的启用版本列表</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <returns>版本列表</returns>
    public PackageVersionInfo[] GetVersions(String name) => packageService.GetVersions(name);

    /// <summary>获取目标平台的当前版本</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <param name="os">目标操作系统。0表示使用节点平台</param>
    /// <param name="arch">目标架构。0表示使用节点平台</param>
    /// <param name="version">指定版本。为空时取部署集当前版本</param>
    /// <returns>版本信息</returns>
    public PackageVersionInfo? GetLatest(String name, Int32 os = 0, Int32 arch = 0, String? version = null) => packageService.GetLatest(name, os, arch, Context.Device as Node, version);

    /// <summary>获取指定版本的下载地址</summary>
    /// <param name="id">版本编号</param>
    /// <returns>版本信息</returns>
    public PackageVersionInfo? Download(Int32 id) => packageService.Download(id);
}
