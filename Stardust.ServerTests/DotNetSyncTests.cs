using NewLife;
using NewLife.Serialization;
using Stardust.Data.Nodes;
using Stardust.Models;
using Xunit;
using Xunit.Abstractions;

namespace ServerTest;

public class DotNetSyncTests
{
    private readonly ITestOutputHelper _output;

    public DotNetSyncTests(ITestOutputHelper output) => _output = output;

    /// <summary>复制自 DotNetSyncService 的解析逻辑，确保与服务端一致</summary>
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

    public static IEnumerable<Object[]> GetVersionData()
    {
        var versions = new[] { "6.0", "7.0", "8.0", "9.0", "10.0" };
        foreach (var v in versions)
        {
            var file = $"Data/releases-{v}.json";
            if (File.Exists(file)) yield return new Object[] { v, file };
        }
    }

    [Theory]
    [MemberData(nameof(GetVersionData))]
    public void ParseReleases(String version, String filePath)
    {
        _output.WriteLine("=== Testing {0} ===", version);

        var json = File.ReadAllText(filePath);
        var js = JsonParser.Decode(json);
        Assert.NotNull(js);

        var releases = js["releases"] as IList<Object>;
        Assert.NotNull(releases);
        Assert.NotEmpty(releases);

        var aspnetCount = 0;
        var runtimeCount = 0;
        var desktopCount = 0;
        var hostingCount = 0;
        var sdkCount = 0;
        var totalFiles = 0;
        var excludedCount = 0; // composite/targeting-pack/apphost-pack/zip 等被过滤掉的文件

        // 局部方法：处理 SDK 文件（sdk 和 sdks 共用）
        void ProcessSdkFiles(IDictionary<String, Object> sdkObj, String ver, String source)
        {
            if (sdkObj == null) return;
            var sdkVer = sdkObj["version"] + "";
            if (sdkVer.IsNullOrEmpty()) return;

            var files = sdkObj["files"] as IList<Object>;
            if (files == null) return;

            _output.WriteLine("  SDK {0} (from {1}):", sdkVer, source);

            foreach (var f in files)
            {
                var file = f as IDictionary<String, Object>;
                Assert.NotNull(file);

                var name = file["name"] + "";
                var rid = file["rid"] + "";

                if (name.StartsWith("dotnet-sdk-"))
                {
                    var (os, arch) = ParseRid(rid);
                    // Windows 上跳过 .zip
                    if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip"))
                    {
                        _output.WriteLine($"    [skip] {name} (.zip)");
                        excludedCount++;
                        continue;
                    }
                    if (os == 0 || arch == 0)
                    {
                        _output.WriteLine($"    [skip] {name} rid=[{rid}] (unparsable)");
                        excludedCount++;
                        continue;
                    }
                    _output.WriteLine($"    sdk: {rid} -> OS={os}({(OSKind)os}) Arch={arch}({(CpuArch)arch})");
                    sdkCount++;
                    totalFiles++;
                }
            }
        }

        foreach (var item in releases)
        {
            var rel = item as IDictionary<String, Object>;
            Assert.NotNull(rel);

            var ver = rel["release-version"] + "";
            _output.WriteLine("  Release: {0}", ver);

            // 测试 aspnetcore-runtime 结构
            if (rel.TryGetValue("aspnetcore-runtime", out var ar) && ar != null)
            {
                var runtime = ar as IDictionary<String, Object>;
                Assert.NotNull(runtime); // 必须是对象，不是数组

                var files = runtime["files"] as IList<Object>;
                Assert.NotNull(files);
                Assert.NotEmpty(files);

                foreach (var f in files)
                {
                    var file = f as IDictionary<String, Object>;
                    Assert.NotNull(file);

                    var name = file["name"] + "";
                    var rid = file["rid"] + "";
                    var url = file["url"] + "";

                    // 标准 aspnetcore 运行时（排除 composite/targeting-pack）
                    if (name.StartsWith("aspnetcore-runtime-") &&
                        !name.Contains("composite") &&
                        !name.Contains("targeting"))
                    {
                        var (os, arch) = ParseRid(rid);
                        if (os == 0 || arch == 0)
                        {
                            _output.WriteLine($"    [skip] {name} rid=[{rid}] (unparsable)");
                            excludedCount++;
                            continue;
                        }
                        // Windows 上跳过 .zip
                        if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip"))
                        {
                            _output.WriteLine($"    [skip] {name} (.zip)");
                            excludedCount++;
                            continue;
                        }
                        _output.WriteLine($"    aspnet: {rid} -> OS={os}({(OSKind)os}) Arch={arch}({(CpuArch)arch})");
                        aspnetCount++;
                        totalFiles++;
                    }
                    // dotnet-hosting 嵌套在 aspnetcore-runtime.files 中
                    else if (name.StartsWith("dotnet-hosting-"))
                    {
                        _output.WriteLine($"    hosting: {name} rid=[{rid}]");
                        hostingCount++;
                        totalFiles++;
                    }
                    else if (name.StartsWith("aspnetcore-"))
                    {
                        // 记录被过滤掉的变体文件
                        _output.WriteLine($"    [skip] {name} (composite/targeting)");
                        excludedCount++;
                    }
                }
            }

            // 测试 runtime 结构（dotnet-runtime 基础运行时）
            if (rel.TryGetValue("runtime", out var rt) && rt != null)
            {
                var baseRt = rt as IDictionary<String, Object>;
                Assert.NotNull(baseRt);

                var files = baseRt["files"] as IList<Object>;
                Assert.NotNull(files);
                Assert.NotEmpty(files);

                foreach (var f in files)
                {
                    var file = f as IDictionary<String, Object>;
                    Assert.NotNull(file);

                    var name = file["name"] + "";
                    var rid = file["rid"] + "";

                    // 只关注 dotnet-runtime-*（排除 dotnet-apphost-pack-*）
                    if (name.StartsWith("dotnet-runtime-"))
                    {
                        var (os, arch) = ParseRid(rid);
                        if (os == 0 || arch == 0)
                        {
                            _output.WriteLine($"    [skip] {name} rid=[{rid}] (unparsable)");
                            excludedCount++;
                            continue;
                        }
                        // Windows 上跳过 .zip
                        if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip"))
                        {
                            _output.WriteLine($"    [skip] {name} (.zip)");
                            excludedCount++;
                            continue;
                        }
                        _output.WriteLine($"    runtime: {rid} -> OS={os}({(OSKind)os}) Arch={arch}({(CpuArch)arch})");
                        runtimeCount++;
                        totalFiles++;
                    }
                    else
                    {
                        _output.WriteLine($"    [skip] {name} (apphost-pack)");
                        excludedCount++;
                    }
                }
            }

            // 测试 windowsdesktop 结构
            if (rel.TryGetValue("windowsdesktop", out var wd) && wd != null)
            {
                var desktop = wd as IDictionary<String, Object>;
                Assert.NotNull(desktop);

                var files = desktop["files"] as IList<Object>;
                Assert.NotNull(files);
                Assert.NotEmpty(files);

                foreach (var f in files)
                {
                    var file = f as IDictionary<String, Object>;
                    Assert.NotNull(file);

                    var name = file["name"] + "";
                    var rid = file["rid"] + "";

                    if (name.StartsWith("windowsdesktop-runtime-"))
                    {
                        // Windows 上跳过 .zip
                        var (os, arch) = ParseRid(rid);
                        if (os == (Int32)OSKind.Windows && name.EndsWithIgnoreCase(".zip"))
                        {
                            _output.WriteLine($"    [skip] {name} (.zip)");
                            excludedCount++;
                            continue;
                        }
                        if (os == 0 || arch == 0)
                        {
                            _output.WriteLine($"    [skip] {name} rid=[{rid}] (unparsable)");
                            excludedCount++;
                            continue;
                        }
                        _output.WriteLine($"    desktop: {rid} -> OS={os}({(OSKind)os}) Arch={arch}({(CpuArch)arch})");
                        desktopCount++;
                        totalFiles++;
                    }
                }
            }

            // 测试 sdk 结构（单个主 SDK）
            var sdk = rel["sdk"] as IDictionary<String, Object>;
            ProcessSdkFiles(sdk, ver, "sdk");
            // 测试 sdks 结构（多个 SDK 版本）
            var sdks = rel["sdks"] as IList<Object>;
            if (sdks != null)
            {
                foreach (var s in sdks)
                    ProcessSdkFiles(s as IDictionary<String, Object>, ver, "sdks");
            }
        }

        _output.WriteLine("  === Summary: aspnet={0}, runtime={1}, desktop={2}, hosting={3}, sdk={4}, excluded={5}, total={6}",
            aspnetCount, runtimeCount, desktopCount, hostingCount, sdkCount, excludedCount, totalFiles);

        Assert.True(aspnetCount > 0, $"版本 {version} 应有 aspnetcore-runtime 记录");
    }

