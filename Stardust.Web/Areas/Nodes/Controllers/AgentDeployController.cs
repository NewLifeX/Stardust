using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using Stardust.Models;
using Stardust.Server;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>StarAgent远程部署</summary>
[Menu(70, true, Icon = "fa-rocket")]
[DisplayName("远程部署")]
[NodesArea]
public class AgentDeployController : ControllerBaseX
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITracer _tracer;

    /// <summary>实例化</summary>
    public AgentDeployController(IHttpClientFactory httpClientFactory, ITracer tracer)
    {
        _httpClientFactory = httpClientFactory;
        _tracer = tracer;
    }

    /// <summary>部署页面</summary>
    /// <returns></returns>
    public ActionResult Index()
    {
        // 获取服务器地址
        var serverUrl = $"http://{Request.Host}";

        ViewBag.ServerUrl = serverUrl;
        ViewBag.DownloadUrl = "http://x.newlifex.com/star/";

        return View();
    }

    /// <summary>执行部署</summary>
    /// <param name="model">部署参数</param>
    /// <param name="executeOn">执行位置：web或server</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult> Deploy([FromBody] AgentDeployModel model, String executeOn = "server")
    {
        using var span = _tracer?.NewSpan("AgentDeploy", model);

        try
        {
            List<AgentDeployResult> results;

            if (executeOn.EqualIgnoreCase("server"))
            {
                // 调用StarServer接口
                var httpClient = _httpClientFactory.CreateClient();
                var serverUrl = $"http://{Request.Host}";

                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{serverUrl}/AgentDeploy/Deploy", content);
                response.EnsureSuccessStatusCode();

                var resultJson = await response.Content.ReadAsStringAsync();
                results = JsonSerializer.Deserialize<List<AgentDeployResult>>(resultJson) ?? [];
            }
            else
            {
                // StarWeb本地执行（暂不支持，返回错误）
                return Json(new { code = 500, message = "StarWeb本地执行功能暂未实现，请选择StarServer执行" });
            }

            return Json(new { code = 0, data = results, message = "部署完成" });
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);

            return Json(new { code = 500, message = ex.Message });
        }
    }

    /// <summary>测试连接</summary>
    /// <param name="host">主机</param>
    /// <param name="port">端口</param>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="executeOn">执行位置</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult> TestConnection(String host, Int32 port, String userName, String password, String executeOn = "server")
    {
        try
        {
            Object? result;

            if (executeOn.EqualIgnoreCase("server"))
            {
                // 调用StarServer接口
                var httpClient = _httpClientFactory.CreateClient();
                var serverUrl = $"http://{Request.Host}";

                var param = new { host, port, userName, password };
                var json = JsonSerializer.Serialize(param);
                var content = new StringContent(json, Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await httpClient.PostAsync($"{serverUrl}/AgentDeploy/TestConnection?host={host}&port={port}&userName={userName}&password={password}", content);
                response.EnsureSuccessStatusCode();

                var resultJson = await response.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<Object>(resultJson);
            }
            else
            {
                // StarWeb本地执行（暂不支持）
                result = new { success = false, message = "StarWeb本地执行功能暂未实现，请选择StarServer执行" };
            }

            return Json(new { code = 0, data = result });
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return Json(new { code = 500, message = ex.Message });
        }
    }
}
