using System.IO.Compression;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Deployment;
using Stardust.Models;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Services;

public class DeployService(StarFactory starFactory, ITracer tracer)
{
    public async Task Control(AppDeploy app, AppDeployNode deployNode, String action, String ip, Int32 startTime, Int32 timeout)
    {
        if (deployNode == null) throw new ArgumentNullException(nameof(deployNode));

        app ??= deployNode.Deploy;
        //if (app == null) throw new ArgumentNullException(nameof(deployNode));
        //if (!deployNode.Enable || app == null || !app.Enable) throw new Exception("部署节点未启用！");
        if (app == null || !app.Enable) throw new Exception($"节点[{deployNode}]上的应用部署集[{app}]未启用！");

        await Task.Yield();

        using var span = starFactory.Tracer?.NewSpan($"Deploy-{action}", deployNode);

        var msg = "";
        var success = true;
        try
        {
            switch (action.ToLower())
            {
                case "install":
                    action = "deploy/install";
                    Install(deployNode);
                    break;
                case "start":
                    action = "deploy/start";
                    Start(deployNode);
                    break;
                case "stop":
                    action = "deploy/stop";
                    Stop(deployNode);
                    break;
                case "restart":
                    action = "deploy/restart";
                    Restart(deployNode);
                    break;
                case "uninstall":
                    action = "deploy/uninstall";
                    Uninstall(deployNode);
                    break;
                default:
                    throw new NotSupportedException($"不支持{action}");
            }

            // 发布安装命令时，为了兼容旧版本，继续传递AppName参数
            var deployName = deployNode.DeployName;
            if (deployName.IsNullOrEmpty()) deployName = app?.Name;

            var args = new { deployNode.Id, DeployName = deployName, app?.AppName }.ToJson();
            msg = args;

            await starFactory.SendNodeCommand(deployNode.Node.Code, action, args, startTime, startTime + 60, timeout);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            msg = ex.Message;
            success = false;

            throw;
        }
        finally
        {
            var hi = AppDeployHistory.Create(deployNode.DeployId, deployNode.NodeId, action, success, msg, ip);
            hi.SaveAsync();
        }
    }

    public void Install(AppDeployNode deployNode)
    {
        deployNode.Enable = true;
        deployNode.Update();
    }

    public void Start(AppDeployNode deployNode)
    {
        deployNode.Enable = true;
        deployNode.Update();
    }

    public void Stop(AppDeployNode deployNode)
    {
        deployNode.Enable = false;
        deployNode.Update();
    }

    public void Restart(AppDeployNode deployNode)
    {
        deployNode.Enable = true;
        deployNode.Update();
    }

    public void Uninstall(AppDeployNode deployNode)
    {
        deployNode.Enable = false;
        deployNode.Update();
    }

