using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>首页控制器。根路径即安装引导页，服务端地址与安装包在这里一次性给出</summary>
/// <remarks>关闭安装引导页（EnableInstallPage=false）时保持旧行为：跳转到 StarWeb 的代理安装页</remarks>
[AllowAnonymous]
[ApiController]
public class HomeController : ControllerBase
{
    private readonly StarServerSetting _setting;
    private readonly InstallService _installService;

    /// <summary>实例化</summary>
    /// <param name="setting">服务端配置</param>
    /// <param name="installService">安装引导服务</param>
    public HomeController(StarServerSetting setting, InstallService installService)
    {
        _setting = setting;
        _installService = installService;
    }

    /// <summary>首页。默认返回安装引导页，Accept 或 format 指定 json 时返回安装清单</summary>
    /// <param name="format">返回格式。json 返回安装清单</param>
    /// <param name="channel">升级通道。release/beta/develop</param>
    /// <returns>安装引导页或安装清单</returns>
    [Route("/")]
    [HttpGet]
    public IActionResult Index(String? format, String? channel)
    {
        if (!_setting.EnableInstallPage) return LegacyHome();

        // 脚本与工具按清单消费；页面本身走 HTML
        var accept = Request.Headers.Accept.ToString();
        if (format.EqualIgnoreCase("json") || accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var server = InstallService.GetServerUrl(Request, _setting);
            return Ok(_installService.GetManifest(channel, server));
        }

        var html = _installService.GetPage();
        if (html.IsNullOrEmpty()) return LegacyHome();

        return Content(html, "text/html; charset=utf-8", Encoding.UTF8);
    }

    /// <summary>旧版首页行为：跳转到 StarWeb 的代理安装页</summary>
    /// <returns>重定向或基本信息</returns>
    private IActionResult LegacyHome()
    {
        // 如果配置了StarWeb地址，跳转到StarWeb的/agent页面
        var webUrl = _setting.WebUrl;
        if (!webUrl.IsNullOrEmpty())
        {
            var url = webUrl.Split(';', ',').FirstOrDefault();
            if (!url.IsNullOrEmpty())
                return Redirect(url.TrimEnd('/') + "/agent");
        }

        // 否则返回基本信息
        return Ok(new
        {
            message = "StarServer is running. Please configure StarWeb to access the agent download page.",
            agent = "/agent"
        });
    }
}

