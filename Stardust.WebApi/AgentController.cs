using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using Stardust.Server;

namespace Stardust.Web.Controllers;

/// <summary>星尘代理下载安装页面（API版）。提供下载目录 JSON 与本机 StarAgent 探测</summary>
[DisplayName("代理安装")]
[AllowAnonymous]
[Route("[controller]/[action]")]
public class AgentController : ControllerBase
{
    private readonly StarServerSetting _setting;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>实例化</summary>
    /// <param name="setting"></param>
    /// <param name="httpClientFactory"></param>
    public AgentController(StarServerSetting setting, IHttpClientFactory httpClientFactory)
    {
        _setting = setting;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>代理安装页面。配置了 StarServer 安装页地址时跳转，否则返回本站下载目录 JSON</summary>
    /// <returns></returns>
    [Route("/Agent")]
    public ActionResult Index()
    {
        // 清单以 StarServer 安装引导页为准，配置了地址后本站只做跳转，避免维护第二份清单。
        // JSON 请求保持原样，兼容仍在调用旧接口的客户端
        var target = _setting.AgentServerUrl;
        if (!target.IsNullOrEmpty() && !IsJsonRequest())
        {
            target = target.TrimEnd('/');
            var current = $"{Request.Scheme}://{Request.Host}";
            if (!target.EqualIgnoreCase(current)) return Redirect(target + "/");
        }

        // WebApi 无视图，非跳转分支统一返回下载目录 JSON（MVC 的 HTML 渲染由 SPA 承担）
        return Content(JsonSerializer.Serialize(new { code = 0, message = (String?)null, data = GetAgentData() }), "application/json", Encoding.UTF8);
    }

    /// <summary>探测本机StarAgent。等价 MVC 安装页的浏览器探测（localhost:5500/info），由 API 宿主代为执行</summary>
    /// <returns>online 表示是否探测到；data 为 StarAgent 的 info 数据</returns>
    [Route("/Agent/Probe")]
    public async Task<ActionResult> Probe()
    {
        var online = false;
        Object? data = null;
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await client.GetAsync("http://localhost:5500/info", cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            data = JsonSerializer.Deserialize<Object>(json);
            online = true;
        }
        catch (Exception ex)
        {
            // 未安装或未启动属常态，降级为探测失败
            XTrace.Log?.Debug("AgentProbe: {0}", ex.Message);
        }

        return Content(JsonSerializer.Serialize(new { code = 0, message = (String?)null, data = new { online, data } }), "application/json", Encoding.UTF8);
    }

    /// <summary>判断当前请求是否要求JSON格式</summary>
    /// <returns>JSON请求返回true</returns>
    private Boolean IsJsonRequest()
    {
        var accept = Request.Headers["Accept"].FirstOrDefault();
        if (accept != null && accept.Contains("application/json")) return true;

        return Request.Query["format"].FirstOrDefault().EqualIgnoreCase("json");
    }

    /// <summary>StarAgent等资源下载基础地址，保证以/结尾</summary>
    private String DownloadBase()
    {
        var url = _setting.AgentDownloadBaseUrl;
        if (url.IsNullOrEmpty()) url = "https://x.newlifex.com/star/";
        return url.EnsureEnd("/");
    }

    /// <summary>.NET运行时安装脚本基础地址，位于下载基础地址同级dotnet/目录</summary>
    private String DotnetBase() => new Uri(new Uri(DownloadBase()), "../dotnet/").ToString();

    private Object GetAgentData()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var agentUrl = DownloadBase();
        var dotnetUrl = DotnetBase();

        return new
        {
            server = baseUrl,
            agent = new
            {
                zip = new[]
                {
                    new { name = "staragent10.zip", framework = "net10.0", os = "Windows/Linux", url = $"{agentUrl}staragent10.zip" },
                    new { name = "staragent90.zip", framework = "net9.0", os = "Windows/Linux", url = $"{agentUrl}staragent90.zip" },
                    new { name = "staragent80.zip", framework = "net8.0", os = "Windows/Linux", url = $"{agentUrl}staragent80.zip" },
                    new { name = "staragent60.zip", framework = "net6.0", os = "Windows/Linux", url = $"{agentUrl}staragent60.zip" },
                    new { name = "staragent45.zip", framework = "net4.5", os = "Windows", url = $"{agentUrl}staragent45.zip" },
                    new { name = "staragent31.zip", framework = "netcoreapp3.1", os = "Mips64(旧龙芯)", url = $"{agentUrl}staragent31.zip" },
                },
                targz = new[]
                {
                    new { name = "staragent10.tar.gz", framework = "net10.0", os = "Linux", url = $"{agentUrl}staragent10.tar.gz" },
                    new { name = "staragent90.tar.gz", framework = "net9.0", os = "Linux", url = $"{agentUrl}staragent90.tar.gz" },
                    new { name = "staragent80.tar.gz", framework = "net8.0", os = "Linux/LA64(新龙芯)", url = $"{agentUrl}staragent80.tar.gz" },
                    new { name = "staragent60.tar.gz", framework = "net6.0", os = "Linux", url = $"{agentUrl}staragent60.tar.gz" },
                    new { name = "staragent31.tar.gz", framework = "netcoreapp3.1", os = "Linux/Mips64(旧龙芯)", url = $"{agentUrl}staragent31.tar.gz" },
                },
                scripts = new[]
                {
                    new { name = "star.sh (net9.0)", description = "自动安装net9.0版", url = $"{agentUrl}star.sh" },
                    new { name = "star8.sh (net8.0)", description = "自动安装net8.0版", url = $"{agentUrl}star8.sh" },
                },
            },
            packages = new[]
            {
                new { name = "star.zip", description = "星尘整体安装包", url = $"{agentUrl}star.zip" },
                new { name = "StarServer.zip", description = "星尘服务端", url = $"{agentUrl}StarServer.zip" },
                new { name = "StarWeb.zip", description = "星尘管理平台", url = $"{agentUrl}StarWeb.zip" },
            },
            dotnet = new[]
            {
                new { name = "net.sh", description = ".NET 最新版运行时安装脚本", url = $"{dotnetUrl}net.sh" },
                new { name = "net10.sh", description = ".NET 10.0 运行时安装脚本", url = $"{dotnetUrl}net10.sh" },
                new { name = "net9.sh", description = ".NET 9.0 运行时安装脚本", url = $"{dotnetUrl}net9.sh" },
                new { name = "net8.sh", description = ".NET 8.0 运行时安装脚本", url = $"{dotnetUrl}net8.sh" },
                new { name = "net6.sh", description = ".NET 6.0 运行时安装脚本", url = $"{dotnetUrl}net6.sh" },
            },
        };
    }
}
