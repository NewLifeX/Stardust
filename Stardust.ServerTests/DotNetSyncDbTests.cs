using NewLife;
using NewLife.Serialization;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;
using Xunit.Abstractions;
using XCode;

namespace ServerTest;

/// <summary>数据库集成测试：解析 releases-10.0.json 并落库，验证所有安装类型/OS/指令集全覆盖</summary>
public class DotNetSyncDbTests
{
    private readonly ITestOutputHelper _output;

    public DotNetSyncDbTests(ITestOutputHelper output) => _output = output;

    // ========== 复制自 DotNetSyncService（确保测试与服务端逻辑一致） ==========

    private static (Int32 os, Int32 arch) ParseRid(String rid)
    {
        if (rid.IsNullOrEmpty()) return (0, 0);

        var os = 0;
        var arch = 0;

        if (rid.StartsWithIgnoreCase("win")) os = (Int32)OSKind.Windows;
        else if (rid.StartsWithIgnoreCase("linux-musl")) os = (Int32)OSKind.LinuxMusl;
        else if (rid.StartsWithIgnoreCase("linux")) os = (Int32)OSKind.Linux;
        else if (rid.StartsWithIgnoreCase("osx")) os = (Int32)OSKind.OSX;

        if (rid.EndsWithIgnoreCase("-x64")) arch = (Int32)CpuArch.X64;
        else if (rid.EndsWithIgnoreCase("-x86")) arch = (Int32)CpuArch.X86;
        else if (rid.EndsWithIgnoreCase("-arm64")) arch = (Int32)CpuArch.Arm64;
        else if (rid.EndsWithIgnoreCase("-arm")) arch = (Int32)CpuArch.Arm;

        return (os, arch);
    }

    private static Boolean IsStandardRuntimeFile(String name, String kind)
    {
        return kind switch
        {
            "aspnet" => (name.StartsWith("aspnetcore-runtime-") &&
                         !name.Contains("composite") &&
                         !name.Contains("targeting")) ||
                        name.StartsWith("dotnet-hosting-"),
            "runtime" => name.StartsWith("dotnet-runtime-"),
            "desktop" => name.StartsWith("windowsdesktop-runtime-"),
            "host" => name.StartsWith("dotnet-hosting-"),
            "sdk" => name.StartsWith("dotnet-sdk-"),
            _ => false,
        };
    }

    // ========== 集成测试 ==========

    [Fact]
    public void Sync10_AllKindsAndArchCovered()
    {
        var filePath = "Data/releases-10.0.json";
        Assert.True(File.Exists(filePath), $"测试数据文件不存在: {filePath}");

        var json = File.ReadAllText(filePath);
        var js = JsonParser.Decode(json);
        var releases = js["releases"] as IList<Object>;
        Assert.NotNull(releases);
        Assert.NotEmpty(releases);

        // 清空 10.0 相关已有记录（幂等）
        var existing = DotNetPackage.FindAll(DotNetPackage._.Version.StartsWith("10."));
        foreach (var e in existing)
            e.Delete();
        _output.WriteLine("已清理 {0} 条 10.x 旧记录", existing.Count);

        var totalInserted = 0;
        var kinds = new HashSet<String>();
        var osKinds = new HashSet<OSKind>();
        var archs = new HashSet<CpuArch>();

        foreach (var item in releases)
        {
            var rel = item as IDictionary<String, Object>;
            Assert.NotNull(rel);

            var ver = rel["release-version"] + "";
            // 跳过预览版和RC版（与服务端一致）
            if (ver.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                ver.Contains("rc", StringComparison.OrdinalIgnoreCase)) continue;

            _output.WriteLine("Release {0}", ver);

            // 处理 aspnetcore-runtime（含嵌套的 dotnet-hosting）
            SyncRuntimeFiles(rel, "aspnetcore-runtime", "aspnet", ver, ref totalInserted, kinds, osKinds, archs);
            // 处理 runtime（dotnet-runtime）
            SyncRuntimeFiles(rel, "runtime", "runtime", ver, ref totalInserted, kinds, osKinds, archs);
            // 处理 windowsdesktop
            SyncRuntimeFiles(rel, "windowsdesktop", "desktop", ver, ref totalInserted, kinds, osKinds, archs);

            // 处理 sdk + sdks
            var sdk = rel["sdk"] as IDictionary<String, Object>;
            if (sdk != null)
                SyncSdkFiles(sdk, ver, ref totalInserted, kinds, osKinds, archs);

            var sdks = rel["sdks"] as IList<Object>;
            if (sdks != null)
            {
                foreach (var s in sdks)
                    SyncSdkFiles(s as IDictionary<String, Object>, ver, ref totalInserted, kinds, osKinds, archs);
            }
        }

        _output.WriteLine("");
        _output.WriteLine("=== 总计入库: {0} 条 ===", totalInserted);
        _output.WriteLine("安装类型: [{0}]", kinds.OrderBy(e => e).Join(", "));
        _output.WriteLine("操作系统: [{0}]", osKinds.Select(e => e.ToString()).OrderBy(e => e).Join(", "));
        _output.WriteLine("指令集:   [{0}]", archs.Select(e => e.ToString()).OrderBy(e => e).Join(", "));

        // ===== 断言：5 种安装类型全覆盖 =====
        Assert.Contains("aspnet", kinds);
        Assert.Contains("runtime", kinds);
        Assert.Contains("desktop", kinds);
        Assert.Contains("host", kinds, StringComparer.Ordinal); // dotnet-hosting 必须被识别为 host！
        Assert.Contains("sdk", kinds);

        // ===== 断言：4 种操作系统全覆盖 =====
        Assert.Contains(OSKind.Windows, osKinds);
        Assert.Contains(OSKind.Linux, osKinds);
        Assert.Contains(OSKind.OSX, osKinds);
        // LinuxMusl 在 10.0 中存在（dotnet-runtime-linux-musl-*）
        Assert.Contains(OSKind.LinuxMusl, osKinds); // Alpine/musl 平台必须存在

        // ===== 断言：4 种指令集全覆盖 =====
        Assert.Contains(CpuArch.X64, archs);
        Assert.Contains(CpuArch.X86, archs);
        Assert.Contains(CpuArch.Arm64, archs);
        Assert.Contains(CpuArch.Arm, archs);

        // ===== 数量合理性 =====
        // 10.0 有 ~27 个正式版本 × 每种 kind 多平台 ≈ 合计应远超 200
        Assert.True(totalInserted > 200, String.Format("预期 >200 条，实际 {0} 条", totalInserted));

        _output.WriteLine("");
        _output.WriteLine("=== 全部断言通过 ===");
    }