    /// <summary>从zip包读取dotnet信息</summary>
    /// <param name="version"></param>
    /// <param name="attachment"></param>
    /// <param name="uploadPath"></param>
    /// <returns></returns>
    public Boolean ReadDotNet(AppDeployVersion version, Attachment attachment, String uploadPath)
    {
        if (version == null || attachment == null) return false;
        if (attachment.Extension != ".zip") return false;

        var fi = attachment.GetFilePath(uploadPath).AsFile();
        if (!fi.Exists) return false;

        using var span = tracer?.NewSpan(nameof(ReadDotNet), fi.FullName);

        // 在zip包中查找后缀为.nginx或.conf的文件，以文本打开，按照nginx文件格式识别其中的listen监听端口
        using var zip = ZipFile.Open(fi.FullName, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.Name.EndsWithIgnoreCase(".runtimeconfig.json"))
            {
                var txt = entry.Open().ToStr();
                var match = Regex.Match(txt, """
                "tfm"\s*:\s*"([^"]+)"
                """, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    version.TargetFramework = match.Groups[1].Value.Trim('"');
                    span?.AppendTag(version.TargetFramework);
                    return true;
                }
            }
            else if (entry.Name.EndsWithIgnoreCase(".exe.config"))
            {
                var txt = entry.Open().ToStr();
                var match = Regex.Match(txt, """
                sku\s*=\s*"\.NETFramework,Version=v([^"]+)"
                """, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    version.TargetFramework = "net" + match.Groups[1].Value.Trim('"');
                    span?.AppendTag(version.TargetFramework);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>从zip包读取nginx信息</summary>
    /// <param name="version"></param>
    /// <param name="attachment"></param>
    /// <param name="uploadPath"></param>
    /// <returns></returns>
    public Boolean ReadNginx(AppDeployVersion version, Attachment attachment, String uploadPath)
    {
        if (version == null || attachment == null) return false;
        if (attachment.Extension != ".zip") return false;

        // 读取其中的nginx文件，识别监听端口
        var deploy = version.Deploy;
        if (deploy == null || deploy.Port != 0 && !deploy.Urls.IsNullOrEmpty()) return false;

        var fi = attachment.GetFilePath(uploadPath).AsFile();
        if (!fi.Exists) return false;

        using var span = tracer?.NewSpan(nameof(ReadNginx), fi.FullName);

        // 在zip包中查找后缀为.nginx或.conf的文件，以文本打开，按照nginx文件格式识别其中的listen监听端口
        using var zip = ZipFile.Open(fi.FullName, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (!entry.Name.EndsWithIgnoreCase(".nginx", ".conf")) continue;

            var nginx = new NginxFile();
            if (!nginx.Parse(entry.Open().ToStr())) continue;

            span?.AppendTag($"nginx:{nginx.ServerName}");

            // 获取后端端口
            if (deploy.Port == 0)
            {
                var backend = nginx.GetBackends().FirstOrDefault();
                if (!backend.IsNullOrEmpty())
                {
                    var uri = new Uri(backend);
                    if (uri.Port > 0) deploy.Port = uri.Port;

                    span?.AppendTag(backend);
                }
            }

            // 获取对外服务地址
            if (deploy.Urls.IsNullOrEmpty() && !nginx.ServerName.IsNullOrEmpty())
            {
                var schema = nginx.Ports.Any(e => e % 1000 == 443) ? "https" : "http";
                var host = nginx.ServerName.Split(',').FirstOrDefault();
                var port = nginx.Ports.Count > 0 ? nginx.Ports.Max() : 0;

                if (schema == "https" && port % 1000 == 443 || schema == "http" && port % 100 == 80)
                    deploy.Urls = $"{schema}://{host}";
                else
                    deploy.Urls = $"{schema}://{host}:{port}";

                span?.AppendTag(deploy.Urls);
            }

            // 找到一个就行了
            return true;
        }

        return false;
    }

    /// <summary>向zip包写入nginx信息</summary>
    /// <param name="version"></param>
    /// <param name="attachment"></param>
    /// <param name="uploadPath"></param>
    public Boolean BuildNginx(AppDeployVersion version, Attachment attachment, String uploadPath)
    {
        if (version == null || attachment == null) return false;
        if (attachment.Extension != ".zip") return false;

        // 如果是标准包或者完整包，检测zip包是否有nginx配置文件，如果没有则主动添加一个
        if (version.Mode is not DeployModes.Standard and not DeployModes.Full) return false;

        var deploy = version.Deploy;
        if (deploy == null || deploy.Port <= 0 || deploy.Urls.IsNullOrEmpty()) return false;

        var fi = attachment.GetFilePath(uploadPath).AsFile();
        if (!fi.Exists) return false;

        using var span = tracer?.NewSpan(nameof(BuildNginx), fi.FullName);

        // 如果没有nginx配置文件，则添加一个默认的
        {
            using var zip = ZipFile.Open(fi.FullName, ZipArchiveMode.Update);
            if (zip.Entries.Any(e => e.Name.EndsWithIgnoreCase(".nginx", ".conf"))) return false;

            var uri = new Uri(deploy.Urls);
            var nginx = new NginxFile
            {
                ServerName = uri.Host
            };
            if (uri.Port % 1000 == 443)
                nginx.Ports = [uri.Port / 1000 + 80, uri.Port];
            else
                nginx.Ports = [uri.Port];

            // 后端端口
            nginx.SetBackends($"http://localhost:{deploy.Port}");

            var txt = nginx.ToString();
            span?.AppendTag(txt);

            // 保存到zip包中
            var entry = zip.CreateEntry($"{uri.Host}.nginx", CompressionLevel.Optimal);
            {
                using var stream = entry.Open();
                stream.Write(txt.GetBytes());
            }
        }

        // 更新附件信息
        {
            fi.Refresh();
            attachment.Hash = fi.MD5().ToHex();
            attachment.Size = fi.Length;
            attachment.Update();
        }

        return true;
    }
}