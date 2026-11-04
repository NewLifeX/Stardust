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
    /// <returns>安装清单</returns>
    [HttpGet("/install")]
    public InstallManifest Manifest()
    {
        var server = InstallService.GetServerUrl(Request, _setting);
        return _installService.GetManifest(server);
    }

    /// <summary>Linux 安装脚本</summary>
    /// <param name="arch">目标架构</param>
    /// <param name="runtime">.NET 主版本号，为空不限制</param>
    /// <param name="version">StarAgent 版本，为空取最新</param>
    /// <returns>可直接执行的 bash 脚本</returns>
    [HttpGet("/install.sh")]
    public ActionResult Shell(String? arch, String? runtime, String? version)
    {
        try
        {
            var script = _installService.BuildScript("linux", arch, runtime, version, InstallService.GetServerUrl(Request, _setting));
            if (script.IsNullOrEmpty()) return NotFound();

            return Content(script, "text/x-shellscript; charset=utf-8", Encoding.UTF8);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Windows 安装脚本</summary>
    /// <param name="arch">目标架构</param>
    /// <param name="runtime">.NET 主版本号，为空不限制</param>
    /// <param name="version">StarAgent 版本，为空取最新</param>
    /// <returns>可直接执行的 PowerShell 脚本</returns>
    [HttpGet("/install.ps1")]
    public ActionResult PowerShell(String? arch, String? runtime, String? version)
    {
        try
        {
            var script = _installService.BuildScript("windows", arch, runtime, version, InstallService.GetServerUrl(Request, _setting));
            if (script.IsNullOrEmpty()) return NotFound();

            return Content(script, "text/plain; charset=utf-8", Encoding.UTF8);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>品牌图标。四叶草标识，供首页与站点图标使用</summary>
    /// <returns>PNG 图片</returns>
    [HttpGet("/install/leaf.png")]
    [HttpGet("/favicon.ico")]
    public ActionResult Leaf()
    {
        var stream = typeof(InstallController).Assembly.GetManifestResourceStream("leaf.png");
        if (stream == null) return NotFound();

        // 图标几乎不变，允许浏览器长时间缓存
        Response.Headers.CacheControl = "public,max-age=86400";
        return File(stream, "image/png");
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