    private void SyncRuntimeFiles(IDictionary<String, Object> rel, String key, String kind, String version,
        ref Int32 totalInserted, HashSet<String> kinds, HashSet<OSKind> osKinds, HashSet<CpuArch> archs)
    {
        if (!rel.TryGetValue(key, out var v) || v == null) return;

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

            // 优先从 URL 截取文件名
            var idx = url.LastIndexOf('/');
            if (idx >= 0 && idx < url.Length - 1)
            {
                var urlName = url.Substring(idx + 1);
                if (!urlName.IsNullOrEmpty() && urlName.Contains('.'))
                    name = urlName;
            }

            if (!IsStandardRuntimeFile(name, kind)) continue;

            var actualKind = name.StartsWith("dotnet-hosting-") ? "host" : kind;

            var (os, arch) = ParseRid(rid);

            // dotnet-hosting 空 rid 推断（与服务端一致）
            if (os == 0 && name.StartsWith("dotnet-hosting-"))
            {
                if (name.Contains("win", StringComparison.OrdinalIgnoreCase))
                {
                    os = (Int32)OSKind.Windows;
                    arch = (Int32)CpuArch.Any;
                }
            }

            if (os == 0) continue;
            if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip")) continue;

            // 查重后落库
            var pkg = DotNetPackage.Find(DotNetPackage._.Version == version &
                DotNetPackage._.Kind == actualKind &
                DotNetPackage._.OSKind == (OSKind)os &
                DotNetPackage._.Architecture == (CpuArch)arch);
            if (pkg != null) continue;

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
            };
            pkg.Insert();
            totalInserted++;

            kinds.Add(actualKind);
            osKinds.Add((OSKind)os);
            archs.Add((CpuArch)arch);
        }
    }

    private void SyncSdkFiles(IDictionary<String, Object> sdkObj, String version,
        ref Int32 totalInserted, HashSet<String> kinds, HashSet<OSKind> osKinds, HashSet<CpuArch> archs)
    {
        if (sdkObj == null) return;

        var sdkVer = sdkObj["version"] + "";
        if (sdkVer.IsNullOrEmpty()) return;

        var files = sdkObj["files"] as IList<Object>;
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

            // 优先从 URL 截取文件名
            var idx = url.LastIndexOf('/');
            if (idx >= 0 && idx < url.Length - 1)
            {
                var urlName = url.Substring(idx + 1);
                if (!urlName.IsNullOrEmpty() && urlName.Contains('.'))
                    name = urlName;
            }

            if (!IsStandardRuntimeFile(name, "sdk")) continue;

            var (os, arch) = ParseRid(rid);
            if (os == 0) continue;
            if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip")) continue;

            var pkg = DotNetPackage.Find(DotNetPackage._.Version == version &
                DotNetPackage._.Kind == "sdk" &
                DotNetPackage._.OSKind == (OSKind)os &
                DotNetPackage._.Architecture == (CpuArch)arch);
            if (pkg != null) continue;

            pkg = new DotNetPackage
            {
                Version = version,
                Kind = "sdk",
                OSKind = (OSKind)os,
                Architecture = (CpuArch)arch,
                FileName = name,
                Source = url,
                FileHash = hash,
                Enable = true,
                Channel = NodeChannels.Release,
            };
            pkg.Insert();
            totalInserted++;

            kinds.Add("sdk");
            osKinds.Add((OSKind)os);
            archs.Add((CpuArch)arch);
        }
    }
}
