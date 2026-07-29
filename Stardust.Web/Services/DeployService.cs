using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Deployment;
using Stardust.Models;
using Stardust.Server;
using Attachment = NewLife.Cube.Entity.Attachment;

namespace Stardust.Web.Services;

public class DeployService(StarFactory starFactory, ITracer tracer)
{
    /// <summary>编译控制</summary>
    /// <param name="app">应用部署集</param>
    /// <param name="buildNode">编译节点</param>
    /// <param name="action">操作。Build-Upload/Package-Upload</param>
    /// <param name="ip">客户端IP</param>
    public async Task<Int32> Compile(AppDeploy app, AppBuildNode buildNode, String action, String ip, CancellationToken cancellationToken = default, Boolean writeHistory = true)
    {
        if (buildNode == null) throw new ArgumentNullException(nameof(buildNode));

        app ??= buildNode.Deploy;
        if (app == null) throw new Exception($"编译节点[{buildNode}]上的应用部署集不存在！");

        await Task.Yield();

        using var span = starFactory.Tracer?.NewSpan($"Compile-{action}", buildNode);

        var msg = "";
        var success = true;
        CommandReplyModel? reply = null;
        try
        {
            // 根据操作类型决定编译步骤
            var pullCode = buildNode.PullCode;
            var buildProject = buildNode.BuildProject;
            var packageOutput = buildNode.PackageOutput;
            var uploadPackage = buildNode.UploadPackage;

            // Package-Upload 仅打包上传，不拉代码和编译
            if (action.EqualIgnoreCase("Package-Upload"))
            {
                pullCode = false;
                buildProject = false;
                packageOutput = true;
                uploadPackage = true;
            }

            // 构造编译命令参数
            var cmd = new CompileCommand
            {
                Repository = app.Repository,
                DeployKey = app.DeployKey,
                RepoUserName = app.RepoUserName,
                Branch = app.Branch,
                SourcePath = buildNode.SourcePath,
                ProjectPath = app.ProjectPath,
                ProjectKind = app.ProjectKind,
                BuildArgs = app.BuildArgs,
                OutputPath = buildNode.OutputPath.IsNullOrEmpty() ? "publish" : buildNode.OutputPath,
                PackageFilters = app.PackageFilters,
                DeployName = app.Name,
                PullCode = pullCode,
                BuildProject = buildProject,
                PackageOutput = packageOutput,
                UploadPackage = uploadPackage,
            };

            // 处理仓库密码：用系统密钥解密存储密文，再用节点密钥加密下发
            if (!app.RepoPassword.IsNullOrEmpty() && buildNode.Node != null)
            {
                var key = StarServerSetting.Current.TokenSecret.Split(':')[1];
                var pass = Encoding.UTF8.GetBytes(key);
                using var aes = Aes.Create();
                var plainPassword = Encoding.UTF8.GetString(aes.Decrypt(app.RepoPassword.ToHex(), pass, CipherMode.CBC, PaddingMode.PKCS7));

                var nodePass = Encoding.UTF8.GetBytes(buildNode.Node.Secret);
                using var aes2 = Aes.Create();
                cmd.RepoPassword = aes2.Encrypt(Encoding.UTF8.GetBytes(plainPassword), nodePass, CipherMode.CBC, PaddingMode.PKCS7).ToHex();
            }

            var args = cmd.ToJson();
            // 脱敏：生成历史记录副本，去掉 DeployKey 和 Repository 中的凭据
            var safeCmd = cmd.RedactForHistory();
            msg = safeCmd.ToJson();

            // fire-and-forget 下发（timeout=0 表示不等待节点回包）。返回 NodeCommand.Id 供调用方（流水线）记录 CommandId
            reply = await starFactory.SendNodeCommandAsync(buildNode.Node.Code, "deploy/compile", args, 0, 3600, 0, cancellationToken);
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
            // writeHistory:false 时（如流水线）跳过写入，真实历史由节点 PostEvents 与 CommandReply 事件负责
            if (writeHistory)
            {
                var hi = AppDeployHistory.Create(buildNode.DeployId, buildNode.NodeId, $"deploy/compile/{action}", success, msg, ip);
                hi.SaveAsync();
            }
        }

        // 异常时由 catch 的 throw 向外传播，不会执行到此处；正常返回命令 Id（fire-and-forget 时 SendNodeCommandAsync 返回 null，则回退 0）
        return (Int32)(reply?.Id ?? 0);
    }

    /// <summary>发布控制</summary>
    /// <param name="app">应用部署集</param>
    /// <param name="deployNode">部署节点</param>
    /// <param name="action">操作。install/start/stop/restart/uninstall</param>
    /// <param name="ip">客户端IP</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="resources">资源列表。逗号分隔的资源名称</param>
    public async Task<Int32> Control(AppDeploy app, AppDeployNode deployNode, String action, String ip, Int32 startTime, Int32 timeout, String[] resources = null, CancellationToken cancellationToken = default, Boolean writeHistory = true)
    {
        if (deployNode == null) throw new ArgumentNullException(nameof(deployNode));

        app ??= deployNode.Deploy;
        if (app == null || !app.Enable) throw new Exception($"节点[{deployNode}]上的应用部署集[{app}]未启用！");

        await Task.Yield();

        using var span = starFactory.Tracer?.NewSpan($"Deploy-{action}", deployNode);

        var msg = "";
        var success = true;
        CommandReplyModel? reply = null;
        try
        {
            switch (action.ToLower())
            {
                case "install":
                    action = "deploy/install";
                    Install(deployNode, resources);
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

            // fire-and-forget 下发（timeout=0 表示不等待节点回包）。返回 NodeCommand.Id 供调用方（流水线）记录 CommandId
            reply = await starFactory.SendNodeCommandAsync(deployNode.Node.Code, action, args, startTime, startTime + 60, timeout, cancellationToken);
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
            // writeHistory:false 时（如流水线）跳过写入，真实历史由节点 PostEvents 与 CommandReply 事件负责
            if (writeHistory)
            {
                var hi = AppDeployHistory.Create(deployNode.DeployId, deployNode.NodeId, action, success, msg, ip);
                hi.SaveAsync();
            }
        }

        // 异常时由 catch 的 throw 向外传播，不会执行到此处；正常返回命令 Id（fire-and-forget 时 SendNodeCommandAsync 返回 null，则回退 0）
        return (Int32)(reply?.Id ?? 0);
    }

    /// <summary>安装应用</summary>
    /// <param name="deployNode">部署节点</param>
    /// <param name="resources">资源列表。保留参数兼容，不再使用</param>
    public void Install(AppDeployNode deployNode, String[] resources = null)
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