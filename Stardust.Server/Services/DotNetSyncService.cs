using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>定期从Microsoft官网同步.NET运行时安装包信息到DotNetPackage表</summary>
public class DotNetSyncService : IHostedService
{
    private readonly ITracer _tracer;
    private readonly StarServerSetting _setting;
    private TimerX _timer;
    private Int32 _lastPeriod;
    private readonly HttpClient _http;

    public DotNetSyncService(ITracer tracer, StarServerSetting setting)
    {
        _tracer = tracer;
        _setting = setting;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 不管配置是否禁用，都启动定时器。DoSync内判断配置，每次执行后更新周期
        _timer = new TimerX(DoSync, null, 5_000, 60_000)
        {
            Async = true,
        };
        _lastPeriod = _setting.DotNetSyncPeriod;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();
        return Task.CompletedTask;
    }

    /// <summary>更新定时器周期，根据当前配置动态调整</summary>
    private void UpdateTimer()
    {
        var period = _setting.DotNetSyncPeriod;
        if (_lastPeriod == period) return;
        _lastPeriod = period;

        if (period <= 0)
        {
            // 禁用时改为每分钟检查，确保配置变化后能快速恢复
            _timer.Period = 60_000;
            XTrace.WriteLine("DotNetSyncService 已暂停，配置周期={0}", period);
        }
        else
        {
            _timer.Period = period * 1000;
            XTrace.WriteLine("DotNetSyncService 周期已更新为 {0}秒", period);
        }
    }

