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
        if (token.IsNullOrEmpty()) throw new ApiException(ApiCode.Unauthorized, L.T("No token provided."));

        var (jwt, ex) = tokenService.DecodeToken(token);
        if (ex != null) throw ex;

        var app = App.FindByName(jwt?.Subject);
        if (app == null || !app.Enable) throw new ApiException(ApiCode.Unauthorized, L.T("Invalid application token"));

        // 空密钥应用可被匿名冒用（如种子StarWeb），禁止使用远程部署等高危能力
        if (app.Secret.IsNullOrEmpty())
            throw new ApiException(ApiCode.Forbidden, L.T("[{0}] has no secret configured; remote deployment is forbidden. Set a Secret for this application in the registry.", app));

        // 仅具控制权的管理应用（配置了AllowControlNodes）可远程部署
        if (app.AllowControlNodes.IsNullOrEmpty())
            throw new ApiException(ApiCode.Forbidden, L.T("[{0}] has no control permission; remote deployment is forbidden.", app));

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
            XTrace.WriteLine(L.TLog("Received remote deployment request: Hosts={0}, User={1}, OS={2}, Server={3}", model.Hosts, model.UserName, model.OSType, model.ServerUrl));

            var results = deployService.Deploy(model);

            var success = results.Count(r => r.Success);
            var failed = results.Count - success;

            XTrace.WriteLine(L.TLog("Deployment completed: succeeded={0}, failed={1}", success, failed));

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
            if (process == null) return new { success = false, message = L.T("Unable to start the process.") };

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(15000))
            {
                process.Kill();
                return new { success = false, message = L.T("Connection timed out.") };
            }

            if (process.ExitCode == 0 && output.Contains("connected"))
            {
                return new { success = true, message = L.T("Connected.") };
            }
            else
            {
                return new { success = false, message = L.T("Connection failed: {0}", error) };
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return new { success = false, message = ex.Message };
        }
    }
}
