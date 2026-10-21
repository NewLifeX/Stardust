using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Models;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace DeployAgent;

/// <summary>部署服务。支持安装为Windows服务或Linux systemd，常驻接收星尘服务端下发的编译命令</summary>
internal class DeployService : ServiceBase
{
    private StarClient _client = null!;
    private StarFactory _factory;

    public DeployService()
    {
        ServiceName = "StarDeploy";
        DisplayName = "Stardust deploy";
        Description = "Stardust build and deployment tool. Automatically downloads repositories, builds, packs, and pushes artifacts to the Stardust release center, with version management and multi-platform distribution, working with StarAgent for automated deployment pipelines.";

        MachineInfo.RegisterAsync();
    }

    #region 服务生命周期
    /// <summary>服务启动</summary>
    /// <remarks>
    /// 安装Windows服务后，服务启动会执行一次该方法。
    /// 控制台菜单按5进入循环调试也会执行该方法。
    /// </remarks>
    public override void StartWork(String reason)
    {
        XTrace.WriteLine("Starting the Deploy client");

        var set = StarSetting.Current;

        var server = set.Server;
        if (server.IsNullOrEmpty())
        {
            XTrace.WriteLine("The Stardust server address is not configured; set Server in config/Star.config.");
            return;
        }

        // 初始化星尘工厂，用于追踪和配置
        _factory = new StarFactory(server, "StarDeploy", null);
        _factory.Register(ObjectContainer.Current);

        var client = new StarClient(server)
        {
            Name = "Deploy",
            Code = set.AppKey,
            Secret = set.Secret,
            ProductCode = "StarDeploy",
            Setting = set,

            Tracer = _factory?.Tracer,
            Log = XTrace.Log,
        };

        // 禁用客户端特性
        client.Features &= ~Features.Upgrade;

        client.Open();

        _client = client;

        // 注册编译命令
        client.RegisterCommand("deploy/compile", OnCompile);

        base.StartWork(reason);
    }

    /// <summary>服务停止</summary>
    /// <remarks>
    /// 安装Windows服务后，服务停止会执行该方法。
    /// 控制台菜单按5进入循环调试，任意键结束时也会执行该方法。
    /// </remarks>
    public override void StopWork(String reason)
    {
        base.StopWork(reason);

        _client?.Logout(reason);
        _client.TryDispose();
        _client = null!;

        _factory = null!;
    }
    #endregion

