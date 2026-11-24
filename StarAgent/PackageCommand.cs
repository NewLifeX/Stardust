using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using StarAgent.Managers;
using Stardust;
using Stardust.Deployment;
using Stardust.Managers;
using Stardust.Models;

namespace StarAgent;

/// <summary>star 命令行。StarAgent 的应用包管理入口</summary>
/// <remarks>
/// star 是 StarAgent 的别名命令，负责从本机代理连接的服务端拉取应用包、安装并运行。
/// 有 -d 时交给 StarAgent 服务守护，无 -d 时前台运行并在 Ctrl+C 时停止。
/// </remarks>
public static class PackageCommand
{
    private static readonly String[] _commands = ["run", "pull", "ls", "list", "info", "search", "start", "stop", "restart", "rm", "help"];

    /// <summary>输出命令行结果。直接写控制台，不带日志时间戳，保持与 docker 类似的简洁输出</summary>
    /// <param name="format">格式字符串</param>
    /// <param name="args">参数</param>
    private static void WriteLine(String format, params Object?[] args)
    {
        if (args == null || args.Length == 0)
            Console.WriteLine(format);
        else
            Console.WriteLine(format, args);
    }

    #region 入口
    /// <summary>处理不依赖日志与运行时的帮助和版本命令。应在初始化日志之前调用，避免输出日志横幅</summary>
    /// <param name="args">命令行参数</param>
    /// <returns>是否已处理</returns>
    public static Boolean TryRunMeta(String[] args)
    {
        if (args == null || args.Length == 0) return false;

        var cmd = args[0];
        if (cmd.EqualIgnoreCase("--help", "-h", "-?", "help"))
        {
            ShowHelp();
            return true;
        }
        if (cmd.EqualIgnoreCase("--version"))
        {
            ShowVersion();
            return true;
        }

        return false;
    }

