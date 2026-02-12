using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using Stardust.Server;

namespace Stardust.Web.Controllers;

/// <summary>星尘代理下载安装页面</summary>
[DisplayName("代理安装")]
[AllowAnonymous]
public class AgentController : ControllerBaseX
{
    private readonly StarServerSetting _setting;

    /// <summary>实例化</summary>
    /// <param name="setting"></param>
    public AgentController(StarServerSetting setting) => _setting = setting;

    /// <summary>代理安装页面</summary>
    /// <returns></returns>
    [Route("[controller]")]
    public ActionResult Index()
    {
        // 判断客户端是否要求JSON格式
        var accept = Request.Headers["Accept"].FirstOrDefault();
        if (accept != null && accept.Contains("application/json"))
            return Json(GetAgentData());

        // 支持format=json查询参数
        var format = Request.Query["format"].FirstOrDefault();
        if (format.EqualIgnoreCase("json"))
            return Json(GetAgentData());

        ViewBag.Title = "星尘代理安装";

        PageSetting.EnableNavbar = false;

        return View();
    }

    private Object GetAgentData()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        return new
        {
            server = baseUrl,
            agent = new
            {
                zip = new[]
                {
                    new { name = "staragent10.zip", framework = "net10.0", os = "Windows/Linux", url = "https://x.newlifex.com/star/staragent10.zip" },
                    new { name = "staragent90.zip", framework = "net9.0", os = "Windows/Linux", url = "https://x.newlifex.com/star/staragent90.zip" },
                    new { name = "staragent80.zip", framework = "net8.0", os = "Windows/Linux", url = "https://x.newlifex.com/star/staragent80.zip" },
                    new { name = "staragent60.zip", framework = "net6.0", os = "Windows/Linux", url = "https://x.newlifex.com/star/staragent60.zip" },
                    new { name = "staragent45.zip", framework = "net4.5", os = "Windows", url = "https://x.newlifex.com/star/staragent45.zip" },
                    new { name = "staragent31.zip", framework = "netcoreapp3.1", os = "Mips64(旧龙芯)", url = "https://x.newlifex.com/star/staragent31.zip" },
                },
                targz = new[]
                {
                    new { name = "staragent10.tar.gz", framework = "net10.0", os = "Linux", url = "https://x.newlifex.com/star/staragent10.tar.gz" },
                    new { name = "staragent90.tar.gz", framework = "net9.0", os = "Linux", url = "https://x.newlifex.com/star/staragent90.tar.gz" },
                    new { name = "staragent80.tar.gz", framework = "net8.0", os = "Linux/LA64(新龙芯)", url = "https://x.newlifex.com/star/staragent80.tar.gz" },
                    new { name = "staragent60.tar.gz", framework = "net6.0", os = "Linux", url = "https://x.newlifex.com/star/staragent60.tar.gz" },
                    new { name = "staragent31.tar.gz", framework = "netcoreapp3.1", os = "Linux/Mips64(旧龙芯)", url = "https://x.newlifex.com/star/staragent31.tar.gz" },
                },
                scripts = new[]
                {
                    new { name = "star.sh (net9.0)", description = "自动安装net9.0版", url = "https://x.newlifex.com/star/star.sh" },
                    new { name = "star8.sh (net8.0)", description = "自动安装net8.0版", url = "https://x.newlifex.com/star/star8.sh" },
                },
            },
            packages = new[]
            {
                new { name = "star.zip", description = "星尘整体安装包", url = "https://x.newlifex.com/star/star.zip" },
                new { name = "StarServer.zip", description = "星尘服务端", url = "https://x.newlifex.com/star/StarServer.zip" },
                new { name = "StarWeb.zip", description = "星尘管理平台", url = "https://x.newlifex.com/star/StarWeb.zip" },
            },
            dotnet = new[]
            {
                new { name = "net10.sh", description = ".NET 10.0 运行时安装脚本", url = "https://x.newlifex.com/dotnet/net10.sh" },
                new { name = "net9.sh", description = ".NET 9.0 运行时安装脚本", url = "https://x.newlifex.com/dotnet/net9.sh" },
                new { name = "net8.sh", description = ".NET 8.0 运行时安装脚本", url = "https://x.newlifex.com/dotnet/net8.sh" },
                new { name = "net6.sh", description = ".NET 6.0 运行时安装脚本", url = "https://x.newlifex.com/dotnet/net6.sh" },
            },
        };
    }
}
