using System.Diagnostics;
using System.IO.Compression;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting.Clients;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace DeployAgent;

/// <summary>
/// 部署工作服务
/// </summary>
public class DeployWorker(StarFactory factory) : IHostedService
{
    private StarClient _client = null!;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        XTrace.WriteLine("开始 Deploy 客户端");

        var set = DeploySetting.Current;

        var client = new StarClient(factory.Server)
        {
            Name = "Deploy",
            Code = set.Code,
            Secret = set.Secret,
            ProductCode = "StarDeploy",
            Setting = set,

            Tracer = factory.Tracer,
            Log = XTrace.Log,
        };

        // 禁用客户端特性
        client.Features &= ~Features.Upgrade;

        client.Open();

        Host.RegisterExit(() => client.Logout("ApplicationExit"));

        _client = client;

        // 注册编译命令
        client.RegisterCommand("deploy/compile", OnCompile);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.TryDispose();

        return Task.CompletedTask;
    }

    /// <summary>处理编译命令</summary>
    private String OnCompile(String args)
    {
        if (args.IsNullOrEmpty()) throw new ArgumentNullException(nameof(args));

        var cmd = args.ToJsonEntity<CompileCommand>();
        if (cmd == null) throw new ArgumentNullException(nameof(args), "无法解析编译命令参数");

        XTrace.WriteLine("========== 开始编译任务 ==========");
        XTrace.WriteLine("仓库：{0}", cmd.Repository);
        XTrace.WriteLine("分支：{0}", cmd.Branch ?? "main");

        var workDir = "";
        try
        {
            var outputPath = cmd.OutputPath;
            if (outputPath.IsNullOrEmpty()) outputPath = "publish";

            // 确定源代码目录
            var repoDir = "";
            if (!cmd.SourcePath.IsNullOrEmpty())
            {
                // 先判断目录是否存在，不存在则创建并执行clone，否则pull

                // 使用本地已有的源代码目录
                repoDir = cmd.SourcePath;
                XTrace.WriteLine("使用本地源代码目录：{0}", repoDir);

                if (!Directory.Exists(cmd.SourcePath))
                {
                    Directory.CreateDirectory(cmd.SourcePath);
                    GitClone(cmd.Repository, cmd.Branch ?? "main", repoDir);
                    XTrace.WriteLine("代码拉取完成：{0}", repoDir);
                }
                // 拉取最新代码
                else if (cmd.PullCode)
                {
                    GitPull(repoDir, cmd.Branch);
                    XTrace.WriteLine("代码拉取完成");
                }
            }
            else if (cmd.PullCode && !cmd.Repository.IsNullOrEmpty())
            {
                // 克隆远程仓库
                workDir = Path.Combine(Path.GetTempPath(), $"stardust-build-{Guid.NewGuid():N}");
                Directory.CreateDirectory(workDir);
                XTrace.WriteLine("工作目录：{0}", workDir);

                repoDir = Path.Combine(workDir, "repo");
                GitClone(cmd.Repository, cmd.Branch ?? "main", repoDir);
                XTrace.WriteLine("代码拉取完成：{0}", repoDir);
            }
            else
            {
                throw new InvalidOperationException("未指定源代码目录或代码仓库地址");
            }

            // 编译项目
            var publishDir = "";
            if (cmd.BuildProject)
            {
                publishDir = BuildProject(cmd, repoDir, outputPath);
                XTrace.WriteLine("编译完成，输出目录：{0}", publishDir);
            }
            else
            {
                // 不编译时直接使用输出目录
                publishDir = Path.Combine(repoDir, outputPath);
            }

            // 获取Git提交信息
            var commitId = "";
            var commitLog = "";
            var commitTime = "";
            if (Directory.Exists(Path.Combine(repoDir, ".git")))
            {
                (commitId, commitLog, commitTime) = GetGitCommitInfo(repoDir);
                if (!commitId.IsNullOrEmpty())
                    XTrace.WriteLine("提交：{0} {1} {2}", commitId, commitLog, commitTime);
            }

            // 打包
            var zipFile = "";
            if (cmd.PackageOutput)
            {
                if (!Directory.Exists(publishDir))
                    throw new DirectoryNotFoundException($"产物目录不存在：{publishDir}");

                var packageName = cmd.DeployName ?? "app";
                // 如果没有临时工作目录，则在源代码目录上级创建临时目录存放zip
                var zipDir = workDir.IsNullOrEmpty() ? Path.GetTempPath() : workDir;
                zipFile = Path.Combine(zipDir, $"{packageName}-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
                ZipCompress(publishDir, zipFile, cmd.PackageFilters);
                XTrace.WriteLine("打包完成：{0} ({1:n0} bytes)", zipFile, new FileInfo(zipFile).Length);
            }

            // 上传到星尘
            if (cmd.UploadPackage && !zipFile.IsNullOrEmpty())
            {
                if (cmd.DeployName.IsNullOrEmpty())
                    throw new InvalidOperationException("未指定应用部署集名称，无法上传");

                UploadPackage(_client.Server, cmd.DeployName, zipFile, commitId, commitLog, commitTime);
                XTrace.WriteLine("上传成功：{0}", zipFile);
            }

            XTrace.WriteLine("========== 编译任务完成 ==========");

            return zipFile;
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("编译任务失败：{0}", ex.Message);
            XTrace.WriteException(ex);

            throw;
        }
        finally
        {
            // 清理临时工作目录（本地源代码目录不清理）
            if (!workDir.IsNullOrEmpty() && Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, true); } catch { }
            }
        }
    }

    /// <summary>编译项目</summary>
    /// <param name="cmd">编译命令参数</param>
    /// <param name="repoDir">源代码目录</param>
    /// <param name="outputPath">输出目录名</param>
    /// <returns>编译输出的绝对路径</returns>
    private String BuildProject(CompileCommand cmd, String repoDir, String outputPath)
    {
        var publishDir = Path.Combine(repoDir, outputPath);

        // 编译前先清空输出目录，避免上次编译产物影响
        if (Directory.Exists(publishDir))
        {
            XTrace.WriteLine("清空输出目录：{0}", publishDir);
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
                    ExecuteProcess("dotnet", arguments, repoDir);
                }
                break;
            case 2: // MSBuild
                {
                    var projectPath = cmd.ProjectPath.IsNullOrEmpty() ? repoDir : Path.Combine(repoDir, cmd.ProjectPath);
                    var arguments = $"\"{projectPath}\" /p:OutputPath=\"{publishDir}\"";
                    if (!cmd.BuildArgs.IsNullOrEmpty()) arguments += $" {cmd.BuildArgs}";

                    XTrace.WriteLine("msbuild {0}", arguments);
                    ExecuteProcess("msbuild", arguments, repoDir);
                }
                break;
            case 99: // Custom - 自定义项目，执行项目 build 文件夹下的 build.sh 脚本
                {
                    // 自定义构建脚本，默认使用 {项目根目录}/build/build.sh
                    var buildScript = cmd.ProjectPath.IsNullOrEmpty()
                        ? Path.Combine(repoDir, "build", "build.sh")
                        : Path.Combine(repoDir, cmd.ProjectPath);

                    if (!File.Exists(buildScript))
                        throw new FileNotFoundException($"构建脚本不存在：{buildScript}");

                    ExecuteBuildScript(buildScript, repoDir);
                }
                break;
            default:
                {
                    // 默认按 dotnet 处理
                    var projectPath = cmd.ProjectPath.IsNullOrEmpty() ? repoDir : Path.Combine(repoDir, cmd.ProjectPath);
                    var arguments = $"publish \"{projectPath}\" -o \"{publishDir}\"";
                    if (!cmd.BuildArgs.IsNullOrEmpty()) arguments += $" {cmd.BuildArgs}";

                    XTrace.WriteLine("dotnet {0}", arguments);
                    ExecuteProcess("dotnet", arguments, repoDir);
                }
                break;
        }

        return publishDir;
    }

    /// <summary>执行通用进程</summary>
    private void ExecuteProcess(String fileName, String arguments, String workingDirectory)
    {
        var psi = CreateProcessStartInfo(fileName, arguments, workingDirectory);

        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) throw new Exception($"无法启动进程：{fileName}");

        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit(600_000);

        if (!output.IsNullOrEmpty()) XTrace.WriteLine(output);
        if (!error.IsNullOrEmpty()) XTrace.WriteLine(error);

        if (p.ExitCode != 0)
            throw new Exception($"{fileName} 执行失败，退出码：{p.ExitCode}\n{error}");
    }

    /// <summary>Git 克隆仓库</summary>
    private void GitClone(String repoUrl, String branch, String targetPath)
    {
        XTrace.WriteLine("开始克隆仓库：{0} 分支：{1}", repoUrl, branch);

        var psi = CreateProcessStartInfo("git", $"clone -b {branch} --depth 1 {repoUrl} \"{targetPath}\"");

        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) throw new Exception("无法启动 git 进程");

        var error = p.StandardError.ReadToEnd();
        p.WaitForExit(300_000);

        if (p.ExitCode != 0)
            throw new Exception($"Git 克隆失败：{error}");

        XTrace.WriteLine("Git 克隆成功");
    }

    /// <summary>Git 拉取最新代码</summary>
    /// <param name="repoDir">本地仓库目录</param>
    /// <param name="branch">分支名称</param>
    private void GitPull(String repoDir, String? branch)
    {
        XTrace.WriteLine("开始拉取代码：{0}", repoDir);

        // 如果指定了分支则先切换
        if (!branch.IsNullOrEmpty())
        {
            var psiCheckout = CreateProcessStartInfo("git", $"checkout {branch}", repoDir);
            using var pCheckout = System.Diagnostics.Process.Start(psiCheckout);
            pCheckout?.WaitForExit(60_000);
        }

        var psi = CreateProcessStartInfo("git", "pull", repoDir);
        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) throw new Exception("无法启动 git 进程");

        var error = p.StandardError.ReadToEnd();
        p.WaitForExit(300_000);

        if (p.ExitCode != 0)
            throw new Exception($"Git 拉取失败：{error}");

        XTrace.WriteLine("Git 拉取成功");
    }

    /// <summary>执行构建脚本。自动判断执行环境，Linux直接使用bash，Windows使用Git Bash</summary>
    private void ExecuteBuildScript(String scriptPath, String workingDirectory)
    {
        XTrace.WriteLine("开始执行构建脚本：{0}", scriptPath);

        if (OperatingSystem.IsWindows())
        {
            // Windows 环境，使用 Git Bash 执行
            var gitBash = FindGitBash();
            if (gitBash.IsNullOrEmpty())
                throw new Exception("未找到 Git Bash，请安装 Git for Windows");

            XTrace.WriteLine("使用 Git Bash: {0}", gitBash);
            ExecuteProcess(gitBash, $"-l -c \"bash '{scriptPath}'\"", workingDirectory);
        }
        else
        {
            // Linux/macOS 环境，直接使用 bash
            XTrace.WriteLine("使用 bash 执行脚本");

            // 确保脚本有执行权限
            ExecuteProcess("chmod", $"+x \"{scriptPath}\"", workingDirectory);
            ExecuteProcess("bash", $"\"{scriptPath}\"", workingDirectory);
        }

        XTrace.WriteLine("构建脚本执行成功");
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
        XTrace.WriteLine("开始压缩：{0} -> {1}", sourceDir, zipFile);

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
            XTrace.WriteLine("获取Git提交信息失败：{0}", ex.Message);
            return ("", "", "");
        }
    }

    /// <summary>上传包文件到星尘平台。调用Deploy/UploadBuildFile接口创建应用版本</summary>
    /// <param name="server">服务器地址</param>
    /// <param name="deployName">应用部署集名称</param>
    /// <param name="packagePath">包文件路径</param>
    /// <param name="commitId">提交标识</param>
    /// <param name="commitLog">提交记录</param>
    /// <param name="commitTime">提交时间</param>
    private void UploadPackage(String server, String deployName, String packagePath, String? commitId = null, String? commitLog = null, String? commitTime = null)
    {
        XTrace.WriteLine("开始上传包文件：{0}", packagePath);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(server.TrimEnd('/')),
            Timeout = TimeSpan.FromMinutes(10)
        };

        // 使用StarClient的登录令牌进行认证，避免在请求头中传递明文密钥
        var token = _client.Client?.Token;
        if (!token.IsNullOrEmpty())
            httpClient.DefaultRequestHeaders.Add("X-Token", token);

        var version = $"v{DateTime.Now:yyyyMMdd-HHmmss}";

        // 使用MultipartFormData上传，对应Deploy/UploadBuildFile接口
        using var content = new MultipartFormDataContent();
        var fileBytes = File.ReadAllBytes(packagePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", Path.GetFileName(packagePath));

        var uploadUrl = $"/Deploy/UploadBuildFile?deployName={Uri.EscapeDataString(deployName)}&version={Uri.EscapeDataString(version)}";
        if (!commitId.IsNullOrEmpty()) uploadUrl += $"&commitId={Uri.EscapeDataString(commitId)}";
        if (!commitLog.IsNullOrEmpty()) uploadUrl += $"&commitLog={Uri.EscapeDataString(commitLog)}";
        if (!commitTime.IsNullOrEmpty()) uploadUrl += $"&commitTime={Uri.EscapeDataString(commitTime)}";
        XTrace.WriteLine("上传 URL: {0}{1}", server, uploadUrl);

        var response = httpClient.PostAsync(uploadUrl, content).Result;
        var responseContent = response.Content.ReadAsStringAsync().Result;

        if (!response.IsSuccessStatusCode)
            throw new Exception($"上传失败：{response.StatusCode} - {responseContent}");

        XTrace.WriteLine("上传成功：{0}", responseContent);
    }

    /// <summary>创建进程启动信息</summary>
    private ProcessStartInfo CreateProcessStartInfo(String fileName, String arguments, String? workingDirectory = null)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
    }
}
