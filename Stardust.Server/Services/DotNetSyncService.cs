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
    private readonly HttpClient _http;

    public DotNetSyncService(ITracer tracer, StarServerSetting setting)
    {
        _tracer = tracer;
        _setting = setting;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var period = _setting.DotNetSyncPeriod;
        if (period <= 0) return Task.CompletedTask;

        // 首次延迟5秒执行，之后按周期
        _timer = new TimerX(DoSync, null, 5_000, period * 1000)
        {
            Async = true,
        };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();
        return Task.CompletedTask;
    }

    private async void DoSync(Object state)
    {
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
            SyncRuntime(rel, "aspnetcore-runtime", "aspnet", ver);
            // 处理 windowsdesktop-runtime → Kind=desktop
            SyncRuntime(rel, "windowsdesktop-runtime", "desktop", ver);
            // 处理 dotnet-hosting → Kind=host
            SyncRuntime(rel, "dotnet-hosting", "host", ver);
        }
    }

    private void SyncRuntime(IDictionary<String, Object> rel, String key, String kind, String version)
    {
        if (!rel.TryGetValue(key, out var v) || v == null) return;

        var files = v as IList<Object>;
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

            // 解析 RID 为 OSKind + CpuArch
            var (os, arch) = ParseRid(rid);
            if (os == 0) continue;

            // 查找或创建 DotNetPackage
            var list = DotNetPackage.FindAll();
            var pkg = list.FirstOrDefault(e =>
                e.Version == version &&
                e.Kind == kind &&
                e.OSKind == (OSKind)os &&
                e.Architecture == (CpuArch)arch);

            if (pkg == null)
            {
                pkg = new DotNetPackage
                {
                    Version = version,
                    Kind = kind,
                    OSKind = (OSKind)os,
                    Architecture = (CpuArch)arch,
                    FileName = name,
                    Source = url,
                    FileHash = hash,
                    Enable = true,
                    AutoImport = true,
                    Channel = NodeChannels.Release,
                    Force = false,
                };
                pkg.Insert();
            }
            else if (pkg.AutoImport)
            {
                // 自动导入的记录，仅更新源信息
                pkg.FileName = name;
                pkg.Source = url;
                pkg.FileHash = hash;
                pkg.Update();
            }
        }
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