    #region SSH 密钥管理
    /// <summary>设置 SSH 密钥。将私钥写入临时文件，返回文件路径</summary>
    private String? SetupSshKey(String? deployKey)
    {
        if (deployKey.IsNullOrEmpty()) return null;

        // TrimStart 兼容用户粘贴时前导空白/换行
        var trimmedKey = deployKey.TrimStart();
        if (!trimmedKey.StartsWith("-----BEGIN"))
        {
            XTrace.WriteLine("Warning: invalid repository key format; skipping SSH key setup");
            return null;
        }

        var keyFile = Path.Combine(Path.GetTempPath(), $"stardust-deploy-key-{Guid.NewGuid():N}");
        // 显式指定 UTF-8 无 BOM 以减少跨平台歧义
        File.WriteAllText(keyFile, trimmedKey, new UTF8Encoding(false));

        // Linux 下设置仅 owner 可读写权限
        if (!Runtime.Windows)
        {
            try { File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { }
        }

        XTrace.WriteLine("SSH key configured: {0}", keyFile);
        return keyFile;
    }

    /// <summary>清理 SSH 密钥临时文件</summary>
    private void CleanupSshKey(String? keyFile)
    {
        if (keyFile.IsNullOrEmpty() || !File.Exists(keyFile)) return;

        try
        {
            File.Delete(keyFile);
            XTrace.WriteLine("SSH key cleaned up: {0}", keyFile);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Warning: failed to clean up the SSH key: {0}", ex.Message);
        }
    }
    #endregion

    #region 编译命令处理
    /// <summary>处理编译命令</summary>
    private String? OnCompile(String? args)
    {
        if (args.IsNullOrEmpty()) throw new ArgumentNullException(nameof(args));

        var cmd = ParseCompileCommand(args);

        // 解密仓库密码并用凭据组装克隆 URL
        var cloneUrl = BuildCloneUrl(cmd);

        XTrace.WriteLine("========== Starting build task ==========");
        XTrace.WriteLine("Repository: {0}", Stardust.Models.CompileCommand.RedactUrlForLog(cloneUrl));
        XTrace.WriteLine("Branch: {0}", cmd.Branch ?? "main");

        var sshKeyFile = SetupSshKey(cmd.DeployKey);

        // 累计构建日志，便于回传服务端写入部署历史
        var buildLog = new StringBuilder();

        var workDir = "";
        try
        {
            var outputPath = cmd.OutputPath;
            if (outputPath.IsNullOrEmpty()) outputPath = "publish";

            // 确定源代码目录，统一走 EnsureAndSyncRepo 处理 clone/pull
            var repoDir = EnsureAndSyncRepo(cmd, cloneUrl, buildLog, sshKeyFile, ref workDir);

            // 编译项目
            var publishDir = BuildOrResolvePublishDir(cmd, repoDir, outputPath, buildLog);

            // 获取Git提交信息
            var (commitId, commitLog, commitTime) = GetCommitInfoIfGitRepo(repoDir);

            // 打包（返回 zip 文件路径和版本号，确保版本号在打包和上传之间一致）
            var (zipFile, version) = ZipOutput(cmd, publishDir, workDir, buildLog);

            // 上传到星尘
            UploadBuildPackageSync(cmd, zipFile, version, commitId, commitLog, commitTime);

            XTrace.WriteLine("========== Build task completed ==========");

            // 上报部署完成事件
            ReportDeployEvent(cmd.DeployName, "done", $"部署代理处理完成（应用：{cmd.DeployName}，产物：{zipFile}）");

            return buildLog.ToString();
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Build task failed: {0}", ex.Message);
            XTrace.WriteException(ex);

            // 将累计构建日志带入异常，便于服务端回传（仅保留尾部，避免消息过大）
            var tail = buildLog.Length > 6000 ? buildLog.ToString(buildLog.Length - 6000, 6000) : buildLog.ToString();

            // 上报失败事件（含日志），服务端写入 AppDeployHistory
            ReportDeployEvent(cmd.DeployName, "error", tail);

            throw new Exception(L.T("Build task failed: {0}\n---- accumulated build log ----\n{1}", ex.Message, tail));
        }
        finally
        {
            CleanupSshKey(sshKeyFile);
            CleanupWorkDir(workDir);
        }
    }

    /// <summary>解析编译命令参数</summary>
    private static CompileCommand ParseCompileCommand(String args)
    {
        var cmd = args.ToJsonEntity<CompileCommand>();
        if (cmd == null) throw new ArgumentNullException(nameof(args), L.T("Unable to parse the build command arguments"));
        return cmd;
    }

    /// <summary>解密仓库密码并用凭据组装克隆 URL</summary>
    private static String BuildCloneUrl(CompileCommand cmd)
    {
        var cloneUrl = cmd.Repository;
        if (!cmd.RepoPassword.IsNullOrEmpty())
        {
            try
            {
                var set = StarSetting.Current;
                var pass = Encoding.UTF8.GetBytes(set.Secret);
                using var aes = System.Security.Cryptography.Aes.Create();
                var plainPassword = Encoding.UTF8.GetString(aes.Decrypt(cmd.RepoPassword.ToHex(), pass, System.Security.Cryptography.CipherMode.CBC, System.Security.Cryptography.PaddingMode.PKCS7));

                cloneUrl = Stardust.Models.CompileCommand.BuildCloneUrl(cmd.Repository, cmd.RepoUserName, cmd.DeployKey, plainPassword);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("Failed to decrypt the repository password: {0}", ex.Message);
            }
        }
        else if (!cmd.RepoUserName.IsNullOrEmpty())
        {
            // 有用户名无密码，也尝试组装 URL（可能用于 SSH 格式）
            cloneUrl = Stardust.Models.CompileCommand.BuildCloneUrl(cmd.Repository, cmd.RepoUserName, cmd.DeployKey, null);
        }
        return cloneUrl;
    }

    /// <summary>编译项目或解析输出目录</summary>
    private String BuildOrResolvePublishDir(CompileCommand cmd, String repoDir, String outputPath, StringBuilder buildLog)
    {
        if (cmd.BuildProject)
        {
            var lenBuild = buildLog.Length;
            var publishDir = BuildProject(cmd, repoDir, outputPath, buildLog);
            XTrace.WriteLine("Build completed, output directory: {0}", publishDir);
            ReportDeployEvent(cmd.DeployName, "build", buildLog.ToString(lenBuild, buildLog.Length - lenBuild));
            return publishDir;
        }

        // 不编译时直接使用输出目录
        return Path.Combine(repoDir, outputPath);
    }

    /// <summary>获取 Git 提交信息（仅 .git 目录存在时）</summary>
    private (String commitId, String commitLog, String commitTime) GetCommitInfoIfGitRepo(String repoDir)
    {
        if (!Directory.Exists(Path.Combine(repoDir, ".git"))) return ("", "", "");

        var (commitId, commitLog, commitTime) = GetGitCommitInfo(repoDir);
        if (!commitId.IsNullOrEmpty())
            XTrace.WriteLine("Commit: {0} {1} {2}", commitId, commitLog, commitTime);

        return (commitId, commitLog, commitTime);
    }

    /// <summary>打包输出目录。返回 zip 文件路径和版本号，版本号取自文件名中的时间戳，供上传复用</summary>
    private (String zipFile, String version) ZipOutput(CompileCommand cmd, String publishDir, String workDir, StringBuilder buildLog)
    {
        if (!cmd.PackageOutput) return ("", "");

        if (!Directory.Exists(publishDir))
            throw new DirectoryNotFoundException(L.T("Artifact directory does not exist: {0}", publishDir));

        var packageName = cmd.DeployName ?? "app";
        // 如果没有临时工作目录，则在源代码目录上级创建临时目录存放zip
        var zipDir = workDir.IsNullOrEmpty() ? Path.GetTempPath() : workDir;
        var now = DateTime.Now;
        var timestamp = now.ToString("yyyyMMdd-HHmmss");
        var zipFile = Path.Combine(zipDir, $"{packageName}-{timestamp}.zip");
        ZipCompress(publishDir, zipFile, cmd.PackageFilters);
        XTrace.WriteLine("Packaging completed: {0} ({1:n0} bytes)", zipFile, new FileInfo(zipFile).Length);
        ReportDeployEvent(cmd.DeployName, "package", $"打包完成：{zipFile} ({new FileInfo(zipFile).Length:n0} bytes)");

        var version = $"v{now:yyyyMMdd-HHmmss}";
        return (zipFile, version);
    }

    /// <summary>上传包文件到星尘平台（同步版本，避免 async 回调死锁）</summary>
    private void UploadBuildPackageSync(CompileCommand cmd, String zipFile, String version, String commitId, String commitLog, String commitTime)
    {
        if (!cmd.UploadPackage || zipFile.IsNullOrEmpty()) return;

        if (cmd.DeployName.IsNullOrEmpty())
            throw new InvalidOperationException(L.T("The application deployment set name is not specified; cannot upload."));

        UploadPackageAsync(_client.Server, cmd.DeployName, zipFile, version, commitId, commitLog, commitTime)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        XTrace.WriteLine("Upload succeeded: {0}", zipFile);
        ReportDeployEvent(cmd.DeployName, "upload", $"上传成功：{zipFile}");
    }

    /// <summary>清理临时工作目录（本地源代码目录不清理）</summary>
    private static void CleanupWorkDir(String workDir)
    {
        if (!workDir.IsNullOrEmpty() && Directory.Exists(workDir))
        {
            try { Directory.Delete(workDir, true); } catch { }
        }
    }

    /// <summary>上报部署事件到服务端，复用 PostEvents 对 ServiceController 事件的现成写入逻辑</summary>
    /// <remarks>
    /// 事件 Name 必须为 "ServiceController"（写死，原因见下方 WriteEvent 调用处注释）：服务端 NodeService.PostEvents
    /// 仅对 Name=="ServiceController" 写 AppDeployHistory，且该 Name 即为历史行的 Action 列。改它则不落库。
    /// Type 格式为“部署集名-步骤”，仅取前缀解析 appId、不持久化；动作区分靠 Type 前缀与 Remark 末尾的 [动作] 标签。
    /// </remarks>
    private void ReportDeployEvent(String? deployName, String step, String log)
    {
        if (_client == null) return;

        // Type 格式“部署集名-步骤”，步骤非 error 即成功；服务端 PostEvents 复用 ServiceController 分支写入 AppDeployHistory
        var type = $"{deployName}-{step}";

        // Remark 上限 2000。服务端不持久化 Type（仅取前缀解析 appId），故动作标签写入 Remark 才能落库区分。
        // 标签置于末尾，确保尾部截断后仍保留，使 AppDeployHistory 每行都能看出是哪个动作（gitclone/build…）。
        const Int32 maxRemark = 2000;
        var label = $"\n[{step}]";
        var remark = (log ?? "") + label;
        if (remark.Length > maxRemark)
        {
            var keep = maxRemark - 30 - label.Length;
            remark = L.T("...(the log is too long and was truncated; the last {0} characters are kept)\n", keep) + log.Substring(log.Length - keep) + label;
            if (remark.Length > maxRemark) remark = remark.Substring(remark.Length - maxRemark);
        }

        try
        {
            // 【必须写死 "ServiceController"，切勿改成动作名或其它值】
            // 原因：服务端 NodeService.PostEvents 仅当 EventModel.Name == "ServiceController" 时才写入 AppDeployHistory（部署历史）；
            //       且该 Name 会直接成为历史行的 Action 列（部署历史.cs:51 Action 上限50）。
            // 若改成其它值（如 "gitclone"/"build"），服务端判定非 ServiceController，将不落库部署历史，
            //       导致“服务端看不到部署情况”，与需求背道而驰。
            // 此名称为既有服务端契约，改动需同步修改 Stardust.Server（当前按需求只改 Agent、不动 Server），故保持不变。
            // 动作区分不靠 Name，而靠 Type（deployName-动作，仅取前缀解析 appId）与 Remark 末尾的 [动作] 标签。
            ((IEventProvider)_client).WriteEvent(type, "ServiceController", remark);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Failed to report the deployment event: {0}", ex.Message);
        }
    }

    /// <summary>编译项目</summary>
    /// <param name="cmd">编译命令参数</param>
    /// <param name="repoDir">源代码目录</param>
    /// <param name="outputPath">输出目录名</param>
    /// <returns>编译输出的绝对路径</returns>
    private String BuildProject(CompileCommand cmd, String repoDir, String outputPath, StringBuilder? log = null)
    {
        var publishDir = Path.Combine(repoDir, outputPath);

        // 编译前先清空输出目录，避免上次编译产物影响
        if (Directory.Exists(publishDir))
        {
            XTrace.WriteLine("Clearing output directory: {0}", publishDir);
            Directory.Delete(publishDir, true);
        }

        // ProjectKind: 1=DotNet, 2=MSBuild, 99=Custom
        switch (cmd.ProjectKind)
        {
            case 1: // DotNet
                {
                    var projectPath = cmd.ProjectPath.IsNullOrEmpty() ? repoDir : Path.Combine(repoDir, cmd.ProjectPath);
                    var arguments = $"publish \"{projectPath}\" -o \"{publishDir}\"";
                    if (!cmd.BuildArgs.IsNullOrEmpty()) arguments += $" {cmd.BuildArgs}";

                    XTrace.WriteLine("dotnet {0}", arguments);
                    ExecuteProcess("dotnet", arguments, repoDir, log);
                }
                break;
            case 2: // MSBuild
                {
                    var projectPath = cmd.ProjectPath.IsNullOrEmpty() ? repoDir : Path.Combine(repoDir, cmd.ProjectPath);
                    var arguments = $"\"{projectPath}\" /p:OutputPath=\"{publishDir}\"";
                    if (!cmd.BuildArgs.IsNullOrEmpty()) arguments += $" {cmd.BuildArgs}";

                    XTrace.WriteLine("msbuild {0}", arguments);
                    ExecuteProcess("msbuild", arguments, repoDir, log);
                }
                break;
            case 99: // Custom - 自定义项目，执行项目 build 文件夹下的 build.sh 脚本
                {
                    // 自定义构建脚本，默认使用 {项目根目录}/build/build.sh
                    var buildScript = cmd.ProjectPath.IsNullOrEmpty()
                        ? Path.Combine(repoDir, "build", "build.sh")
                        : Path.Combine(repoDir, cmd.ProjectPath);

                    if (!File.Exists(buildScript))
                        throw new FileNotFoundException(L.T("Build script does not exist: {0}", buildScript));

                    ExecuteBuildScript(buildScript, repoDir, log);
                }
                break;
            default:
                {
                    // 默认按 dotnet 处理
                    var projectPath = cmd.ProjectPath.IsNullOrEmpty() ? repoDir : Path.Combine(repoDir, cmd.ProjectPath);
                    var arguments = $"publish \"{projectPath}\" -o \"{publishDir}\"";
                    if (!cmd.BuildArgs.IsNullOrEmpty()) arguments += $" {cmd.BuildArgs}";

                    XTrace.WriteLine("dotnet {0}", arguments);
                    ExecuteProcess("dotnet", arguments, repoDir, log);
                }
                break;
        }

        return publishDir;
    }

    /// <summary>执行通用进程</summary>
    private void ExecuteProcess(String fileName, String arguments, String? workingDirectory, StringBuilder? log = null, String? sshKeyFile = null)
    {
        var psi = CreateProcessStartInfo(fileName, arguments, workingDirectory, sshKeyFile);

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var outputSb = new StringBuilder();
        var errorSb = new StringBuilder();

        p.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                XTrace.WriteLine(e.Data);
                outputSb.AppendLine(e.Data);
            }
        };
        p.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                XTrace.WriteLine(e.Data);
                errorSb.AppendLine(e.Data);
            }
        };

        if (!p.Start()) throw new Exception(L.T("Unable to start process: {0}", fileName));
        p.StandardInput.Close();  // 关键：关闭标准输入，防止阻塞
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        // 超时时间设置为 10 分钟（600,000 毫秒），避免长时间挂起
        var timeout = 600_000;
        if (!p.WaitForExit(timeout))
        {
            try { p.Kill(); } catch { }
            log?.AppendLine($"{fileName} 执行超时，已终止（{timeout} ms）");
            log?.Append(outputSb);
            log?.Append(errorSb);
            throw new Exception(L.T("{0} execution timed out and was terminated ({1} ms)\n{2}\n{3}", fileName, timeout, outputSb, errorSb));
        }

        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            log?.AppendLine($"{fileName} 执行失败，退出码：{p.ExitCode}");
            log?.Append(outputSb);
            log?.Append(errorSb);
            throw new Exception(L.T("{0} execution failed with exit code {1}\n{2}\n{3}", fileName, p.ExitCode, outputSb, errorSb));
        }

        // 记录成功输出，供上层回传构建日志
        log?.Append(outputSb);
        log?.Append(errorSb);
    }

    /// <summary>Git 克隆仓库</summary>
    private void GitClone(String repoUrl, String branch, String targetPath, StringBuilder? log = null, String? sshKeyFile = null)
    {
        var safeUrl = Stardust.Models.CompileCommand.RedactUrlForLog(repoUrl);
        XTrace.WriteLine("Cloning repository {0}, branch: {1}", safeUrl, branch);

        var args = $"clone -b {branch} --depth 1 {repoUrl} \"{targetPath}\"";
        XTrace.WriteLine("git {0}", $"clone -b {branch} --depth 1 {safeUrl} \"{targetPath}\"");
        ExecuteProcess("git", args, null, log, sshKeyFile);

        XTrace.WriteLine("Git clone succeeded");
    }

    /// <summary>Git 拉取最新代码</summary>
    /// <param name="repoDir">本地仓库目录</param>
    /// <param name="branch">分支名称</param>
    private void GitPull(String repoDir, String? branch, StringBuilder? log = null, String? sshKeyFile = null)
    {
        XTrace.WriteLine("Pulling code: {0}", repoDir);

        // 如果指定了分支则先切换
        if (!branch.IsNullOrEmpty())
        {
            ExecuteProcess("git", $"checkout {branch}", repoDir, log, sshKeyFile);
        }

        ExecuteProcess("git", "pull", repoDir, log, sshKeyFile);

        XTrace.WriteLine("Git pull succeeded");
    }

    /// <summary>执行构建脚本。自动判断执行环境，Linux直接使用bash，Windows使用Git Bash</summary>
    private void ExecuteBuildScript(String scriptPath, String workingDirectory, StringBuilder? log = null)
    {
        XTrace.WriteLine("Executing build script: {0}", scriptPath);

        if (OperatingSystem.IsWindows())
        {
            // Windows 环境，使用 Git Bash 执行
            var gitBash = FindGitBash();
            if (gitBash.IsNullOrEmpty())
                throw new Exception(L.T("Git Bash was not found; install Git for Windows."));

            XTrace.WriteLine("Using Git Bash: {0}", gitBash);
            ExecuteProcess(gitBash, $"-l -c \"bash '{scriptPath}'\"", workingDirectory, log);
        }
        else
        {
            // Linux/macOS 环境，直接使用 bash
            XTrace.WriteLine("Executing the script with bash");

            // 确保脚本有执行权限
            ExecuteProcess("chmod", $"+x \"{scriptPath}\"", workingDirectory, log);
            ExecuteProcess("bash", $"\"{scriptPath}\"", workingDirectory, log);
        }

        XTrace.WriteLine("Build script executed successfully");
    }

    /// <summary>查找 Git Bash 路径</summary>
    private String? FindGitBash()
    {
        var paths = new[]
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            @"D:\Program Files\Git\bin\bash.exe",
            @"D:\Program Files (x86)\Git\bin\bash.exe"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!pathEnv.IsNullOrEmpty())
        {
            foreach (var dir in pathEnv.Split(';'))
            {
                var bash = Path.Combine(dir.Trim(), "bash.exe");
                if (File.Exists(bash)) return bash;
            }
        }

        return null;
    }

    /// <summary>压缩目录</summary>
    /// <param name="sourceDir">源目录</param>
    /// <param name="zipFile">目标zip文件</param>
    /// <param name="filters">过滤器，支持通配符，多项分号隔开</param>
    private void ZipCompress(String sourceDir, String zipFile, String? filters = null)
    {
        XTrace.WriteLine("Starting compression: {0} -> {1}", sourceDir, zipFile);

        if (File.Exists(zipFile)) File.Delete(zipFile);

        using var zip = System.IO.Compression.ZipFile.Open(zipFile, System.IO.Compression.ZipArchiveMode.Create);

        // 获取待打包的文件列表
        var files = GetFilesToPack(sourceDir, filters);
        foreach (var file in files)
        {
            // ZIP规范要求使用正斜杠作为目录分隔符，Windows反斜杠在Linux解压时会导致路径错乱
            var entryName = file[sourceDir.Length..].TrimStart('/', '\\').Replace('\\', '/');
            zip.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.Optimal);
        }
    }

    /// <summary>根据过滤器获取待打包文件</summary>
    private String[] GetFilesToPack(String sourceDir, String? filters)
    {
        if (filters.IsNullOrEmpty())
            return Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        var list = new List<String>();
        foreach (var pattern in filters.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = pattern.Trim();
            if (!p.IsNullOrEmpty())
                list.AddRange(Directory.GetFiles(sourceDir, p, SearchOption.AllDirectories));
        }

        return list.Distinct().ToArray();
    }

    /// <summary>获取Git最新提交信息</summary>
    /// <param name="repoDir">仓库目录</param>
    /// <returns>提交标识、提交记录、提交时间</returns>
    private (String commitId, String commitLog, String commitTime) GetGitCommitInfo(String repoDir)
    {
        try
        {
            var psi = CreateProcessStartInfo("git", "log -1 --format=%H||%s||%ai", repoDir);
            using var p = Process.Start(psi);
            if (p == null) return ("", "", "");

            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(30_000);

            if (p.ExitCode != 0 || output.IsNullOrEmpty()) return ("", "", "");

            var parts = output.Split("||");
            if (parts.Length < 3) return ("", "", "");

            return (parts[0], parts[1], parts[2]);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Failed to get Git commit information: {0}", ex.Message);
            return ("", "", "");
        }
    }

    /// <summary>尝试备份现有仓库并重新克隆（失败则尝试还原备份）</summary>
    private void TryRecoverAndReclone(String repoUrl, String branch, String repoDir, String? sshKeyFile = null)
    {
        XTrace.WriteLine("Attempting to back up and re-pull the repository: {0}", repoDir);

        var backupDir = "";
        try
        {
            if (Directory.Exists(repoDir))
            {
                backupDir = repoDir + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                Directory.Move(repoDir, backupDir);
                XTrace.WriteLine("Original repository backed up to: {0}", backupDir);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Failed to back up the original repository: {0}", ex.Message);
            XTrace.WriteException(ex);
        }

        try
        {
            GitClone(repoUrl, branch ?? "main", repoDir, null, sshKeyFile);
            XTrace.WriteLine("Re-clone succeeded: {0}", repoDir);

            if (!string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
            {
                try
                {
                    Directory.Delete(backupDir, true);
                    XTrace.WriteLine("Backup deleted: {0}", backupDir);
                }
                catch (Exception ex)
                {
                    XTrace.WriteLine("Failed to delete the backup: {0}", ex.Message);
                    XTrace.WriteException(ex);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Re-clone failed: {0}", ex.Message);
            XTrace.WriteException(ex);

            // 尝试还原备份
            try
            {
                if (!string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
                {
                    if (Directory.Exists(repoDir))
                    {
                        try { Directory.Delete(repoDir, true); } catch { }
                    }
                    Directory.Move(backupDir, repoDir);
                    XTrace.WriteLine("Backup restored to: {0}", repoDir);
                }
            }
            catch (Exception ex2)
            {
                XTrace.WriteLine("Failed to restore the backup: {0}", ex2.Message);
                XTrace.WriteException(ex2);
            }

            throw;
        }
    }

    /// <summary>确保本地仓库就绪。根据目录存在性和 .git 目录判断 clone 还是 pull，返回仓库目录</summary>
    private String EnsureAndSyncRepo(CompileCommand cmd, String cloneUrl, StringBuilder buildLog, String? sshKeyFile, ref String workDir)
    {
        // 无 SourcePath 时克隆到临时目录
        if (cmd.SourcePath.IsNullOrEmpty())
        {
            if (!cmd.PullCode || cmd.Repository.IsNullOrEmpty())
                throw new InvalidOperationException(L.T("Neither a source directory nor a code repository address was specified."));

            workDir = Path.Combine(Path.GetTempPath(), $"stardust-build-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workDir);
            XTrace.WriteLine("Working directory: {0}", workDir);

            var repoDir = Path.Combine(workDir, "repo");
            CloneWithRetry(cloneUrl, cmd.Branch ?? "main", repoDir, buildLog, sshKeyFile, cmd.DeployName);
            return repoDir;
        }

        var repo = cmd.SourcePath;
        XTrace.WriteLine("Using local source directory: {0}", repo);

        // 目录不存在或存在但 .git 不存在，都走 clone
        var needClone = !Directory.Exists(repo) || !Directory.Exists(Path.Combine(repo, ".git"));
        if (needClone)
        {
            if (!Directory.Exists(repo))
                Directory.CreateDirectory(repo);

            CloneWithRetry(cloneUrl, cmd.Branch ?? "main", repo, buildLog, sshKeyFile, cmd.DeployName);
        }
        else if (cmd.PullCode)
        {
            // 目录和 .git 都存在，拉取最新
            PullWithRetry(repo, cmd.Branch, buildLog, sshKeyFile, cmd.DeployName, cmd.Repository, cloneUrl);
        }
        // else: 目录和 .git 都在，且不需要 pull → 啥也不做

        return repo;
    }

    /// <summary>克隆仓库，失败时备份重试</summary>
    private void CloneWithRetry(String repoUrl, String branch, String repoDir, StringBuilder buildLog, String? sshKeyFile, String? deployName)
    {
        try
        {
            var lenGit = buildLog.Length;
            GitClone(repoUrl, branch, repoDir, buildLog, sshKeyFile);
            XTrace.WriteLine("Code pull completed: {0}", repoDir);
            ReportDeployEvent(deployName, "gitclone", buildLog.ToString(lenGit, buildLog.Length - lenGit));
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Git clone failed: {0}", ex.Message);
            XTrace.WriteException(ex);
            RecoverAndReclone(repoUrl, branch, repoDir, sshKeyFile);
        }
    }

    /// <summary>拉取仓库，失败时备份并重新克隆</summary>
    private void PullWithRetry(String repoDir, String? branch, StringBuilder buildLog, String? sshKeyFile, String? deployName, String? cmdRepository, String cloneUrl)
    {
        try
        {
            var lenPull = buildLog.Length;
            GitPull(repoDir, branch, buildLog, sshKeyFile);
            XTrace.WriteLine("Code pull completed");
            ReportDeployEvent(deployName, "gitpull", buildLog.ToString(lenPull, buildLog.Length - lenPull));
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Git pull failed: {0}", ex.Message);
            XTrace.WriteException(ex);
            var repoUrl = cmdRepository.IsNullOrEmpty() ? cloneUrl : cmdRepository;
            RecoverAndReclone(repoUrl, branch ?? "main", repoDir, sshKeyFile);
        }
    }

    /// <summary>备份现有仓库并重新克隆，失败则尝试还原备份</summary>
    private void RecoverAndReclone(String repoUrl, String branch, String repoDir, String? sshKeyFile = null)
    {
        var safeUrl = Stardust.Models.CompileCommand.RedactUrlForLog(repoUrl);
        XTrace.WriteLine("Attempting to back up and re-pull the repository: {0}", safeUrl);

        var backupDir = "";
        try
        {
            if (Directory.Exists(repoDir))
            {
                backupDir = repoDir + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                Directory.Move(repoDir, backupDir);
                XTrace.WriteLine("Original repository backed up to: {0}", backupDir);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Failed to back up the original repository: {0}", ex.Message);
            XTrace.WriteException(ex);
        }

        try
        {
            GitClone(repoUrl, branch ?? "main", repoDir, null, sshKeyFile);
            XTrace.WriteLine("Re-clone succeeded: {0}", repoDir);

            if (!string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
            {
                try
                {
                    Directory.Delete(backupDir, true);
                    XTrace.WriteLine("Backup deleted: {0}", backupDir);
                }
                catch (Exception ex)
                {
                    XTrace.WriteLine("Failed to delete the backup: {0}", ex.Message);
                    XTrace.WriteException(ex);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Re-clone failed: {0}", ex.Message);
            XTrace.WriteException(ex);

            // 尝试还原备份
            try
            {
                if (!string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
                {
                    if (Directory.Exists(repoDir))
                    {
                        try { Directory.Delete(repoDir, true); } catch { }
                    }
                    Directory.Move(backupDir, repoDir);
                    XTrace.WriteLine("Backup restored to: {0}", repoDir);
                }
            }
            catch (Exception ex2)
            {
                XTrace.WriteLine("Failed to restore the backup: {0}", ex2.Message);
                XTrace.WriteException(ex2);
            }

            throw;
        }
    }

    /// <summary>上传包文件到星尘平台。调用Deploy/UploadBuildFile接口创建应用版本</summary>
    /// <param name="server">服务器地址</param>
    /// <param name="deployName">应用部署集名称</param>
    /// <param name="packagePath">包文件路径</param>
    /// <param name="version">版本号，需与 zip 打包时间一致</param>
    /// <param name="commitId">提交标识</param>
    /// <param name="commitLog">提交记录</param>
    /// <param name="commitTime">提交时间</param>
    private async Task UploadPackageAsync(String server, String deployName, String packagePath, String version, String? commitId = null, String? commitLog = null, String? commitTime = null)
    {
        XTrace.WriteLine("Uploading package file: {0}", packagePath);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(server.TrimEnd('/')),
            Timeout = TimeSpan.FromMinutes(10)
        };

        // 使用StarClient的登录令牌进行认证，避免在请求头中传递明文密钥
        var token = _client.Client?.Token;
        if (!token.IsNullOrEmpty())
            httpClient.DefaultRequestHeaders.Add("X-Token", token);

        // version 由 ZipOutput 传入，确保与 zip 打包时间戳一致
        var uploadUrl = $"/Deploy/UploadBuildFile?deployName={Uri.EscapeDataString(deployName)}&version={Uri.EscapeDataString(version)}";
        if (!commitId.IsNullOrEmpty()) uploadUrl += $"&commitId={Uri.EscapeDataString(commitId)}";
        if (!commitLog.IsNullOrEmpty()) uploadUrl += $"&commitLog={Uri.EscapeDataString(commitLog)}";
        if (!commitTime.IsNullOrEmpty()) uploadUrl += $"&commitTime={Uri.EscapeDataString(commitTime)}";
        XTrace.WriteLine("Upload URL: {0}{1}", server, uploadUrl);

        const Int32 maxRetries = 3;
        Exception lastError = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await PostUploadAsync(httpClient, uploadUrl, packagePath).ConfigureAwait(false);

                // 检测 401 认证失效
                if (result.StatusCode == 401 || IsUnauthorizedBody(result.Body))
                {
                    XTrace.WriteLine("Upload received 401 (authentication expired); attempting to log in again");
                    var newToken = await EnsureLoginAsync().ConfigureAwait(false);
                    // 更新 HttpClient 的 X-Token 头
                    httpClient.DefaultRequestHeaders.Remove("X-Token");
                    if (!newToken.IsNullOrEmpty())
                        httpClient.DefaultRequestHeaders.Add("X-Token", newToken);
                    // 401 不消耗重试配额，立即重试
                    attempt--;
                    continue;
                }

                // 非 2xx 状态码
                if (result.StatusCode < 200 || result.StatusCode >= 300)
                {
                    throw new Exception(L.T("Upload failed: HTTP {0} - {1}", result.StatusCode, result.Body));
                }

                // 检查响应体中的应用层错误码（ApiFilter 返回 HTTP 200，错误信息在 body 中）
                if (!result.Body.IsNullOrEmpty())
                {
                    try
                    {
                        var uploadResult = result.Body.ToJsonEntity<UploadResult>();
                        if (uploadResult != null && uploadResult.Code != 0 && uploadResult.Code == 401)
                        {
                            XTrace.WriteLine("Response body code=401; attempting to log in again");
                            var newToken = await EnsureLoginAsync().ConfigureAwait(false);
                            httpClient.DefaultRequestHeaders.Remove("X-Token");
                            if (!newToken.IsNullOrEmpty())
                                httpClient.DefaultRequestHeaders.Add("X-Token", newToken);
                            attempt--;
                            continue;
                        }
                        if (uploadResult != null && uploadResult.Code != 0)
                            throw new Exception(L.T("Upload failed: {0} - {1}", uploadResult.Code, uploadResult.Message));
                    }
                    catch (Exception ex) when (!ex.Message.StartsWith("上传失败："))
                    {
                        // 解析失败视为成功（响应可能不是 UploadResult 结构）
                    }
                }

                XTrace.WriteLine("Upload succeeded: {0}", result.Body);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or System.Net.Sockets.SocketException or TaskCanceledException)
            {
                lastError = ex;
                if (attempt >= maxRetries)
                {
                    XTrace.WriteException(ex);
                    throw new Exception(L.T("Upload failed after {0} retries: {1}", maxRetries, ex.Message), ex);
                }

                var delay = (Int32)Math.Pow(2, attempt) * 1000; // 1s, 2s, 4s
                XTrace.WriteLine("Retry {0}, reason: {1}, waiting {2} second(s)", attempt + 1, ex.Message, delay / 1000);
                await Task.Delay(delay).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 非网络异常（如业务错误、重新登录失败）直接抛出
                XTrace.WriteException(ex);
                throw;
            }
        }

        if (lastError != null) throw new Exception(L.T("Upload failed after {0} retries: {1}", maxRetries, lastError.Message), lastError);
    }

    /// <summary>执行单次上传请求。封装打开文件流→构建内容→POST→解析响应的完整流程，供重试调用</summary>
    private async Task<UploadAttempt> PostUploadAsync(HttpClient httpClient, String uploadUrl, String packagePath)
    {
        // 使用流式上传，避免大文件内存峰值
        await using var fileStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", Path.GetFileName(packagePath));

        var response = await httpClient.PostAsync(uploadUrl, content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        return new UploadAttempt { StatusCode = (Int32)response.StatusCode, Body = body };
    }

    /// <summary>重新登录获取新 token。在 401 认证失效时调用</summary>
    private async Task<String?> EnsureLoginAsync()
    {
        XTrace.WriteLine("Token expired; retrying upload after logging in again");
        try
        {
            await _client.Login("上传认证失效重试").ConfigureAwait(false);
            var newToken = _client.Client?.Token;
            if (newToken.IsNullOrEmpty()) throw new Exception(L.T("Token is still empty after logging in again"));
            return newToken;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            throw new Exception(L.T("Re-login failed: {0}", ex.Message), ex);
        }
    }

    private static Boolean IsUnauthorizedBody(String body)
    {
        if (body.IsNullOrEmpty()) return false;
        try
        {
            var r = body.ToJsonEntity<UploadResult>();
            return r != null && r.Code == 401;
        }
        catch { return false; }
    }

    /// <summary>创建进程启动信息</summary>
    private ProcessStartInfo CreateProcessStartInfo(String fileName, String arguments, String? workingDirectory = null, String? sshKeyFile = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = String.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        // 服务模式运行在 Session 0，没有桌面/TTY，禁止 Git 交互式认证
        // GIT_TERMINAL_PROMPT=0：禁止 Git 尝试从 stdin 读取用户名密码
        // GCM_INTERACTIVE=Never：禁止 Git Credential Manager 弹出认证窗口
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GCM_INTERACTIVE"] = "Never";

        if (!sshKeyFile.IsNullOrEmpty())
        {
            // 构建 SSH 命令：默认启用严格主机密钥校验，可通过 StarSetting 的 DisableSshStrictChecking 关闭
            var strictChecking = !StarSetting.Current.DisableSshStrictChecking;
            if (strictChecking)
            {
                // 严格模式：使用专用 known_hosts 文件
                var knownHostsFile = GetSshKnownHostsFile();
                // accept-new 在 OpenSSH 7.3+ 支持，旧版本不识别会直接失败
                // 检测版本并选择合适的策略：accept-new(7.3+) -> yes(旧版本)
                var strictMode = GetSshStrictHostKeyCheckingMode();
                psi.Environment["GIT_SSH_COMMAND"] = $"ssh -i \"{sshKeyFile}\" -o StrictHostKeyChecking={strictMode} -o UserKnownHostsFile=\"{knownHostsFile}\"";
            }
            else
            {
                // 兼容模式：跳过主机密钥校验（MITM 风险，仅在明确知晓风险时使用）
                // 注意：Windows 上 /dev/null 不存在，需使用 NUL 或临时文件
                var nullDevice = Environment.OSVersion.Platform == PlatformID.Unix ? "/dev/null" : "NUL";
                psi.Environment["GIT_SSH_COMMAND"] = $"ssh -i \"{sshKeyFile}\" -o StrictHostKeyChecking=no -o UserKnownHostsFile={nullDevice}";
            }
        }

        return psi;
    }

    /// <summary>获取 SSH known_hosts 文件路径</summary>
    private static String GetSshKnownHostsFile()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, ".ssh");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, "known_hosts");
    }

    /// <summary>获取 SSH 严格主机密钥校验模式</summary>
    private static String GetSshStrictHostKeyCheckingMode()
    {
        // accept-new 在 OpenSSH 7.3+ 支持，自动接受新主机但拒绝更改的主机
        // 旧版本不识别会直接失败，此时回退到 yes（接受新主机）
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = "-V",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return "yes";
            var output = process.StandardError.ReadToEnd();
            process.WaitForExit(1000);

            // OpenSSH 版本格式: "OpenSSH_7.3p1 ..." 或 "OpenSSH_8.0p1 ..."
            var match = System.Text.RegularExpressions.Regex.Match(output, @"OpenSSH_(\d+)\.(\d+)");
            if (match.Success)
            {
                var major = int.Parse(match.Groups[1].Value);
                var minor = int.Parse(match.Groups[2].Value);
                // 7.3+ 支持 accept-new
                if (major > 7 || (major == 7 && minor >= 3)) return "accept-new";
            }
        }
        catch { }
        return "yes";
    }
    #endregion

    private class UploadResult
    {
        public Int32 Code { get; set; }
        public String? Message { get; set; }
    }

    private class UploadAttempt
    {
        public Int32 StatusCode { get; set; }
        public String Body { get; set; } = "";
    }
}
