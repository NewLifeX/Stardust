using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Security;
using Stardust.Server.Models;

namespace Stardust.Server.Services;

/// <summary>StarAgent远程部署服务</summary>
public class AgentDeployService
{
    private readonly ITracer _tracer;

    /// <summary>实例化</summary>
    public AgentDeployService(ITracer tracer) => _tracer = tracer;

    /// <summary>执行远程部署</summary>
    /// <param name="model">部署参数</param>
    /// <returns>部署结果列表</returns>
    public List<AgentDeployResult> Deploy(AgentDeployModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (model.Hosts.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Hosts));
        if (model.UserName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.UserName));
        if (model.Password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Password));

        using var span = _tracer?.NewSpan("AgentDeploy", model);

        // 解析主机列表
        var hosts = ParseHosts(model.Hosts);
        XTrace.WriteLine($"解析到 {hosts.Count} 个目标主机");

        var results = new List<AgentDeployResult>();
        foreach (var host in hosts)
        {
            try
            {
                var result = DeploySingle(host, model);
                results.Add(result);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                results.Add(new AgentDeployResult
                {
                    Host = host,
                    Success = false,
                    Message = $"部署失败: {ex.Message}"
                });
            }
        }

        return results;
    }

    /// <summary>部署到单个主机</summary>
    private AgentDeployResult DeploySingle(String host, AgentDeployModel model)
    {
        XTrace.WriteLine($"开始部署到 {host}");

        var result = new AgentDeployResult { Host = host };

        try
        {
            // 生成部署脚本
            String scriptContent;
            String scriptFile;
            String executeCmd;

            if (model.OSType.EqualIgnoreCase("Windows"))
            {
                scriptContent = GenerateWindowsScript(model);
                scriptFile = $"/tmp/deploy_agent_{Rand.Next()}.ps1";
                executeCmd = GenerateWindowsDeployCommand(host, model, scriptFile);
            }
            else
            {
                scriptContent = GenerateLinuxScript(model);
                scriptFile = $"/tmp/deploy_agent_{Rand.Next()}.sh";
                executeCmd = GenerateLinuxDeployCommand(host, model, scriptFile);
            }

            // 写入脚本文件
            File.WriteAllText(scriptFile, scriptContent);
            XTrace.WriteLine($"生成脚本文件: {scriptFile}");

            // 执行部署命令
            var output = ExecuteCommand(executeCmd, 300000); // 5分钟超时
            result.Output = output;

            // 清理临时文件
            try { File.Delete(scriptFile); } catch { }

            // 判断是否成功
            if (output.Contains("安装成功") || output.Contains("installed successfully") || !output.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = true;
                result.Message = "部署成功";
                XTrace.WriteLine($"部署成功: {host}");
            }
            else
            {
                result.Success = false;
                result.Message = "部署失败，请查看输出日志";
                XTrace.WriteLine($"部署失败: {host}");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            XTrace.WriteException(ex);
        }

        return result;
    }

    /// <summary>生成Linux部署脚本</summary>
    private String GenerateLinuxScript(AgentDeployModel model)
    {
        var serverUrl = model.ServerUrl ?? "http://localhost:6600";
        var downloadUrl = model.DownloadUrl ?? "http://x.newlifex.com/star/";
        downloadUrl = downloadUrl.EnsureEnd("/");

        var dotnetScript = model.DotnetVersion switch
        {
            8 => "net8.sh",
            9 => "net9.sh",
            10 => "net10.sh",
            _ => "net.sh"
        };

        var agentFile = model.DotnetVersion switch
        {
            8 => "staragent80.tar.gz",
            9 => "staragent90.tar.gz",
            10 => "staragent100.tar.gz",
            _ => "staragent90.tar.gz"
        };

        var script = $@"#!/bin/bash

# 检查并安装.NET运行时
if [ ! -d ""/usr/lib/dotnet/"" ] && [ ! -d ""/usr/share/dotnet/"" ]; then
    echo ""正在安装.NET运行时...""
    curl {downloadUrl.Replace("star/", "dotnet/")}{dotnetScript} | bash
fi

# 下载StarAgent
gzfile=""{agentFile}""
if [ ! -f ""$gzfile"" ]; then
    echo ""正在下载StarAgent...""
    wget {downloadUrl}$gzfile
fi

# 解压
if [ ! -d ""agent/"" ]; then
    mkdir agent
fi
tar -xzf $gzfile -C agent

# 进入目录
cd agent

# 卸载旧版本
dotnet StarAgent.dll -uninstall 2>/dev/null || true

# 安装并指定服务器
echo ""正在安装StarAgent，服务器: {serverUrl}""
dotnet StarAgent.dll -install -server {serverUrl}

# 启动服务
if command -v systemctl &> /dev/null; then
    systemctl start staragent
    systemctl status staragent
fi

# 清理
cd ..
rm $gzfile -f

echo ""StarAgent安装成功""
";

        return script;
    }

    /// <summary>生成Windows部署脚本</summary>
    private String GenerateWindowsScript(AgentDeployModel model)
    {
        var serverUrl = model.ServerUrl ?? "http://localhost:6600";
        var downloadUrl = model.DownloadUrl ?? "http://x.newlifex.com/star/";
        downloadUrl = downloadUrl.EnsureEnd("/");

        var agentFile = model.DotnetVersion switch
        {
            8 => "staragent80.zip",
            9 => "staragent90.zip",
            10 => "staragent100.zip",
            _ => "staragent90.zip"
        };

        var script = $@"# StarAgent远程部署脚本（Windows）

Write-Host ""正在下载StarAgent...""
$url = ""{downloadUrl}{agentFile}""
$zipFile = ""$env:TEMP\{agentFile}""
$agentPath = ""$env:ProgramFiles\StarAgent""

# 下载文件
Invoke-WebRequest -Uri $url -OutFile $zipFile

# 解压
if (Test-Path $agentPath) {{
    Write-Host ""停止并删除旧版本...""
    dotnet ""$agentPath\StarAgent.dll"" -uninstall -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Remove-Item -Path $agentPath -Recurse -Force -ErrorAction SilentlyContinue
}}

New-Item -ItemType Directory -Path $agentPath -Force | Out-Null
Expand-Archive -Path $zipFile -DestinationPath $agentPath -Force

# 安装服务
Write-Host ""正在安装StarAgent，服务器: {serverUrl}""
Set-Location $agentPath
dotnet StarAgent.dll -install -server {serverUrl}

# 启动服务
Start-Service staragent -ErrorAction SilentlyContinue
Get-Service staragent

# 清理
Remove-Item $zipFile -Force -ErrorAction SilentlyContinue

Write-Host ""StarAgent安装成功""
";

        return script;
    }

    /// <summary>生成Linux部署命令</summary>
    private String GenerateLinuxDeployCommand(String host, AgentDeployModel model, String scriptFile)
    {
        // 使用sshpass传递密码（如果系统中有），否则需要手动输入
        // 由于不引入第三方依赖，这里使用expect或直接ssh
        var remoteScript = "/tmp/install_staragent.sh";

        var sb = new StringBuilder();

        // 先上传脚本
        sb.AppendLine($"sshpass -p '{model.Password}' scp -P {model.Port} -o StrictHostKeyChecking=no {scriptFile} {model.UserName}@{host}:{remoteScript} && \\");

        // 然后执行脚本
        sb.Append($"sshpass -p '{model.Password}' ssh -p {model.Port} -o StrictHostKeyChecking=no {model.UserName}@{host} 'chmod +x {remoteScript} && bash {remoteScript}'");

        return sb.ToString();
    }

    /// <summary>生成Windows部署命令</summary>
    private String GenerateWindowsDeployCommand(String host, AgentDeployModel model, String scriptFile)
    {
        // Windows使用PowerShell远程执行
        var remoteScript = "$env:TEMP\\install_staragent.ps1";

        var sb = new StringBuilder();

        // 使用pscp（PuTTY）或scp上传脚本，然后通过ssh执行PowerShell
        sb.Append($"sshpass -p '{model.Password}' scp -P {model.Port} -o StrictHostKeyChecking=no {scriptFile} {model.UserName}@{host}:{remoteScript} && ");
        sb.Append($"sshpass -p '{model.Password}' ssh -p {model.Port} -o StrictHostKeyChecking=no {model.UserName}@{host} 'powershell -ExecutionPolicy Bypass -File {remoteScript}'");

        return sb.ToString();
    }

    /// <summary>执行命令</summary>
    private String ExecuteCommand(String command, Int32 timeout = 60000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("无法启动进程");

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeout))
        {
            process.Kill();
            throw new TimeoutException($"命令执行超时（{timeout}ms）");
        }

        var result = output.ToString();
        if (error.Length > 0)
        {
            result += "\n=== 错误输出 ===\n" + error;
        }

        return result;
    }

    /// <summary>解析主机列表，支持单个IP、IP列表、CIDR网段</summary>
    private List<String> ParseHosts(String hosts)
    {
        var result = new List<String>();

        if (hosts.IsNullOrEmpty()) return result;

        // 分隔符：逗号、分号、换行
        var items = hosts.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in items)
        {
            var host = item.Trim();
            if (host.IsNullOrEmpty()) continue;

            // 检查是否为CIDR格式（如192.168.1.0/24）
            if (host.Contains('/'))
            {
                var ips = ParseCIDR(host);
                result.AddRange(ips);
            }
            else
            {
                result.Add(host);
            }
        }

        return result;
    }

    /// <summary>解析CIDR网段</summary>
    private List<String> ParseCIDR(String cidr)
    {
        var result = new List<String>();

        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) return result;

            var ipAddr = parts[0];
            var maskBits = Int32.Parse(parts[1]);

            if (maskBits < 0 || maskBits > 32) return result;

            var ip = IPAddress.Parse(ipAddr);
            var ipBytes = ip.GetAddressBytes();

            // 只支持IPv4
            if (ipBytes.Length != 4) return result;

            var ipInt = BitConverter.ToUInt32(ipBytes, 0);
            if (BitConverter.IsLittleEndian) ipInt = ReverseBytes(ipInt);

            // 计算网段范围
            var mask = 0xFFFFFFFFu << (32 - maskBits);
            var networkInt = ipInt & mask;
            var broadcastInt = networkInt | ~mask;

            // 生成该网段所有IP（跳过网络地址和广播地址）
            for (var i = networkInt + 1; i < broadcastInt; i++)
            {
                var currentIp = i;
                if (BitConverter.IsLittleEndian) currentIp = ReverseBytes(currentIp);

                var bytes = BitConverter.GetBytes(currentIp);
                var addr = new IPAddress(bytes);
                result.Add(addr.ToString());

                // 限制最多1024个IP
                if (result.Count >= 1024) break;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"解析CIDR失败: {cidr}, {ex.Message}");
        }

        return result;
    }

    /// <summary>反转字节序</summary>
    private static UInt32 ReverseBytes(UInt32 value)
    {
        return (value & 0x000000FFu) << 24 |
               (value & 0x0000FF00u) << 8 |
               (value & 0x00FF0000u) >> 8 |
               (value & 0xFF000000u) >> 24;
    }
}
