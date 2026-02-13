using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;

namespace Stardust.Server.Controllers;

/// <summary>首页控制器。根路径重定向到代理安装页</summary>
[AllowAnonymous]
[ApiController]
public class HomeController : ControllerBase
{
    private readonly StarServerSetting _setting;

    /// <summary>实例化</summary>
    /// <param name="setting"></param>
    public HomeController(StarServerSetting setting) => _setting = setting;

    /// <summary>首页跳转</summary>
    /// <returns></returns>
    [Route("/")]
    [HttpGet]
    public IActionResult Index()
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
