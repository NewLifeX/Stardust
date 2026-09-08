using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Services;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>StarAgent远程部署服务</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class AgentDeployController(AgentDeployService deployService, ITokenService tokenService, ITracer tracer) : ControllerBase
{
    /// <summary>验证调用方。远程部署把服务器当作SSH跳板，仅允许已配置密钥且具控制权的管理应用（如StarWeb）调用</summary>
    /// <returns>通过校验的应用</returns>
    private App Valid()
    {
        var token = ApiFilterAttribute.GetToken(HttpContext);
        if (token.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, "未提供令牌");

        var (jwt, ex) = tokenService.DecodeToken(token);
        if (ex != null) throw ex;

        var app = App.FindByName(jwt?.Subject);
        if (app == null || !app.Enable) throw new ApiException(ApiCode.Unauthorized, "无效应用令牌");

        // 空密钥应用可被匿名冒用（如种子StarWeb），禁止使用远程部署等高危能力
        if (app.Secret.IsNullOrEmpty())
            throw new ApiException(ApiCode.Forbidden, $"[{app}]未配置密钥，禁止远程部署！请在注册中心为该应用设置Secret");

        // 仅具控制权的管理应用（配置了AllowControlNodes）可远程部署
        if (app.AllowControlNodes.IsNullOrEmpty())
            throw new ApiException(ApiCode.Forbidden, $"[{app}]无控制权限，禁止远程部署！");

        return app;
    }

    /// <summary>执行远程部署</summary>
    /// <param name="model">部署参数</param>
    /// <returns>部署结果</returns>
    [HttpPost]
    public List<AgentDeployResult> Deploy([FromBody] AgentDeployModel model)
    {
        Valid();

        using var span = tracer?.NewSpan("AgentDeploy-Deploy", model);

        try
        {
            XTrace.WriteLine($"收到远程部署请求: Hosts={model.Hosts}, User={model.UserName}, OS={model.OSType}, Server={model.ServerUrl}");

            var results = deployService.Deploy(model);

            var success = results.Count(r => r.Success);
            var failed = results.Count - success;

            XTrace.WriteLine($"部署完成: 成功={success}, 失败={failed}");

            return results;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);
            throw;
        }
    }

    /// <summary>测试连接</summary>
    /// <param name="host">目标主机</param>
    /// <param name="port">SSH端口</param>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>连接测试结果</returns>
    [HttpPost]
    public Object TestConnection(String host, Int32 port, String userName, String password)
    {
        Valid();

        if (host.IsNullOrEmpty()) throw new ArgumentNullException(nameof(host));
        if (userName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(userName));
        if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

        try
        {
            // 简单测试：尝试SSH连接并执行echo命令
            var cmd = $"sshpass -p '{password}' ssh -p {port} -o StrictHostKeyChecking=no -o ConnectTimeout=10 {userName}@{host} 'echo connected'";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{cmd.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return new { success = false, message = "无法启动进程" };

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(15000))
            {
                process.Kill();
                return new { success = false, message = "连接超时" };
            }

            if (process.ExitCode == 0 && output.Contains("connected"))
            {
                return new { success = true, message = "连接成功" };
            }
            else
            {
                return new { success = false, message = $"连接失败: {error}" };
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return new { success = false, message = ex.Message };
        }
    }
}
