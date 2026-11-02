using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>安装引导接口。为安装页面、安装脚本与客户端提供统一入口</summary>
/// <remarks>
/// 全部匿名可访问：引导安装发生在节点注册之前，没有令牌可用。
/// 下载接口只放行后台启用版本下的安装包编号，不开放附件表任意下载。
/// </remarks>
[AllowAnonymous]
[ApiController]
public class InstallController : ControllerBase
{
    private readonly StarServerSetting _setting;
    private readonly InstallService _installService;

    /// <summary>实例化安装引导接口</summary>
    /// <param name="setting">服务端配置</param>
    /// <param name="installService">安装引导服务</param>
    public InstallController(StarServerSetting setting, InstallService installService)
    {
        _setting = setting;
        _installService = installService;
    }

    /// <summary>安装清单。页面、脚本与工具统一从这里取包</summary>
    /// <param name="channel">升级通道。release/beta/develop，为空取服务端默认通道</param>
    /// <returns>安装清单</returns>
    [HttpGet("/install")]
    public InstallManifest Manifest(String? channel)
    {
        var server = InstallService.GetServerUrl(Request, _setting);
        return _installService.GetManifest(channel, server);
    }

    /// <summary>Linux 安装脚本</summary>
    /// <param name="os">目标系统，固定 linux</param>
    /// <param name="arch">目标架构</param>
    /// <param name="runtime">.NET 主版本号，为空不限制</param>
    /// <param name="version">StarAgent 版本，为空取最新</param>
    /// <param name="channel">升级通道</param>
    /// <returns>可直接执行的 bash 脚本</returns>
    [HttpGet("/install.sh")]
    public ActionResult Shell(String? os, String? arch, String? runtime, String? version, String? channel)
    {
        try
        {
            var script = _installService.BuildScript("linux", arch, runtime, version, channel, InstallService.GetServerUrl(Request, _setting));
            if (script.IsNullOrEmpty()) return NotFound();

            return Content(script, "text/x-shellscript; charset=utf-8", Encoding.UTF8);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Windows 安装脚本</summary>
    /// <param name="os">目标系统，固定 windows</param>
    /// <param name="arch">目标架构</param>
    /// <param name="runtime">.NET 主版本号，为空不限制</param>
    /// <param name="version">StarAgent 版本，为空取最新</param>
    /// <param name="channel">升级通道</param>
    /// <returns>可直接执行的 PowerShell 脚本</returns>
    [HttpGet("/install.ps1")]
    public ActionResult PowerShell(String? os, String? arch, String? runtime, String? version, String? channel)
    {
        try
        {
            var script = _installService.BuildScript("windows", arch, runtime, version, channel, InstallService.GetServerUrl(Request, _setting));
            if (script.IsNullOrEmpty()) return NotFound();

            return Content(script, "text/plain; charset=utf-8", Encoding.UTF8);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>安装包下载。现场签发短时令牌后重定向到实际地址</summary>
    /// <param name="kind">包类型。agent/runtime</param>
    /// <param name="id">包编号</param>
    /// <returns>302 跳转到下载地址</returns>
    [HttpGet("/install/download/{kind}/{id}")]
    public ActionResult Download(String kind, Int32 id)
    {
        var url = _installService.ResolveDownloadUrl(kind, id, InstallService.GetServerUrl(Request, _setting));
        if (url.IsNullOrEmpty()) return NotFound();

        return Redirect(url);
    }
}
