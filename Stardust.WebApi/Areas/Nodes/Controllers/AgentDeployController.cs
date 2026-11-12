using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Log;
using Stardust;
using Stardust.Models;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>StarAgent远程部署</summary>
[Menu(70, true, Icon = "fa-rocket")]
[DisplayName("远程部署")]
[NodesArea]
public class AgentDeployController : ControllerBaseX
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StarFactory _starFactory;
    private readonly ITracer _tracer;

    /// <summary>实例化</summary>
    public AgentDeployController(IHttpClientFactory httpClientFactory, StarFactory starFactory, ITracer tracer)
    {
        _httpClientFactory = httpClientFactory;
        _starFactory = starFactory;
        _tracer = tracer;
    }

    /// <summary>部署面板首屏默认值。对齐 MVC Index 下发的 ServerUrl/DownloadUrl</summary>
    /// <returns></returns>
    public ActionResult Index()
    {
        // 获取服务器地址
        var serverUrl = $"http://{Request.Host}";

        return Json(0, null, new { serverUrl, downloadUrl = "http://x.newlifex.com/star/" });
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
                // 调用StarServer接口，附加应用令牌（Server端AgentDeploy已要求鉴权，安全V-3）
                var httpClient = _httpClientFactory.CreateClient();
                var token = _starFactory?.Client?.Token;
                if (!token.IsNullOrEmpty())
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // task-95 自环修复（2026-11-12 证实的独立 bug）：转发目标必须是 StarServer（配置键），
                // 原 Request.Host=StarWebApi 自身，自调用自收=递归；StarWebApi 宿主不承载 AgentDeploy 真实现。
                // 守卫信封用本宿主 ControllerBaseX.Json(code,message)（MVC 孪生匿名 Json(Object) 重载在 API 版 Cube 不存在）
                var serverUrl = _starFactory?.Server;
                if (serverUrl.IsNullOrEmpty())
                    return Json(500, L.T("StarServer 地址未配置（appsettings StarServer 键），无法转发部署请求。"));

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
                return Json(500, L.T("Local execution in StarWeb is not implemented; use StarServer instead."));
            }

            return Json(0, L.T("Deployment completed."), results);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);

            return Json(500, ex.Message);
        }
    }

    /// <summary>测试连接。凭据改走 POST JSON body（task-95 二期 N3：查询串密码随访问日志/链路明文落盘，根治=body）。
    /// 兼容窗：查询串提交凭据的旧调用方得到明确改道指引（不静默断）。
    /// 内部转发仍走查询串（StarServer 侧 TestConnection 为 query-only 绑定，Stardust.Server 出本卡 writeScopes，候 lead 批后同步 body 化）。</summary>
    /// <param name="model">连接参数（body）</param>
    /// <returns>连接测试结果</returns>
    [HttpPost]
    public async Task<ActionResult> TestConnection([FromBody] TestConnectionModel? model)
    {
        // 兼容窗：旧调用以查询串提交凭据（如 ?password=xxx），返回明确指引而非静默失败
        if (Request.Query.ContainsKey("password"))
            return Json(400, L.T("TestConnection 凭据已改走 POST JSON body {host,port,userName,password,executeOn}；查询串提交密码已停用（安全 N3）。"));

        if (model == null) return Json(400, L.T("缺少请求体：请以 JSON {host,port,userName,password,executeOn} 提交。"));

        var host = model.Host;
        var port = model.Port;
        var userName = model.UserName;
        var password = model.Password;
        var executeOn = model.ExecuteOn.IsNullOrEmpty() ? "server" : model.ExecuteOn;

        try
        {
            Object? result;

            if (executeOn.EqualIgnoreCase("server"))
            {
                // 调用StarServer接口，附加应用令牌（Server端AgentDeploy已要求鉴权，安全V-3）
                var httpClient = _httpClientFactory.CreateClient();
                var token = _starFactory?.Client?.Token;
                if (!token.IsNullOrEmpty())
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // task-95 自环修复（2026-11-12 证实的独立 bug）：转发目标必须是 StarServer（配置键），
                // 原 Request.Host=StarWebApi 自身，自调用自收=递归；StarWebApi 宿主不承载 AgentDeploy 真实现。
                // 守卫信封用本宿主 ControllerBaseX.Json(code,message)（MVC 孪生匿名 Json(Object) 重载在 API 版 Cube 不存在）
                var serverUrl = _starFactory?.Server;
                if (serverUrl.IsNullOrEmpty())
                    return Json(500, L.T("StarServer 地址未配置（appsettings StarServer 键），无法转发连接测试。"));

                // N3 落地（2026-11-12 lead 批）：StarServer TestConnection 已改 [FromBody] DTO，转发改纯 JSON body，凭据零经查询串
                var json = JsonSerializer.Serialize(new { host, port, userName, password });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{serverUrl}/AgentDeploy/TestConnection", content);
                response.EnsureSuccessStatusCode();

                var resultJson = await response.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<Object>(resultJson);
            }
            else
            {
                // StarWeb本地执行（暂不支持）
                result = new { success = false, message = L.T("Local execution in StarWeb is not implemented; use StarServer instead.") };
            }

            return Json(0, null, result);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return Json(500, ex.Message);
        }
    }
}

/// <summary>连接测试参数（task-95 N3：凭据走 body，不入查询串/访问日志）</summary>
public class TestConnectionModel
{
    /// <summary>主机</summary>
    public String Host { get; set; } = null!;

    /// <summary>端口</summary>
    public Int32 Port { get; set; }

    /// <summary>用户名</summary>
    public String UserName { get; set; } = null!;

    /// <summary>密码</summary>
    public String Password { get; set; } = null!;

    /// <summary>执行位置：server 或 web</summary>
    public String ExecuteOn { get; set; } = "server";
}