    [Theory]
    [InlineData("win-x64", OSKind.Windows, CpuArch.X64)]
    [InlineData("win-x86", OSKind.Windows, CpuArch.X86)]
    [InlineData("win-arm64", OSKind.Windows, CpuArch.Arm64)]
    [InlineData("linux-x64", OSKind.Linux, CpuArch.X64)]
    [InlineData("linux-arm", OSKind.Linux, CpuArch.Arm)]
    [InlineData("linux-arm64", OSKind.Linux, CpuArch.Arm64)]
    [InlineData("linux-musl-x64", OSKind.LinuxMusl, CpuArch.X64)]
    [InlineData("linux-musl-arm64", OSKind.LinuxMusl, CpuArch.Arm64)]
    [InlineData("osx-x64", OSKind.OSX, CpuArch.X64)]
    [InlineData("osx-arm64", OSKind.OSX, CpuArch.Arm64)]
    public void ParseRid_Success(String rid, OSKind expectedOs, CpuArch expectedArch)
    {
        var (os, arch) = ParseRid(rid);
        Assert.Equal((Int32)expectedOs, os);
        Assert.Equal((Int32)expectedArch, arch);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unknown-xyz")]
    public void ParseRid_Unknown(String rid)
    {
        var (os, arch) = ParseRid(rid);
        Assert.Equal(0, os);
        Assert.Equal(0, arch);
    }

    [Theory]
    [InlineData("aspnetcore-runtime-10.0.9-linux-x64.tar.gz", "aspnet", true)]
    [InlineData("aspnetcore-runtime-composite-10.0.9-linux-x64.tar.gz", "aspnet", false)]
    [InlineData("aspnetcore-targeting-pack-10.0.9-linux-x64.tar.gz", "aspnet", false)]
    [InlineData("dotnet-hosting-10.0.9-win.exe", "aspnet", true)]    // 嵌套在 aspnetcore-runtime key 中
    [InlineData("windowsdesktop-runtime-10.0.9-win-x64.exe", "desktop", true)]
    [InlineData("windowsdesktop-runtime-10.0.9-win-x64.zip", "desktop", true)]
    [InlineData("dotnet-hosting-10.0.9-win.exe", "host", true)]
    [InlineData("dotnet-runtime-10.0.9-linux-x64.tar.gz", "runtime", true)]
    [InlineData("dotnet-apphost-pack-linux-x64.tar.gz", "runtime", false)]
    [InlineData("dotnet-apphost-pack-win-x64.zip", "runtime", false)]
    [InlineData("dotnet-sdk-10.0.301-win-x64.exe", "sdk", true)]
    [InlineData("dotnet-sdk-10.0.301-linux-x64.tar.gz", "sdk", true)]
    [InlineData("dotnet-sdk-10.0.109-win-x86.exe", "sdk", true)]
    [InlineData("dotnet-sdk-linux-x64.tar.gz", "sdk", true)]
    [InlineData("dotnet-sdk-linux-x64.tar.gz", "aspnet", false)]
    [InlineData("dotnet-sdk-linux-x64.tar.gz", "runtime", false)]
    public void IsStandardRuntimeFile_Filter(String name, String kind, Boolean expected)
    {
        // 使用反射测试私有方法（与服务端过滤逻辑一致）
        var method = typeof(Stardust.Server.Services.DotNetSyncService)
            .GetMethod("IsStandardRuntimeFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var result = (Boolean)method.Invoke(null, new Object[] { name, kind });
        Assert.Equal(expected, result);
    }
}
