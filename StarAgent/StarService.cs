using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using NewLife;
using NewLife.Agent;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Threading;
using Stardust;
using Stardust.Deployment;
using Stardust.Managers;
using Stardust.Models;

namespace StarAgent;

[Api(null)]
public class StarService : DisposeBase, IApi
{
    #region 属性
    /// <summary>
    /// 网络会话
    /// </summary>
    public IApiSession Session { get; set; }

    ///// <summary>服务对象</summary>
    //public ServiceBase Service { get; set; }

    ///// <summary>服务主机</summary>
    //public IHost Host { get; set; }

    /// <summary>应用服务管理</summary>
    public ServiceManager Manager { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider Provider { get; set; }

    ///// <summary>插件管理</summary>
    //public PluginManager PluginManager { get; set; }

    /// <summary>星尘设置</summary>
    public StarSetting StarSetting { get; set; }

    /// <summary>星尘代理设置</summary>
    public StarAgentSetting AgentSetting { get; set; }

    private AgentInfo _agentInfo;
    private TimerX _timer;
    #endregion

    #region 构造
    public StarService()
    {
        // 本机 RPC 共享令牌，代理启动时生成并写入 local.token，供本机调用方读取
        LocalApiToken.Ensure();

        // 获取本地进程名比较慢，平均200ms，有时候超过500ms
        //Task.Run(() =>
        //{
        //    _agentInfo = AgentInfo.GetLocal(true);
        //});
        _timer = new TimerX(DoRefreshLocal, null, 100, 5_000) { Async = true };
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
    }
    #endregion

    #region 业务
    /// <summary>信息</summary>
    /// <returns></returns>
    [Api(nameof(Info))]
    public AgentInfo Info(AgentInfo info)
    {
        //XTrace.WriteLine("Info<={0}", info.ToJson());

        var set = StarSetting;

        var ai = _agentInfo ??= AgentInfo.GetLocal(true);
        ai.Server = set.Server;
        ai.Services = Manager?.Services?.Where(e => e.Enable || !e.Name.EqualIgnoreCase("test", "test2")).Select(e => e.Name).ToArray();
        ai.Code = AgentSetting.Code;
        ai.IP = AgentInfo.GetIps();

        var raw = (ControllerContext.Current?.Request as IPacket)?.ToStr();

        // 局域网广播搜索等无参探测不带请求体，补上来访地址，避免平台历史出现空内容
        if (raw.IsNullOrEmpty())
        {
            var remote = (Session as INetSession)?.Remote;
            raw = L.TLog("Received a local probe from {0}", remote == null ? "-" : remote.ToString());
            WriteLog(raw);
        }

        if (Provider?.GetService<StarClient>() is StarClient client)
        {
            client.WriteEvent("info", "LocalProbe", raw);
        }

        // 更新应用服务
        var controller = info == null ? null : Manager?.QueryByProcess(info.ProcessId);
        if (controller != null)
        {
            // 标记为星尘应用，停止Deploy上报进程信息
            controller.IsStarApp = true;

            controller.WriteEvent("LocalProbe", raw!);
        }

        // 返回插件服务器地址
        var core = NewLife.Setting.Current;
        if (!core.PluginServer.IsNullOrEmpty() && !core.PluginServer.Contains("x.newlifex.com"))
        {
            ai.PluginServer = core.PluginServer;
        }

        return ai;
    }

    /// <summary>本地心跳</summary>
    /// <param name="info"></param>
    /// <returns></returns>
    [Api(nameof(Ping))]
    public IPingResponse Ping(LocalPingInfo info)
    {
        if (info != null && info.ProcessId > 0)
        {
            // 喂狗
            if (info.WatchdogTimeout > 0) FeedDog(info.ProcessId, info.WatchdogTimeout);
        }

        return new PingResponse { ServerTime = DateTime.UtcNow.ToLong() };
    }

    /// <summary>设置星尘服务端地址</summary>
    /// <param name="server">服务端地址</param>
    /// <param name="token">本机 RPC 令牌。只允许本机特权调用方改写服务端配置</param>
    /// <returns></returns>
    [Api(nameof(SetServer))]
    public String SetServer(String server, String? token)
    {
        CheckLocal();
        CheckToken(token);

        var set = StarSetting;
        if (set.Server.IsNullOrEmpty() && !server.IsNullOrEmpty())
        {
            set.Server = server;
            set.Save();

            XTrace.WriteLine("StarAgent uses the Stardust server address provided by [{0}]: {1}", Session, server);

            if (Provider?.GetService<ServiceBase>() is MyService svc)
            {
                ThreadPool.QueueUserWorkItem(s =>
                {
                    Thread.Sleep(1000);

                    svc.StartFactory();
                    svc.StartClient();
                });
            }
        }

        return set.Server;
    }

    /// <summary>获取所有服务列表</summary>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns></returns>
    [Api(nameof(GetServices))]
    public ServicesInfo GetServices(String? token)
    {
        CheckLocal();
        CheckToken(token);

        var list = Manager.Services;
        var runningList = Manager.RunningServices;

        var result = new ServicesInfo
        {
            Services = list?.ToArray(),
            RunningServices = runningList?.ToArray()
        };

        return result;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns></returns>
    [Api("StartService")]
    public ServiceOperationResult StartService(String serviceName, String? token)
    {
        CheckLocal();
        CheckToken(token);

        if (serviceName.IsNullOrEmpty())
        {
            return new ServiceOperationResult { Success = false, Message = L.T("Service name cannot be empty.") };
        }

        try
        {
            var result = Manager?.Start(serviceName);
            return new ServiceOperationResult
            {
                Success = result ?? false,
                Message = result == true ? L.T("Service started successfully.") : L.T("Service failed to start or does not exist."),
                ServiceName = serviceName
            };
        }
        catch (Exception ex)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Message = L.T("Error while starting service: {0}", ex.Message),
                ServiceName = serviceName
            };
        }
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns></returns>
    [Api("StopService")]
    public ServiceOperationResult StopService(String serviceName, String? token)
    {
        CheckLocal();
        CheckToken(token);

        if (serviceName.IsNullOrEmpty())
        {
            return new ServiceOperationResult { Success = false, Message = L.T("Service name cannot be empty.") };
        }

        try
        {
            var result = Manager?.Stop(serviceName, "API call stop");
            return new ServiceOperationResult
            {
                Success = result ?? false,
                Message = result == true ? L.T("Service stopped successfully.") : L.T("Service failed to stop or does not exist."),
                ServiceName = serviceName
            };
        }
        catch (Exception ex)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Message = L.T("Error while stopping service: {0}", ex.Message),
                ServiceName = serviceName
            };
        }
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns></returns>
    [Api("RestartService")]
    public ServiceOperationResult RestartService(String serviceName, String? token)
    {
        CheckLocal();
        CheckToken(token);

        if (serviceName.IsNullOrEmpty())
        {
            return new ServiceOperationResult { Success = false, Message = L.T("Service name cannot be empty.") };
        }

        try
        {
            // 使用共同的重启逻辑
            var success = InternalRestartService(Manager, serviceName, "API call restart");

            // 根据结果提供更详细的信息
            var message = success ? L.T("Service restarted successfully.") : GetRestartFailureMessage(Manager, serviceName);

            return new ServiceOperationResult
            {
                Success = success,
                Message = message,
                ServiceName = serviceName
            };
        }
        catch (Exception ex)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Message = L.T("Error while restarting service: {0}", ex.Message),
                ServiceName = serviceName
            };
        }
    }

    /// <summary>获取重启失败的详细原因</summary>
    private static String GetRestartFailureMessage(ServiceManager manager, String serviceName)
    {
        if (manager?.Services?.Any(e => e.Name.EqualIgnoreCase(serviceName)) != true)
        {
            return L.T("Service does not exist.");
        }

        var isRunning = manager.RunningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true;
        if (isRunning)
        {
            return L.T("Service restart failed: unable to stop service [{0}]", serviceName);
        }
        else
        {
            return L.T("Service restart failed: unable to start service [{0}]", serviceName);
        }
    }

    #region 应用包
    /// <summary>搜索应用包</summary>
    /// <param name="key">关键字</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">页大小</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>包列表</returns>
    [Api(nameof(PackageSearch))]
    public PackageInfo[] PackageSearch(String? key, Int32 pageIndex, Int32 pageSize, String? token)
    {
        CheckLocal();
        CheckToken(token);

        var client = GetServerClient() ?? throw new InvalidOperationException(L.T("StarAgent is not connected to the server."));

        return client.SearchPackage(key, pageIndex, pageSize).GetAwaiter().GetResult() ?? [];
    }

    /// <summary>获取应用包信息</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>包信息</returns>
    [Api(nameof(PackageGet))]
    public PackageInfo? PackageGet(String name, String? token)
    {
        CheckLocal();
        CheckToken(token);

        var client = GetServerClient() ?? throw new InvalidOperationException(L.T("StarAgent is not connected to the server."));

        return client.GetPackage(name).GetAwaiter().GetResult();
    }

    /// <summary>获取应用包最新匹配版本</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <param name="os">目标操作系统。0表示使用节点平台</param>
    /// <param name="arch">目标架构。0表示使用节点平台</param>
    /// <param name="version">指定版本。为空时取最新版本</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>版本信息</returns>
    [Api(nameof(PackageGetLatest))]
    public PackageVersionInfo? PackageGetLatest(String name, Int32 os, Int32 arch, String? version, String? token)
    {
        CheckLocal();
        CheckToken(token);

        var client = GetServerClient() ?? throw new InvalidOperationException(L.T("StarAgent is not connected to the server."));

        return client.GetLatestPackage(name, os, arch, version).GetAwaiter().GetResult();
    }

    /// <summary>下载包 zip 到本机临时目录。调用方负责复制并清理</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <param name="version">版本。为空时取最新匹配版本</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>下载结果</returns>
    [Api(nameof(PackageFetch))]
    public StarPackageFetchResult PackageFetch(String name, String? version, String? token)
    {
        CheckLocal();
        CheckToken(token);

        try
        {
            var client = GetServerClient() ?? throw new InvalidOperationException(L.T("StarAgent is not connected to the server."));
            var ver = FindVersion(client, name, version);
            if (ver == null || ver.Url.IsNullOrEmpty()) return new StarPackageFetchResult { Success = false, Message = L.T("Package [{0}] version [{1}] was not found.", name, version), Name = name, Version = version };

            var store = CreatePackageStore();
            var zip = store.DownloadAsync(client.BuildUrl(ver.Url), ver.Hash).GetAwaiter().GetResult();

            return new StarPackageFetchResult
            {
                Success = true,
                Name = ver.Name,
                Version = ver.Version,
                ZipPath = zip,
                Hash = ver.Hash,
                Manifest = ver.Manifest,
            };
        }
        catch (Exception ex)
        {
            return new StarPackageFetchResult { Success = false, Message = ex.Message, Name = name, Version = version };
        }
    }

    /// <summary>下载并安装应用包，交给服务管理器守护</summary>
    /// <param name="name">完整包名或唯一短名</param>
    /// <param name="version">版本。为空时取当前匹配版本</param>
    /// <param name="arguments">追加到主应用的启动参数</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>安装结果</returns>
    [Api(nameof(PackageInstall))]
    public StarPackageInstallResult PackageInstall(String name, String? version, String? arguments, String? token)
    {
        CheckLocal();
        CheckToken(token);

        try
        {
            var manager = Manager ?? throw new InvalidOperationException(L.T("Service manager not available."));
            var client = GetServerClient() ?? throw new InvalidOperationException(L.T("StarAgent is not connected to the server."));
            var ver = FindVersion(client, name, version);
            if (ver == null || ver.Url.IsNullOrEmpty()) return new StarPackageInstallResult { Success = false, Message = L.T("Package [{0}] version [{1}] was not found.", name, version), Name = name, Version = version };

            var store = CreatePackageStore();
            var manifest = ver.Manifest.IsNullOrEmpty()
                ? store.InstallAsync(ver.Name, ver.Version, client.BuildUrl(ver.Url), ver.Hash).GetAwaiter().GetResult()
                : store.InstallAsync(ver.Name, ver.Version, client.BuildUrl(ver.Url), StarPackage.Parse(ver.Manifest!), ver.Hash).GetAwaiter().GetResult();
            var services = manifest.ToServices(store.GetVersionDir(manifest.Name, manifest.Version));

            // 追加命令行参数到主应用
            var main = manifest.GetMainApp();
            if (main != null && !arguments.IsNullOrEmpty())
            {
                var svc = services.FirstOrDefault(e => e.PackageApp.EqualIgnoreCase(main.Name));
                if (svc != null) svc.Arguments = $"{svc.Arguments} {arguments}".Trim();
            }

            // 记录旧版本，安装失败时回滚
            var old = manager.Services?.Where(e => e.Package.EqualIgnoreCase(manifest.Name)).Select(e => e.Clone()).ToList() ?? [];
            foreach (var item in old)
            {
                manager.Uninstall(item.Name, L.TLog("Upgrading package {0}", manifest.Name));
            }

            var installed = new List<ServiceInfo>();
            try
            {
                foreach (var item in services)
                {
                    var pi = manager.Install(item);
                    if (pi == null) throw new InvalidOperationException(L.T("Failed to start application [{0}].", item.Name));

                    installed.Add(item);
                }
            }
            catch (Exception ex)
            {
                // 回滚：删除新服务，恢复旧服务
                foreach (var item in installed)
                {
                    manager.Uninstall(item.Name, L.TLog("Package install failed; rolling back"));
                }
                foreach (var item in old)
                {
                    manager.Add(item);
                    manager.Start(item.Name);
                }

                PersistServices();

                return new StarPackageInstallResult { Success = false, Message = ex.Message, Name = manifest.Name, Version = manifest.Version };
            }

            PersistServices();

            return new StarPackageInstallResult
            {
                Success = true,
                Name = manifest.Name,
                Version = manifest.Version,
                Services = installed.ToArray(),
            };
        }
        catch (Exception ex)
        {
            return new StarPackageInstallResult { Success = false, Message = ex.Message, Name = name, Version = version };
        }
    }

    /// <summary>停止应用包的全部应用，并禁止自动启动</summary>
    /// <param name="name">完整包名</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>操作结果</returns>
    [Api(nameof(PackageStop))]
    public StarPackageOperationResult PackageStop(String name, String? token) => SetPackageEnable(name, false, token);

    /// <summary>启动应用包的全部应用</summary>
    /// <param name="name">完整包名</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>操作结果</returns>
    [Api(nameof(PackageStart))]
    public StarPackageOperationResult PackageStart(String name, String? token) => SetPackageEnable(name, true, token);

    /// <summary>删除应用包的服务与本地文件</summary>
    /// <param name="name">完整包名</param>
    /// <param name="version">版本。为空时删除整个包</param>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>操作结果</returns>
    [Api(nameof(PackageRemove))]
    public StarPackageOperationResult PackageRemove(String name, String? version, String? token)
    {
        CheckLocal();
        CheckToken(token);

        try
        {
            var manager = Manager ?? throw new InvalidOperationException(L.T("Service manager not available."));

            var list = manager.Services?
                .Where(e => e.Package.EqualIgnoreCase(name) && (version.IsNullOrEmpty() || e.PackageVersion.EqualIgnoreCase(version)))
                .ToList() ?? [];
            var running = manager.RunningServices?
                .Where(e => list.Any(s => s.Name.EqualIgnoreCase(e.Name)))
                .ToArray() ?? [];
            foreach (var item in list)
            {
                manager.Uninstall(item.Name, L.TLog("Removing package {0}", name));
            }

            PersistServices();

            // 确保进程真正退出，Windows 下进程占用工作目录会导致包目录删不掉
            StopOrphanProcesses(running);

            var store = CreatePackageStore();
            var removed = store.Remove(name, version);

            return new StarPackageOperationResult
            {
                Success = removed,
                Name = name,
                Message = removed ? L.T("Package [{0}] removed.", name) : L.T("Package [{0}] was not found.", name),
            };
        }
        catch (Exception ex)
        {
            return new StarPackageOperationResult { Success = false, Message = ex.Message, Name = name };
        }
    }

    /// <summary>列出本机已安装的应用包与运行状态</summary>
    /// <param name="token">本机 RPC 令牌</param>
    /// <returns>包列表</returns>
    [Api(nameof(PackageList))]
    public StarPackageListResult PackageList(String? token)
    {
        CheckLocal();
        CheckToken(token);

        var store = CreatePackageStore();
        var services = Manager?.Services?.Where(e => !e.Package.IsNullOrEmpty()).ToArray() ?? [];
        var running = Manager?.RunningServices ?? [];

        return new StarPackageListResult
        {
            Packages = store.List().ToArray(),
            Services = services,
            Running = running.Select(e => new StarPackageProcessInfo { Name = e.Name, ProcessId = e.ProcessId }).ToArray(),
        };
    }

    private StarPackageOperationResult SetPackageEnable(String name, Boolean enable, String? token)
    {
        CheckLocal();
        CheckToken(token);

        try
        {
            var manager = Manager ?? throw new InvalidOperationException(L.T("Service manager not available."));

            var list = manager.Services?.Where(e => e.Package.EqualIgnoreCase(name)).ToList() ?? [];
            if (list.Count == 0) return new StarPackageOperationResult { Success = false, Message = L.T("Package [{0}] is not installed.", name), Name = name };

            foreach (var item in list)
            {
                var svc = item.Clone();
                svc.Enable = enable;
                manager.Add(svc);

                if (enable)
                    manager.Start(svc.Name);
                else
                    manager.Stop(svc.Name, L.TLog("Package {0} stopped", name));
            }

            PersistServices();

            return new StarPackageOperationResult
            {
                Success = true,
                Name = name,
                Message = enable ? L.T("Package [{0}] started.", name) : L.T("Package [{0}] stopped.", name),
            };
        }
        catch (Exception ex)
        {
            return new StarPackageOperationResult { Success = false, Message = ex.Message, Name = name };
        }
    }

    /// <summary>查找指定平台的最新启用版本。version 为空时取最新，否则按版本与平台双重匹配</summary>
    private static PackageVersionInfo? FindVersion(StarClient client, String name, String? version) => client.GetLatestPackage(name, (Int32)OSKind.Any, (Int32)CpuArch.Any, version).GetAwaiter().GetResult();

    private StarClient? GetServerClient() => Provider?.GetService<StarClient>();

    private StarPackageStore CreatePackageStore()
    {
        var set = AgentSetting ?? StarAgentSetting.Current;
        var path = set.PackagePath.IsNullOrEmpty() ? "../packages" : set.PackagePath;

        return new StarPackageStore(path.GetBasePath())
        {
            Log = Log,
            Tracer = Tracer,
        };
    }

    /// <summary>等待卸载后的进程退出，超时后强制结束。避免进程占用包目录导致删除失败</summary>
    /// <param name="processes">卸载前记录的运行进程</param>
    private static void StopOrphanProcesses(ProcessInfo[] processes)
    {
        foreach (var item in processes)
        {
            if (item.ProcessId <= 0) continue;

            for (var i = 0; i < 50; i++)
            {
                try
                {
                    using var p = Process.GetProcessById(item.ProcessId);
                    if (p.HasExited) break;
                    if (!item.ProcessName.IsNullOrEmpty() && !p.ProcessName.EqualIgnoreCase(item.ProcessName)) break;
                    Thread.Sleep(100);
                }
                catch (ArgumentException) { break; }
            }

            try
            {
                using var p = Process.GetProcessById(item.ProcessId);
                if (!p.HasExited && (item.ProcessName.IsNullOrEmpty() || p.ProcessName.EqualIgnoreCase(item.ProcessName)))
                {
                    p.Kill();
                    p.WaitForExit(3_000);
                }
            }
            catch { }
        }
    }

    private void PersistServices()
    {
        var set = AgentSetting ?? StarAgentSetting.Current;
        set.Services = Manager?.Services;
        set.Save();
    }
    #endregion

    private void DoRefreshLocal(Object state)
    {
        var ai = AgentInfo.GetLocal(true);
        if (ai != null)
        {
            //XTrace.WriteLine("IP: {0}", ai.IP);
            _agentInfo = ai;

            // 如果未取得本机IP，则在较短时间内重新获取
            if (_timer != null)
                _timer.Period = ai.IP.IsNullOrEmpty() ? 5_000 : 60_000;
        }
    }

    private void CheckLocal()
    {
        if (Session is INetSession ns && !ns.Remote.Address.IsLocal()) throw new InvalidOperationException(Localization.Translate("Operations from remote hosts are not allowed."));
    }

    /// <summary>校验本机 RPC 令牌</summary>
    /// <remarks>来源地址判定挡不住同机低权限进程，启停受管服务与改写服务端地址必须带 local.token 中的令牌</remarks>
    /// <param name="token">调用方携带的令牌</param>
    private static void CheckToken(String? token)
    {
        if (!LocalApiToken.Verify(token))
            throw new InvalidOperationException(Localization.Translate("Local RPC requires a valid token. Read local.token from the StarAgent directory, or set STAR_LOCAL_TOKEN."));
    }

    ///// <summary>杀死并启动进程</summary>
    ///// <param name="processId">进程</param>
    ///// <param name="delay">延迟结束的秒数</param>
    ///// <param name="fileName">文件名</param>
    ///// <param name="arguments">参数</param>
    ///// <param name="workingDirectory">工作目录</param>
    ///// <returns></returns>
    //[Api(nameof(KillAndStart))]
    //public Object KillAndStart(Int32 processId, Int32 delay, String fileName, String arguments, String workingDirectory)
    //{
    //    CheckLocal();

    //    var p = Process.GetProcessById(processId);
    //    if (p == null) throw new InvalidOperationException($"无效进程Id[{processId}]");

    //    var name = p.ProcessName;
    //    var pid = 0;

    //    ThreadPool.QueueUserWorkItem(s =>
    //    {
    //        WriteLog("杀死进程 {0}/{1}，等待 {2}秒", processId, p.ProcessName, delay);

    //        if (delay > 0) Thread.Sleep(delay * 1000);

    //        try
    //        {
    //            if (!p.HasExited)
    //            {
    //                p.Kill();
    //                p.WaitForExit(5_000);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            XTrace.WriteException(ex);
    //        }

    //        // 启动进程
    //        if (!fileName.IsNullOrEmpty())
    //        {
    //            WriteLog("启动进程：{0} {1} {2}", fileName, arguments, workingDirectory);

    //            var si = new ProcessStartInfo
    //            {
    //                FileName = fileName,
    //                Arguments = arguments,
    //                WorkingDirectory = workingDirectory,

    //                // false时目前控制台合并到当前控制台，一起退出；
    //                // true时目标控制台独立窗口，不会一起退出；
    //                UseShellExecute = true,
    //            };

    //            var p2 = Process.Start(si);
    //            pid = p2.Id;

    //            WriteLog("应用[{0}]启动成功 PID={1}", p2.ProcessName, p2.Id);
    //        }
    //    });

    //    return new { name, pid };
    //}

    ///// <summary>安装应用服务（星尘代理守护）</summary>
    ///// <param name="service"></param>
    ///// <returns></returns>
    //[Api(nameof(Install))]
    //public ProcessInfo Install(ServiceInfo service)
    //{
    //    XTrace.WriteLine("Install<={0}", service.ToJson());

    //    CheckLocal();

    //    var rs = Manager.Install(service);
    //    if (rs != null)
    //    {
    //        var set = Setting.Current;
    //        set.Services = Manager.Services;
    //        set.Save();
    //    }

    //    return rs;
    //}

    ///// <summary>卸载应用服务</summary>
    ///// <param name="serviceName"></param>
    ///// <returns></returns>
    //[Api(nameof(Uninstall))]
    //public Boolean Uninstall(String serviceName)
    //{
    //    XTrace.WriteLine("Uninstall<={0}", serviceName);

    //    CheckLocal();

    //    var rs = Manager.Uninstall(serviceName, "ServiceUninstall");
    //    if (rs)
    //    {
    //        var set = Setting.Current;
    //        set.Services = Manager.Services;
    //        set.Save();
    //    }

    //    return rs;
    //}
    #endregion

    #region 看门狗
    static ConcurrentDictionary<Int32, DateTime> _dogs = new();
    static TimerX _dogTimer;
    void FeedDog(Int32 pid, Int32 timeout)
    {
        if (pid <= 0 || timeout <= 0) return;

        using var span = DefaultTracer.Instance?.NewSpan(nameof(FeedDog), new { pid, timeout });

        // 更新过期时间，超过该时间未收到心跳，将会重启本应用进程
        _dogs[pid] = DateTime.Now.AddSeconds(timeout);

        _dogTimer ??= new TimerX(CheckDog, Manager, 1000, 15000) { Async = true };
    }

    static void CheckDog(Object state)
    {
        var ks = new List<Int32>();
        var ds = new List<Int32>();
        foreach (var item in _dogs)
        {
            if (item.Value < DateTime.Now)
                ks.Add(item.Key);
            else
            {
                var p = GetProcessById(item.Key);
                if (p == null || p.GetHasExited())
                    ds.Add(item.Key);
            }
        }

        if (ks.Count == 0 && ds.Count == 0) return;

        using var span = DefaultTracer.Instance?.NewSpan(nameof(CheckDog));

        // 重启超时的进程
        foreach (var item in ks)
        {
            try
            {
                var p = GetProcessById(item);
                if (p == null || p.GetHasExited())
                    _dogs.Remove(item);
                else
                {
                    XTrace.WriteLine("Process [{0}/{1}] has not sent a heartbeat for a while and may be hung; preparing to restart.", p.ProcessName, p.Id);

                    var restartSuccess = false;

                    // 优先尝试通过服务重启
                    if (state is ServiceManager manager)
                    {
                        try
                        {
                            // 通过进程ID查找对应的服务控制器
                            var controller = manager.QueryByProcess(item);
                            if (controller != null && !controller.Name.IsNullOrEmpty())
                            {
                                XTrace.WriteLine("Attempting to restart via the service: {0}", controller.Name);
                                span?.AppendTag($"RestartService {controller.Name}");

                                // 使用共同的重启逻辑
                                restartSuccess = InternalRestartService(manager, controller.Name, "Watchdog restart");

                                if (restartSuccess)
                                {
                                    XTrace.WriteLine("Service [{0}] restarted successfully", controller.Name);
                                }
                                else
                                {
                                    XTrace.WriteLine("Failed to restart service [{0}]", controller.Name);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            XTrace.WriteException(ex);
                        }
                    }

                    // 如果服务重启失败，则使用原有的SafetyKill方法
                    if (!restartSuccess)
                    {
                        XTrace.WriteLine("Service restart failed; using SafetyKill to force-terminate process [{0}/{1}]", p.ProcessName, p.Id);
                        span?.AppendTag($"SafetyKill {p.ProcessName}/{p.Id}");
                        p.SafetyKill();
                    }

                    //todo 启动应用。暂时不需要，因为StarAgent会自动启动
                    if (state is ServiceManager manager2) manager2.CheckNow();
                }
            }
            catch (Exception ex)
            {
                _dogs.Remove(item);
                XTrace.WriteException(ex);
            }
        }

        // 删除不存在的进程
        foreach (var item in ds)
        {
            _dogs.Remove(item);
        }
    }

    static Process? GetProcessById(Int32 processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch { }

        return null;
    }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion

    #region 内部方法
    /// <summary>内部服务重启方法</summary>
    /// <param name="manager">服务管理器</param>
    /// <param name="serviceName">服务名称</param>
    /// <param name="reason">重启原因</param>
    /// <returns>是否成功</returns>
    private static Boolean InternalRestartService(ServiceManager manager, String serviceName, String reason = "Restart")
    {
        if (manager == null || serviceName.IsNullOrEmpty()) return false;

        using var span = DefaultTracer.Instance?.NewSpan(nameof(InternalRestartService), new { serviceName, reason });

        try
        {
            // 检查服务是否存在
            var service = manager.Services?.FirstOrDefault(e => e.Name.EqualIgnoreCase(serviceName));
            if (service == null)
            {
                XTrace.WriteLine("Service restart failed: service [{0}] does not exist", serviceName);
                span?.AppendTag("ServiceNotFound");
                return false;
            }

            // 检查服务是否正在运行
            var runningServices = manager.RunningServices;
            var isRunning = runningServices?.Any(e => e.Name.EqualIgnoreCase(serviceName)) == true;

            XTrace.WriteLine("Restarting service [{0}], current state: {1}, reason: {2}", serviceName, isRunning ? L.TLog("Running") : L.TLog("Stopped"), reason);

            if (isRunning)
            {
                // 服务正在运行，先停止服务
                XTrace.WriteLine("Stopping service [{0}]", serviceName);
                span?.AppendTag("StopService");

                var stopResult = manager.Stop(serviceName, reason);
                if (stopResult != true)
                {
                    XTrace.WriteLine("Service restart failed: unable to stop service [{0}]", serviceName);
                    span?.AppendTag("StopFailed");
                    return false;
                }

                XTrace.WriteLine("Service [{0}] stopped successfully; starting it after 1 second", serviceName);
                // 等待服务完全停止
                Thread.Sleep(1000);
            }
            else
            {
                XTrace.WriteLine("Service [{0}] is not running; starting it directly", serviceName);
                span?.AppendTag("DirectStart");
            }

            // 启动服务（无论之前是否运行）
            XTrace.WriteLine("Starting service [{0}]", serviceName);
            span?.AppendTag("StartService");

            var startResult = manager.Start(serviceName);
            if (startResult == true)
            {
                XTrace.WriteLine("Service [{0}] restarted successfully", serviceName);
                span?.AppendTag("Success");
                return true;
            }
            else
            {
                XTrace.WriteLine("Service restart failed: unable to start service [{0}]", serviceName);
                span?.AppendTag("StartFailed");
                return false;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Error while restarting service [{0}]: {1}", serviceName, ex.Message);
            span?.SetError(ex, null);
            return false;
        }
    }
    #endregion
}