    /// <summary>尝试执行 star 命令。不是包管理命令时返回 false，交回 StarAgent 原有逻辑</summary>
    /// <param name="args">命令行参数</param>
    /// <returns>是否已处理</returns>
    public static Boolean TryRun(String[] args)
    {
        if (args == null || args.Length == 0) return false;

        if (TryRunMeta(args)) return true;

        var cmd = args[0];
        var shorthand = false;
        if (!_commands.Any(e => e.EqualIgnoreCase(cmd)))
        {
            // 包名简写：必须带命名空间，且不是本地 zip / 服务参数
            if (!cmd.StartsWith("-") && cmd.Contains('/') && !cmd.EndsWithIgnoreCase(".zip"))
            {
                cmd = "run";
                shorthand = true;
            }
            else
            {
                return false;
            }
        }

        try
        {
            RunAsync(args, cmd, shorthand).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static async Task RunAsync(String[] args, String command, Boolean shorthand)
    {
        var list = shorthand ? args : args.Skip(1).ToArray();

        switch (command.ToLowerInvariant())
        {
            case "run": await RunPackageAsync(list).ConfigureAwait(false); break;
            case "pull": await PullAsync(list).ConfigureAwait(false); break;
            case "ls":
            case "list": await ListAsync().ConfigureAwait(false); break;
            case "info": await InfoAsync(list).ConfigureAwait(false); break;
            case "search": await SearchAsync(list).ConfigureAwait(false); break;
            case "start": await ChangeStateAsync(list, "PackageStart").ConfigureAwait(false); break;
            case "stop": await ChangeStateAsync(list, "PackageStop").ConfigureAwait(false); break;
            case "restart":
                await ChangeStateAsync(list, "PackageStop").ConfigureAwait(false);
                await ChangeStateAsync(list, "PackageStart").ConfigureAwait(false);
                break;
            case "rm": await RemoveAsync(list).ConfigureAwait(false); break;
            default: ShowHelp(); break;
        }
    }
    #endregion

    #region 运行
    private static async Task RunPackageAsync(String[] args)
    {
        var options = ParseArgs(args, true);
        if (options.Help)
        {
            ShowRunHelp();
            return;
        }
        if (options.Package.IsNullOrEmpty() && options.File.IsNullOrEmpty()) throw new ArgumentException("Package name or -f is required.");

        var store = new StarPackageStore { Log = Logger.Null };

        // 本地 zip：只支持前台运行
        if (!options.File.IsNullOrEmpty())
        {
            var local = store.InstallFromFile(options.File);
            if (options.Daemon)
                throw new InvalidOperationException(L.T("-d is not supported for a local package file."));

            await RunForegroundAsync(local, store.GetVersionDir(local.Name!, local.Version!), options).ConfigureAwait(false);
            return;
        }

        var name = options.Package!;
        var agentRunning = IsLocalAgentRunning();
        var daemon = options.Daemon;

        if (daemon)
        {
            if (!agentRunning) throw new InvalidOperationException(L.T("StarAgent service is not running. Start it before using -d."));

            var rs = InvokeLocal<StarPackageInstallResult>("PackageInstall", new Dictionary<String, Object?>
            {
                ["name"] = name,
                ["version"] = options.Version ?? "",
                ["arguments"] = options.Extra.Join(" "),
            });
            if (rs == null || !rs.Success) throw new InvalidOperationException(rs?.Message ?? L.T("Package install failed."));

            WriteLine(L.T("Package {0} {1} installed and supervised.", rs.Name, rs.Version));
            foreach (var item in rs.Services ?? [])
            {
                WriteLine("  {0} -> {1} {2}", item.Name, item.FileName, item.Arguments);
            }
            return;
        }

        // 前台运行。代理已运行时通过它下载，避免重复登录顶掉代理会话
        StarPackage manifest;
        if (agentRunning)
        {
            try
            {
                manifest = await FetchFromAgentAsync(name, options.Version, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 代理在跑时绝不能改为直连，否则同节点会话会把代理顶下线
                throw new InvalidOperationException(L.T("Failed to fetch package from the local agent: {0}. Upgrade StarAgent or StarServer if either is an old version.", ex.Message), ex);
            }
        }
        else
        {
            manifest = await FetchDirectAsync(name, options.Version, default).ConfigureAwait(false);
        }

        await RunForegroundAsync(manifest, store.GetVersionDir(manifest.Name!, manifest.Version!), options).ConfigureAwait(false);
    }

    private static async Task PullAsync(String[] args)
    {
        var options = ParseArgs(args, false);
        if (options.Help)
        {
            ShowPullHelp();
            return;
        }
        if (options.Package.IsNullOrEmpty() && options.File.IsNullOrEmpty()) throw new ArgumentException("Package name or -f is required.");

        if (!options.File.IsNullOrEmpty())
        {
            var store = new StarPackageStore { Log = Logger.Null };
            var local = store.InstallFromFile(options.File);
            WriteLine(L.T("Pulled {0} {1} to {2}", local.Name, local.Version, store.GetVersionDir(local.Name!, local.Version!)));
            return;
        }

        var name = options.Package!;
        StarPackage manifest;
        if (IsLocalAgentRunning())
        {
            try
            {
                manifest = await FetchFromAgentAsync(name, options.Version, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 代理在跑时绝不能改为直连，否则同节点会话会把代理顶下线
                throw new InvalidOperationException(L.T("Failed to fetch package from the local agent: {0}. Upgrade StarAgent or StarServer if either is an old version.", ex.Message), ex);
            }
        }
        else
        {
            manifest = await FetchDirectAsync(name, options.Version, default).ConfigureAwait(false);
        }

        var dir = new StarPackageStore().GetVersionDir(manifest.Name!, manifest.Version!);
        WriteLine(L.T("Pulled {0} {1} to {2}", manifest.Name, manifest.Version, dir));
    }

    private static async Task RunForegroundAsync(StarPackage manifest, String root, PackageArgs options)
    {
        CheckPorts(manifest);

        var services = manifest.ToServices(root);
        var main = manifest.GetMainApp();
        if (main != null && options.Extra.Count > 0)
        {
            var svc = services.FirstOrDefault(e => e.PackageApp.EqualIgnoreCase(main.Name));
            if (svc != null) svc.Arguments = $"{svc.Arguments} {options.Extra.Join(" ")}".Trim();
        }

        var processes = new List<Process>();
        var stopped = false;
        ConsoleCancelEventHandler handler = (s, e) =>
        {
            e.Cancel = true;
            stopped = true;
            Environment.ExitCode = 130;
        };
        Console.CancelKeyPress += handler;
        try
        {
            foreach (var item in services)
            {
                WriteLine(L.T("Starting {0}: {1} {2}", item.Name, item.FileName, item.Arguments));

                var strategy = new StandardDeployStrategy();
                var context = new DeployContext
                {
                    Name = item.Name,
                    Service = item,
                    WorkingDirectory = item.WorkingDirectory,
                    Arguments = item.Arguments,
                    StartWait = 3_000,
                    Log = new ActionLog((format, args) => WriteLine(format, args)),
                };

                if (!strategy.Extract(context)) throw new InvalidOperationException(L.T("Failed to prepare application [{0}].", item.Name));

                var process = strategy.Execute(context)
                    ?? throw new InvalidOperationException(context.LastError.IsNullOrEmpty() ? L.T("Failed to start application [{0}].", item.Name) : context.LastError);

                processes.Add(process);
                WriteLine(L.T("Application {0} started, PID={1}", item.Name, process.Id));
            }

            while (!stopped)
            {
                if (processes.All(e => e.HasExited)) break;
                await Task.Delay(500).ConfigureAwait(false);
            }
        }
        finally
        {
            Console.CancelKeyPress -= handler;

            for (var i = processes.Count - 1; i >= 0; i--)
            {
                var process = processes[i];
                try
                {
                    if (!process.HasExited) KillTree(process);
                }
                catch (Exception ex)
                {
                    WriteLine(L.T("Failed to stop process {0}: {1}", process.Id, ex.Message));
                }
            }
        }
    }

    private static void CheckPorts(StarPackage manifest)
    {
        if (manifest.Apps == null || manifest.Apps.Length == 0) return;

        var ports = new HashSet<Int32>();
        foreach (var app in manifest.Apps)
        {
            if (app.Ports == null) continue;
            foreach (var port in app.Ports) ports.Add(port);
        }
        if (ports.Count == 0) return;

        var props = IPGlobalProperties.GetIPGlobalProperties();
        var used = new HashSet<Int32>();
        foreach (var item in props.GetActiveTcpListeners()) used.Add(item.Port);
        foreach (var item in props.GetActiveUdpListeners()) used.Add(item.Port);

        foreach (var port in ports)
        {
            if (used.Contains(port)) throw new InvalidOperationException(L.T("Port {0} is already in use.", port));
        }
    }

    private static void KillTree(Process process)
    {
#if NET5_0_OR_GREATER
        process.Kill(true);
#else
        if (Runtime.Windows)
            Process.Start("taskkill", $"/PID {process.Id} /T /F")?.WaitForExit(5_000);
        else
            process.Kill();
#endif
        process.WaitForExit(5_000);
    }
    #endregion

    #region 查询与生命周期
    private static async Task ListAsync()
    {
        var store = new StarPackageStore { Log = Logger.Null };
        var packages = store.List();
        StarPackageListResult? state = null;

        if (IsLocalAgentRunning())
        {
            try
            {
                state = InvokeLocal<StarPackageListResult>("PackageList", []);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(L.T("Failed to query the local agent: {0}", ex.Message));
            }
        }

        if (state?.Packages is { Length: > 0 }) packages = state.Packages;

        if (packages.Count == 0)
        {
            WriteLine(L.T("No packages installed."));
            return;
        }

        var running = state?.Running?.Select(e => e.Name).ToArray() ?? [];
        var services = state?.Services ?? [];
        foreach (var item in packages)
        {
            // 服务按版本归属；旧服务的 PackageVersion 为空时只归到当前版本
            var versionServices = services
                .Where(e => e.Package.EqualIgnoreCase(item.Name) && (e.PackageVersion.IsNullOrEmpty() ? item.IsCurrent : e.PackageVersion.EqualIgnoreCase(item.Version)))
                .ToArray();
            var isRunning = versionServices.Any(e => running.Any(r => r.EqualIgnoreCase(e.Name)));
            var mark = isRunning ? "*" : " ";
            WriteLine("{0} {1} {2}  {3}", mark, item.Name, item.Version, item.IsCurrent ? L.T("(current)") : "");

            foreach (var svc in versionServices)
            {
                var run = running.Any(e => e.EqualIgnoreCase(svc.Name));
                WriteLine("    {0} {1} enable={2}", run ? L.T("running") : L.T("stopped"), svc.Name, svc.Enable);
            }
        }
    }

    private static async Task InfoAsync(String[] args)
    {
        var name = args.FirstOrDefault();
        if (name.IsNullOrEmpty()) throw new ArgumentException("Package name is required.");

        PackageInfo? info = null;
        PackageVersionInfo[]? versions = null;
        if (IsLocalAgentRunning())
        {
            try
            {
                info = InvokeLocal<PackageInfo>("PackageGet", new Dictionary<String, Object?> { ["name"] = name });
                versions = InvokeLocal<PackageVersionInfo[]>("PackageGetVersions", new Dictionary<String, Object?> { ["name"] = name });
            }
            catch (Exception ex)
            {
                // 代理在跑时绝不能改为直连，否则同节点会话会把代理顶下线
                throw new InvalidOperationException(L.T("Failed to query the local agent: {0}. Upgrade StarAgent or StarServer if either is an old version.", ex.Message), ex);
            }
        }

        if (info == null)
        {
            using var client = await LoginAsync().ConfigureAwait(false);
            info = await client.GetPackage(name).ConfigureAwait(false);
            versions = await client.GetPackageVersions(name).ConfigureAwait(false);
        }

        if (info == null) throw new InvalidOperationException(L.T("Package [{0}] was not found.", name));

        WriteLine("{0}  {1}", info.Name, info.DisplayName);
        if (!info.Description.IsNullOrEmpty()) WriteLine(info.Description);
        WriteLine(L.T("Current version: {0}", info.Version));
        foreach (var item in versions ?? [])
        {
            WriteLine("  {0}  {1}/{2}  {3:n0} bytes  {4}", item.Version, item.OS, item.Arch, item.Size, item.PublishTime);
        }
    }

    private static async Task SearchAsync(String[] args)
    {
        var key = args.Join(" ");
        PackageInfo[]? list = null;
        if (IsLocalAgentRunning())
        {
            try
            {
                list = InvokeLocal<PackageInfo[]>("PackageSearch", new Dictionary<String, Object?>
                {
                    ["key"] = key,
                    ["pageIndex"] = 1,
                    ["pageSize"] = 50,
                });
            }
            catch (Exception ex)
            {
                // 代理在跑时绝不能改为直连，否则同节点会话会把代理顶下线
                throw new InvalidOperationException(L.T("Failed to query the local agent: {0}. Upgrade StarAgent or StarServer if either is an old version.", ex.Message), ex);
            }
        }

        if (list == null)
        {
            using var client = await LoginAsync().ConfigureAwait(false);
            list = await client.SearchPackage(key, 1, 50).ConfigureAwait(false);
        }

        if (list == null || list.Length == 0)
        {
            WriteLine(L.T("No packages found."));
            return;
        }

        foreach (var item in list)
        {
            WriteLine("{0,-24} {1,-16} {2}", item.Name, item.Version, item.Description);
        }
    }

    private static async Task ChangeStateAsync(String[] args, String action)
    {
        var name = args.FirstOrDefault();
        if (name.IsNullOrEmpty()) throw new ArgumentException("Package name is required.");
        if (!IsLocalAgentRunning()) throw new InvalidOperationException(L.T("StarAgent service is not running."));

        var rs = InvokeLocal<StarPackageOperationResult>(action, new Dictionary<String, Object?> { ["name"] = name });
        if (rs == null || !rs.Success) throw new InvalidOperationException(rs?.Message ?? L.T("Operation failed."));

        WriteLine(rs.Message);
    }

    private static Task RemoveAsync(String[] args)
    {
        var token = args.FirstOrDefault();
        if (token.IsNullOrEmpty()) throw new ArgumentException("Package name is required.");

        var name = token;
        var version = "";
        var index = token.LastIndexOf(':');
        if (index > 0)
        {
            name = token[..index];
            version = token[(index + 1)..];
        }

        var removed = false;
        String? agentError = null;
        if (IsLocalAgentRunning())
        {
            try
            {
                var rs = InvokeLocal<StarPackageOperationResult>("PackageRemove", new Dictionary<String, Object?>
                {
                    ["name"] = name,
                    ["version"] = version,
                });
                if (rs != null && rs.Success) removed = true;
                else if (rs != null && !rs.Message.IsNullOrEmpty()) agentError = rs.Message;
            }
            catch (Exception ex)
            {
                // 旧代理没有包管理 RPC，继续尝试清理本地缓存
                Console.Error.WriteLine(L.T("Failed to query the local agent: {0}", ex.Message));
            }
        }

        // 代理未运行或旧代理时，直接清理用户缓存中的包
        var store = new StarPackageStore { Log = Logger.Null };
        if (store.Remove(name, version)) removed = true;

        if (!removed && agentError != null) throw new InvalidOperationException(agentError);
        if (!removed) throw new InvalidOperationException(L.T("Package [{0}] was not found.", name));

        // 本地缓存已删，但守护侧删除失败时仍然给出错误信号
        if (agentError != null)
        {
            Console.Error.WriteLine(agentError);
            Environment.ExitCode = 1;
        }

        WriteLine(L.T("Package [{0}] removed.", name));

        return Task.FromResult(0);
    }
    #endregion

    #region 数据源
    private static async Task<StarPackage> FetchFromAgentAsync(String name, String? version, CancellationToken cancellationToken)
    {
        var rs = InvokeLocal<StarPackageFetchResult>("PackageFetch", new Dictionary<String, Object?>
        {
            ["name"] = name,
            ["version"] = version ?? "",
        });
        if (rs == null || !rs.Success || rs.ZipPath.IsNullOrEmpty()) throw new InvalidOperationException(rs?.Message ?? L.T("Package fetch failed."));

        var temp = Path.Combine(Path.GetTempPath(), $"star-{Guid.NewGuid():n}.zip");
        try
        {
            File.Copy(rs.ZipPath, temp, true);

            var store = new StarPackageStore { Log = Logger.Null };
            return rs.Manifest.IsNullOrEmpty()
                ? store.InstallFromFile(temp, rs.Hash)
                : store.InstallFromFile(temp, StarPackage.Parse(rs.Manifest!), rs.Hash);
        }
        finally
        {
            TryDelete(temp);
            TryDelete(rs.ZipPath);
        }
    }

    private static async Task<StarPackage> FetchDirectAsync(String name, String? version, CancellationToken cancellationToken)
    {
        using var client = await LoginAsync().ConfigureAwait(false);

        var info = await client.GetLatestPackage(name, 0, 0, version).ConfigureAwait(false);
        if (info == null || info.Url.IsNullOrEmpty()) throw new InvalidOperationException(L.T("Package [{0}] version [{1}] was not found.", name, version));

        var store = new StarPackageStore { Log = Logger.Null };
        var zip = await store.DownloadAsync(client.BuildUrl(info.Url), info.Hash, cancellationToken).ConfigureAwait(false);
        try
        {
            return info.Manifest.IsNullOrEmpty()
                ? store.InstallFromFile(zip, info.Hash)
                : store.InstallFromFile(zip, StarPackage.Parse(info.Manifest!), info.Hash);
        }
        finally
        {
            TryDelete(zip);
        }
    }

    private static async Task<MyStarClient> LoginAsync()
    {
        var set = StarSetting.Current;
        if (set.Server.IsNullOrEmpty()) throw new InvalidOperationException(L.T("StarServer address is not configured. Start StarAgent first."));

        var client = new MyStarClient(StarAgentSetting.Current) { Log = Logger.Null };
        client.Server = set.Server;
        await client.Login("star-cli", default).ConfigureAwait(false);

        return client;
    }
    #endregion

    #region 辅助
    private static PackageArgs ParseArgs(String[] args, Boolean allowDaemon)
    {
        var rs = new PackageArgs();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.EqualIgnoreCase("-h", "--help"))
            {
                rs.Help = true;
                continue;
            }
            if (allowDaemon && arg.EqualIgnoreCase("-d", "--daemon"))
            {
                rs.Daemon = true;
                continue;
            }
            if (arg.EqualIgnoreCase("-f", "--file"))
            {
                if (++i >= args.Length) throw new ArgumentException("-f requires a file path.");
                rs.File = args[i];
                continue;
            }
            if (arg.EqualIgnoreCase("-v", "--verbose")) continue;
            if (arg == "--")
            {
                for (var j = i + 1; j < args.Length; j++) rs.Extra.Add(args[j]);
                break;
            }

            if (rs.Package.IsNullOrEmpty())
            {
                if (arg.StartsWith("-")) throw new ArgumentException($"Unknown option [{arg}].");

                var index = arg.LastIndexOf(':');
                if (index > 0)
                {
                    rs.Package = arg[..index];
                    rs.Version = arg[(index + 1)..];
                }
                else
                {
                    rs.Package = arg;
                }
                continue;
            }

            // 包名之后的参数原样透传给主应用，不做变量替换
            if (arg.StartsWith("-") && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                rs.Extra.Add(arg);
                rs.Extra.Add(args[++i]);
            }
            else
            {
                rs.Extra.Add(arg);
            }
        }

        return rs;
    }

    private sealed class PackageArgs
    {
        public Boolean Daemon;
        public Boolean Help;
        public String? File;
        public String? Package;
        public String? Version;
        public readonly List<String> Extra = [];
    }

    private static Boolean IsLocalAgentRunning()
    {
        var set = StarAgentSetting.Current;
        if (set.LocalPort <= 0) return false;

        LocalStarClient.Port = set.LocalPort;
        try
        {
            // 首次 UDP 调用需要建立连接，GetInfo 默认只等 1 秒，CLI 冷启动时容易误判代理未运行。
            // 这里给足 5 秒，避免误走直连登录把代理会话顶下线
            var task = new LocalStarClient { Log = Logger.Null }.GetInfoAsync();
            return task.Wait(5_000) && task.Result != null;
        }
        catch
        {
            return false;
        }
    }

    private static T? InvokeLocal<T>(String action, Dictionary<String, Object?> args)
    {
        var set = StarAgentSetting.Current;
        if (set.LocalPort <= 0) throw new InvalidOperationException(L.T("Local port is not configured."));

        using var client = new ApiClient($"udp://127.0.0.1:{set.LocalPort}")
        {
            Timeout = 300_000,
            Log = Logger.Null,
        };
        args["token"] = LocalApiToken.Read();

        return client.Invoke<T>(action, args);
    }

    private static void TryDelete(String? path)
    {
        if (path.IsNullOrEmpty()) return;

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 忽略临时文件清理失败
        }
    }

    private static void ShowVersion()
    {
        var asm = typeof(PackageCommand).Assembly;
        var version = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (version.IsNullOrEmpty()) version = asm.GetName().Version?.ToString();

        WriteLine("star {0}", version?.Split('+')[0]);
    }

    private static void ShowHelp()
    {
        WriteLine("""
star - Stardust package manager

Usage:
  star <package>[:version]                 Run with the package default mode
  star run <package>[:version] [-d] [args] Run a package; -d supervises it
  star pull <package>[:version]            Download only
  star ls                                  List installed packages
  star info <package>                      Show package details
  star search [keyword]                    Search server packages
  star start|stop|restart <package>        Control a supervised package
  star rm <package>[:version]              Remove a package
  star run -f <file.zip>                   Run a local package file

Options:
  -d, --daemon   Install and supervise with StarAgent
  -f, --file     Local package zip
  -h, --help     Show help
""");
    }

    private static void ShowRunHelp()
    {
        WriteLine("""
Usage: star run <package>[:version] [-d] [args...]
       star <package>[:version] [-d] [args...]

  -d, --daemon   Install and supervise with StarAgent
  -f, --file     Run a local package zip
  args           Appended to the main application as-is
""");
    }

    private static void ShowPullHelp()
    {
        WriteLine("""
Usage: star pull <package>[:version]
       star pull -f <file.zip>
""");
    }
    #endregion
}
