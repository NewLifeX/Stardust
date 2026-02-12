using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Extensions;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>StarAgent远程部署服务</summary>
[ApiFilter]
[Route("[controller]/[action]")]
public class AgentDeployController(AgentDeployService deployService, ITracer tracer) : ControllerBase
{
    /// <summary>执行远程部署</summary>
    /// <param name="model">部署参数</param>
    /// <returns>部署结果</returns>
    [HttpPost]
    public List<AgentDeployResult> Deploy([FromBody] AgentDeployModel model)
    {
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