    private async void DoSync(Object state)
    {
        var period = _setting.DotNetSyncPeriod;
        if (period <= 0)
        {
            // 禁用时不执行同步，但每分钟会进来检查配置变化
            UpdateTimer();
            return;
        }

        var url = _setting.DotNetSyncUrl;
        if (url.IsNullOrEmpty()) return;

        using var span = _tracer?.NewSpan(nameof(DotNetSyncService));
        try
        {
            // 扫描多个主版本
            var majors = new[] { 6, 7, 8, 9, 10 };
            foreach (var major in majors)
            {
                var u = url.Replace("{major}", major + "");
                await SyncMajor(major + ".0", u, span);
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.Log.Error("DotNetSyncService.Error {0}", ex.Message);
        }
        finally
        {
            // 每次执行完后检查配置有无变化，动态更新定时器周期
            UpdateTimer();
        }
    }

    private async Task SyncMajor(String versionPrefix, String url, ISpan span)
    {
        XTrace.WriteLine("DotNetSyncService.Sync {0}", url);

        var json = await _http.GetStringAsync(url);
        var js = JsonParser.Decode(json);
        var releases = js["releases"] as IList<Object>;
        if (releases == null) return;

        foreach (var item in releases)
        {
            var rel = item as IDictionary<String, Object>;
            if (rel == null) continue;

            var ver = rel["release-version"] + "";
            // 跳过预览版和RC版
            if (ver.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                ver.Contains("rc", StringComparison.OrdinalIgnoreCase)) continue;

            // 处理 aspnetcore-runtime → Kind=aspnet
            var allPkgs = DotNetPackage.FindAllByVersion(ver);
            SyncRuntime(rel, "aspnetcore-runtime", "aspnet", ver, allPkgs);
            // 处理 runtime → Kind=runtime（dotnet-runtime 基础运行时）
            SyncRuntime(rel, "runtime", "runtime", ver, allPkgs);
            // 处理 windowsdesktop → Kind=desktop
            SyncRuntime(rel, "windowsdesktop", "desktop", ver, allPkgs);
            // dotnet-hosting 不在顶级 key 中，而是在 aspnetcore-runtime.files 的最后一项，由 SyncRuntime 内识别

            // 处理 sdk（单个主 SDK，如 10.0.301）→ Kind=sdk
            SyncSdk(rel["sdk"] as IDictionary<String, Object>, ver);
            // 处理 sdks（多个 SDK 版本数组，如 [10.0.301, 10.0.109]）→ Kind=sdk
            var sdks = rel["sdks"] as IList<Object>;
            if (sdks != null)
            {
                foreach (var s in sdks)
                    SyncSdk(s as IDictionary<String, Object>, ver);
            }
        }
    }

    private void SyncRuntime(IDictionary<String, Object> rel, String key, String kind, String version,
        IList<DotNetPackage> allPkgs)
    {
        if (!rel.TryGetValue(key, out var v) || v == null) return;

        // Microsoft JSON 结构: { "aspnetcore-runtime": { "files": [...] } }
        var runtime = v as IDictionary<String, Object>;
        if (runtime == null) return;

        var files = runtime["files"] as IList<Object>;
        if (files == null) return;

        foreach (var item in files)
        {
            var file = item as IDictionary<String, Object>;
            if (file == null) continue;

            var name = file["name"] + "";
            var url = file["url"] + "";
            var rid = file["rid"] + "";
            var hash = file["hash"] + "";

            if (name.IsNullOrEmpty() || url.IsNullOrEmpty()) continue;

            // 优先从 URL 截取文件名（JSON name 字段可能缺版本号，如 dotnet-hosting-win.exe）
            var idx = url.LastIndexOf('/');
            if (idx >= 0 && idx < url.Length - 1)
            {
                var urlName = url.Substring(idx + 1);
                if (!urlName.IsNullOrEmpty() && urlName.Contains('.'))
                    name = urlName;
            }

            // 过滤：仅保留标准运行时安装包，排除 composite/targeting-pack 等变体
            if (!IsStandardRuntimeFile(name, kind)) continue;

            // 根据文件名确定实际 Kind（dotnet-hosting 嵌套在 aspnetcore-runtime 中，应记为 host）
            var actualKind = name.StartsWith("dotnet-hosting-") ? "host" : kind;

            // 解析 RID 为 OSKind + CpuArch
            var (os, arch) = ParseRid(rid);

            // dotnet-hosting-win.exe 的 rid 为空字符串，根据文件名推断为 Windows/Any
            if (os == 0 && name.StartsWith("dotnet-hosting-"))
            {
                if (name.Contains("win", StringComparison.OrdinalIgnoreCase))
                {
                    os = (Int32)OSKind.Windows;
                    arch = (Int32)CpuArch.Any;
                }
            }

            if (os == 0) continue;

            // Windows 上跳过 .zip（只保留 .exe 安装包）
            if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip")) continue;

            // 在调用方传入的内存列表中查找（无需再次查库）
            var pkg = allPkgs.FirstOrDefault(e =>
                e.Kind == actualKind &&
                e.OSKind == (OSKind)os &&
                e.Architecture == (CpuArch)arch);

            if (pkg == null)
            {
                pkg = new DotNetPackage
                {
                    Version = version,
                    Kind = actualKind,
                    OSKind = (OSKind)os,
                    Architecture = (CpuArch)arch,
                    FileName = name,
                    Source = url,
                    FileHash = hash,
                    Enable = true,
                    Channel = NodeChannels.Release,
                    Force = false,
                };
                pkg.Insert();
            }
            // 手动上传的记录（Source 指向 Cube 附件）不被自动同步覆盖
            else if (!pkg.Source.StartsWith("/cube/file"))
            {
                pkg.FileName = name;
                pkg.Source = url;
                pkg.FileHash = hash;
                pkg.Update();
            }
        }
    }

    /// <summary>同步 SDK 安装包（单个 SDK 对象或 sdks 数组中的每个元素）</summary>
    /// <param name="sdkObj">sdk 对象，含 version / files 字段</param>
    /// <param name="runtimeVersion">所属运行时版本号（仅日志用）</param>
    private void SyncSdk(IDictionary<String, Object> sdkObj, String runtimeVersion)
    {
        if (sdkObj == null) return;

        var sdkVersion = sdkObj["version"] + "";
        if (sdkVersion.IsNullOrEmpty()) return;

        var files = sdkObj["files"] as IList<Object>;
        if (files == null) return;

        //XTrace.WriteLine("DotNetSyncService.SyncSdk {0} (runtime={1})", sdkVersion, runtimeVersion);

        foreach (var item in files)
        {
            var file = item as IDictionary<String, Object>;
            if (file == null) continue;

            var name = file["name"] + "";
            var url = file["url"] + "";
            var rid = file["rid"] + "";
            var hash = file["hash"] + "";

            if (name.IsNullOrEmpty() || url.IsNullOrEmpty()) continue;

            // 优先从 URL 截取文件名
            var idx = url.LastIndexOf('/');
            if (idx >= 0 && idx < url.Length - 1)
            {
                var urlName = url.Substring(idx + 1);
                if (!urlName.IsNullOrEmpty() && urlName.Contains('.'))
                    name = urlName;
            }

            // 过滤：仅保留 dotnet-sdk-* 安装包
            if (!IsStandardRuntimeFile(name, "sdk")) continue;

            // 解析 RID
            var (os, arch) = ParseRid(rid);
            if (os == 0) continue;

            // Windows 上跳过 .zip（只保留 .exe 安装包）
            if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip")) continue;

            // 按运行时版本查已有 SDK 记录（文件名中已含 SDK 版本号）
            var sdkPkgs = DotNetPackage.FindAllByVersion(runtimeVersion);
            var pkg = sdkPkgs.FirstOrDefault(e =>
                e.Kind == "sdk" &&
                e.OSKind == (OSKind)os &&
                e.Architecture == (CpuArch)arch);

            if (pkg == null)
            {
                pkg = new DotNetPackage
                {
                    Version = runtimeVersion,
                    Kind = "sdk",
                    OSKind = (OSKind)os,
                    Architecture = (CpuArch)arch,
                    FileName = name,
                    Source = url,
                    FileHash = hash,
                    Enable = true,
                    Channel = NodeChannels.Release,
                    Force = false,
                };
                pkg.Insert();
            }
            // 手动上传的记录不被自动同步覆盖
            else if (!pkg.Source.StartsWith("/cube/file"))
            {
                pkg.FileName = name;
                pkg.Source = url;
                pkg.FileHash = hash;
                pkg.Update();
            }
        }
    }

    /// <summary>判断文件是否为标准运行时安装包（排除 composite/targeting-pack 等变体）</summary>
    /// <remarks>
    /// 注意：dotnet-hosting-win.exe 不在独立的 "dotnet-hosting" 顶级 key 中，
    /// 而是作为最后一项出现在 "aspnetcore-runtime".files 数组内。
    /// 因此处理 "aspnet" kind 时也需要识别 dotnet-hosting-* 前缀。
    /// </remarks>
    private static Boolean IsStandardRuntimeFile(String name, String kind)
    {
        return kind switch
        {
            // aspnetcore-runtime 下：标准运行时 + 嵌套的 dotnet-hosting（排除 composite/targeting）
            "aspnet" => (name.StartsWith("aspnetcore-runtime-") &&
                         !name.Contains("composite") &&
                         !name.Contains("targeting")) ||
                        name.StartsWith("dotnet-hosting-"),
            // runtime 下：dotnet-runtime-*（排除 dotnet-apphost-pack-*）
            "runtime" => name.StartsWith("dotnet-runtime-"),
            "desktop" => name.StartsWith("windowsdesktop-runtime-"),
            "host" => name.StartsWith("dotnet-hosting-"),
            "sdk" => name.StartsWith("dotnet-sdk-"),
            _ => false,
        };
    }

    /// <summary>解析RID为OSKind和CpuArch</summary>
    private static (Int32 os, Int32 arch) ParseRid(String rid)
    {
        if (rid.IsNullOrEmpty()) return (0, 0);

        var os = 0;
        var arch = 0;

        // OS 判断
        if (rid.StartsWithIgnoreCase("win")) os = (Int32)OSKind.Windows;
        else if (rid.StartsWithIgnoreCase("linux-musl")) os = (Int32)OSKind.LinuxMusl;
        else if (rid.StartsWithIgnoreCase("linux")) os = (Int32)OSKind.Linux;
        else if (rid.StartsWithIgnoreCase("osx")) os = (Int32)OSKind.OSX;

        // Arch 判断
        if (rid.EndsWithIgnoreCase("-x64")) arch = (Int32)CpuArch.X64;
        else if (rid.EndsWithIgnoreCase("-x86")) arch = (Int32)CpuArch.X86;
        else if (rid.EndsWithIgnoreCase("-arm64")) arch = (Int32)CpuArch.Arm64;
        else if (rid.EndsWithIgnoreCase("-arm")) arch = (Int32)CpuArch.Arm;

        return (os, arch);
    }
}
