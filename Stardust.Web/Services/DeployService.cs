using System;
using System.IO.Compression;
using NewLife;
using NewLife.Net;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Deployment;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Services;

public class DeployService
{
    private readonly StarFactory _starFactory;

    public DeployService(StarFactory starFactory) => _starFactory = starFactory;

    public async Task Control(AppDeploy app, AppDeployNode deployNode, String action, String ip, Int32 startTime, Int32 timeout)
    {
        if (deployNode == null) throw new ArgumentNullException(nameof(deployNode));

        app ??= deployNode.Deploy;
        //if (app == null) throw new ArgumentNullException(nameof(deployNode));
        //if (!deployNode.Enable || app == null || !app.Enable) throw new Exception("部署节点未启用！");
        if (app == null || !app.Enable) throw new Exception($"节点[{deployNode}]上的应用部署集[{app}]未启用！");

        await Task.Yield();

        using var span = _starFactory.Tracer?.NewSpan($"Deploy-{action}", deployNode);

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

            await _starFactory.SendNodeCommand(deployNode.Node.Code, action, args, startTime, startTime + 60, timeout);
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

    public void BuildNginx(AppDeployVersion version, Attachment attachment, String uploadPath)
    {
        if (version == null || attachment == null) return;
        if (attachment.Extension != ".zip") return;

        var deploy = version.Deploy;
        if (deploy == null) return;

        var fi = attachment.GetFilePath(uploadPath).AsFile();
        if (!fi.Exists) return;

        // 读取其中的nginx文件，识别监听端口
        if (deploy.Port == 0 || deploy.Urls.IsNullOrEmpty())
        {
            // 在zip包中查找后缀为.nginx或.conf的文件，以文本打开，按照nginx文件格式识别其中的listen监听端口
            using var zip = ZipFile.Open(fi.FullName, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWithIgnoreCase(".nginx") || entry.FullName.EndsWithIgnoreCase(".conf"))
                {
                    var nginx = new NginxFile();
                    if (nginx.Parse(entry.Open().ToStr()))
                    {
                        // 获取后端端口
                        if (deploy.Port == 0)
                        {
                            var backend = nginx.GetBackends().FirstOrDefault();
                            if (!backend.IsNullOrEmpty())
                            {
                                var uri = new Uri(backend);
                                if (uri.Port > 0) deploy.Port = uri.Port;
                            }
                        }

                        // 获取对外服务地址
                        if (deploy.Urls.IsNullOrEmpty() && !nginx.ServerName.IsNullOrEmpty())
                        {
                            var schema = nginx.Ports.Any(e => e % 443 == 0) ? "https" : "http";
                            var host = nginx.ServerName.Split(',').FirstOrDefault();
                            var port = nginx.Ports.Count > 0 ? nginx.Ports.Max() : 0;

                            if (schema == "https" && port % 443 == 0 || schema == "http" && port % 80 == 0)
                                deploy.Urls = $"{schema}://{host}";
                            else
                                deploy.Urls = $"{schema}://{host}:{port}";
                        }

                        //deploy.Update();

                        break; // 找到一个就行了
                    }
                }
            }
        }
    }
}