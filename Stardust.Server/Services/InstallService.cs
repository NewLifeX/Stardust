using System.Text;
using Microsoft.AspNetCore.Http;
using NewLife;
using NewLife.Caching;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode;

namespace Stardust.Server.Services;

/// <summary>安装引导服务。为安装页面、安装脚本与原生 Agent 引导安装提供统一的选包与下载地址</summary>
/// <remarks>
/// 安装页面、安装脚本与 node/bootstrap 共用同一套选包逻辑：先取后台维护的产品版本与发布包，
/// 服务端未维护对应包时回退公共镜像（AgentDownloadBaseUrl），保证页面与引导安装开箱可用。
/// 对外的下载地址统一指回本站 /install/download/{kind}/{id}，
/// 由服务端在用户点击或脚本执行时现场签发短时令牌，因此界面与脚本里不会出现会过期的签名地址。
/// </remarks>
public class InstallService
{
    /// <summary>完整版 StarAgent 的产品编码</summary>
    private const String AgentProductCode = "StarAgent";

    /// <summary>清单缓存时间（秒）。安装包变更不频繁，短时缓存避免公开页面频繁查库</summary>
    private const Int32 CatalogExpire = 60;

    /// <summary>页面展示的运行时保留的主版本数。只挑最近几个大版本，避免列表过长</summary>
    private const Int32 RuntimeMajorKeep = 2;

    private readonly StarServerSetting _setting;
    private readonly ICacheProvider _cacheProvider;
    private readonly DownloadTokenService _tokenService;

    #region 构造
    /// <summary>实例化安装引导服务</summary>
    /// <param name="setting">服务端配置</param>
    /// <param name="cacheProvider">缓存提供者，分布式部署时为 Redis</param>
    /// <param name="tokenService">下载令牌服务</param>
    public InstallService(StarServerSetting setting, ICacheProvider cacheProvider, DownloadTokenService tokenService)
    {
        _setting = setting;
        _cacheProvider = cacheProvider;
        _tokenService = tokenService;
    }
    #endregion

    #region 清单
    /// <summary>获取安装引导清单</summary>
    /// <param name="server">服务端地址，下发给客户端的注册地址</param>
    /// <returns>安装引导清单</returns>
    public InstallManifest GetManifest(String server)
    {
        var catalog = GetCatalog();

        return new InstallManifest
        {
            Server = server,
            UpdateTime = catalog.UpdateTime,
            Versions = catalog.Versions,
            Runtimes = catalog.Runtimes,
            Mirror = catalog.Mirror,
        };
    }

    /// <summary>获取安装引导页面内容</summary>
    /// <returns>页面HTML，读取失败时返回空</returns>
    public String? GetPage() => ReadResource("install.html");
    #endregion

    #region 引导安装
    /// <summary>获取原生 Agent 的引导安装信息（.NET 运行时 + 完整版 StarAgent）</summary>
    /// <remarks>原生 Agent 只做采集与指令响应，需要完整能力时由它下载安装运行时与 .NET 版 StarAgent。
    /// 选包规则与安装页一致：最新启用版本优先，按节点系统与架构匹配，忽略节点自身产品编码。</remarks>
    /// <param name="node">节点</param>
    /// <returns>引导安装信息；非原生 Agent，或服务端未维护对应安装包时返回null</returns>
    public BootstrapModel? GetBootstrap(Node node)
    {
        if (node == null || !IsNativeAgent(node.ProductCode)) return null;

        var os = OSKindHelper.ToOSKind(node.OSKind);
        if (!Enum.TryParse<CpuArch>(node.Architecture, true, out var arch)) arch = CpuArch.Any;

        // 产品编码固定为完整版 StarAgent：原生 Agent 的编码是 StarAgent-Go/Rust/Cpp，直接匹配会落空
        foreach (var release in QueryReleases(50))
        {
            var packages = ProductPackage.FindAll(ProductPackage._.ReleaseId == release.Id);
            if (packages == null || packages.Count == 0) continue;

            var models = packages.Select(e => ToPackage(e, release.Version)).ToList();
            var pkg = SelectPackage(models, os, arch, null, null);
            if (pkg == null) continue;

            var agent = packages.FirstOrDefault(e => e.Id == pkg.Id);
            if (agent == null) continue;

            var runtime = FindRuntimeEntity(os, arch, pkg.Runtime);

            return new BootstrapModel
            {
                Version = release.Version,
                Code = node.Code,
                // 运行时版本取自安装包自身，避免与完整版版本号混淆
                Runtime = ToBootstrapPackage(runtime),
                Agent = ToBootstrapPackage(agent, release.Version),
            };
        }

        return null;
    }
    #endregion

    #region 下载
    /// <summary>解析安装包下载地址。相对地址补全为绝对地址并附加短时令牌</summary>
    /// <param name="kind">包类型。agent/runtime</param>
    /// <param name="id">包编号</param>
    /// <param name="server">服务端基础地址</param>
    /// <returns>可直接302跳转的下载地址；包不存在、未启用或没有下载源时返回null</returns>
    public String? ResolveDownloadUrl(String? kind, Int32 id, String server)
    {
        if (id <= 0) return null;

        var source = "";
        if (kind.EqualIgnoreCase("agent"))
        {
            var pkg = ProductPackage.FindById(id);
            if (pkg == null) return null;

            // 只放行启用版本下的包，避免匿名下载入口被用来枚举附件
            var release = ProductRelease.FindById(pkg.ReleaseId);
            if (release == null || !release.Enable) return null;
            if (!release.ProductCode.IsNullOrEmpty() && !release.ProductCode.EqualIgnoreCase(AgentProductCode)) return null;

            source = pkg.Source;
        }
        else if (kind.EqualIgnoreCase("runtime"))
        {
            var pkg = DotNetPackage.FindById(id);
            if (pkg == null || !pkg.Enable) return null;

            source = pkg.Source;
        }
        else
        {
            return null;
        }

        if (source.IsNullOrEmpty()) return null;

        return DownloadSourceHelper.Absolutize(source, server, url => _tokenService.AppendToken(url, _setting.AttachmentTokenExpire));
    }
    #endregion

    #region 安装脚本
    /// <summary>生成安装脚本</summary>
    /// <param name="os">目标系统。linux/windows</param>
    /// <param name="arch">目标架构。x64/arm64/arm/loongarch64/mips64</param>
    /// <param name="runtime">.NET主版本号，如8/10；为空表示不限制</param>
    /// <param name="version">StarAgent版本，为空取最新版本</param>
    /// <param name="server">服务端地址，安装后注册的目标地址</param>
    /// <returns>脚本内容；没有可用安装包时返回null</returns>
    public String? BuildScript(String? os, String? arch, String? runtime, String? version, String server)
    {
        if (server.IsNullOrEmpty()) throw new ArgumentException("server is required");
        if (server.Contains('\'') || server.Contains('"') || server.Contains('\n') || server.Contains('\r'))
            throw new ArgumentException("Invalid server address");

        var osKind = ParseOS(os);
        if (osKind == OSKind.Any) return null;

        var cpuArch = ParseArch(arch);
        var isWindows = osKind == OSKind.Windows;
        var catalog = GetCatalog();

        var release = version.IsNullOrEmpty()
            ? catalog.Versions.FirstOrDefault()
            : catalog.Versions.FirstOrDefault(e => e.Version.EqualIgnoreCase(version));

        // 优先使用后台维护的安装包，缺失时回退公共镜像
        var pkg = release == null ? null : SelectPackage(release.Packages, osKind, cpuArch, runtime, isWindows);
        pkg ??= SelectPackage(catalog.Mirror, osKind, cpuArch, runtime, isWindows);
        if (pkg == null || pkg.Url.IsNullOrEmpty()) return null;

        // 运行时版本以安装包自身的运行时就近匹配，找不到则退回该平台最新运行时
        var runtimePkg = FindRuntimeEntity(osKind, cpuArch, pkg.Runtime);

        var values = new Dictionary<String, String>
        {
            ["server"] = server.TrimEnd('/'),
            ["version"] = pkg.Version ?? "",
            ["agentUrl"] = Absolutize(pkg.Url, server),
            ["agentFile"] = pkg.FileName ?? "staragent.tar.gz",
            ["agentHash"] = pkg.FileHash ?? "",
            ["runtimeUrl"] = runtimePkg == null ? "" : Absolutize(ToPackage(runtimePkg).Url + "", server),
            ["runtimeFile"] = runtimePkg?.FileName ?? "",
            ["runtimeHash"] = runtimePkg?.FileHash ?? "",
            ["runtimeBlock"] = BuildRuntimeBlock(runtimePkg, isWindows),
        };

        var template = ReadResource(isWindows ? "install-windows.tpl" : "install-linux.tpl");
        if (template.IsNullOrEmpty()) return null;

        var sb = new StringBuilder(template);
        foreach (var item in values) sb.Replace("{{" + item.Key + "}}", item.Value);

        return sb.ToString();
    }

    /// <summary>生成运行时安装片段</summary>
    /// <param name="runtime">运行时安装包，为空表示无需安装</param>
    /// <param name="isWindows">是否Windows</param>
    /// <returns>脚本片段</returns>
    private static String BuildRuntimeBlock(DotNetPackage? runtime, Boolean isWindows)
    {
        if (runtime == null || runtime.Source.IsNullOrEmpty())
            return "# 无需安装 .NET 运行时（自包含发布包或已安装）";

        if (isWindows)
        {
            return """
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $runtimePath = Join-Path $work $runtimeFile
    Get-File $runtimeUrl $runtimePath
    Test-Hash $runtimePath $runtimeHash
    Start-Process -FilePath $runtimePath -ArgumentList "/install","/quiet","/norestart" -Wait
}
""";
        }

        return """
if ! command -v dotnet >/dev/null 2>&1; then
    download "$RUNTIME_URL" "/tmp/$RUNTIME_FILE"
    verify "/tmp/$RUNTIME_FILE" "$RUNTIME_HASH"
    mkdir -p /usr/share/dotnet
    tar -xzf "/tmp/$RUNTIME_FILE" -C /usr/share/dotnet
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi
""";
    }
    #endregion

    #region 辅助
    /// <summary>取得下发给客户端的服务端地址。配置为空时取请求地址</summary>
    /// <param name="request">当前请求</param>
    /// <param name="setting">服务端配置</param>
    /// <returns>不含结尾斜杠的服务端地址</returns>
    public static String GetServerUrl(HttpRequest request, StarServerSetting setting)
    {
        var url = setting.AgentServerUrl;
        if (!url.IsNullOrEmpty()) return url.TrimEnd('/');

        return $"{request.Scheme}://{request.Host}";
    }

    private static String Absolutize(String url, String server) =>
        url.StartsWithIgnoreCase("http://", "https://") ? url : server.TrimEnd('/') + "/" + url.TrimStart('/');

    private static OSKind ParseOS(String? os) => os?.ToLowerInvariant() switch
    {
        "windows" or "win" => OSKind.Windows,
        "linux" => OSKind.Linux,
        "alpine" or "musl" => OSKind.LinuxMusl,
        "osx" or "macos" or "mac" => OSKind.OSX,
        _ => OSKind.Any,
    };

    private static CpuArch ParseArch(String? arch) => arch?.ToLowerInvariant() switch
    {
        "x86" or "x32" or "i386" => CpuArch.X86,
        "x64" or "x86_64" or "amd64" => CpuArch.X64,
        "arm" or "arm32" or "armv7" => CpuArch.Arm,
        "arm64" or "aarch64" => CpuArch.Arm64,
        "loongarch64" or "la64" => CpuArch.LA64,
        "riscv64" => CpuArch.RiscV64,
        "mips64" => CpuArch.Mips64,
        _ => CpuArch.Any,
    };

    /// <summary>按系统架构与运行时挑选安装包。精确匹配优先于通用包</summary>
    /// <param name="packages">候选包</param>
    /// <param name="os">目标系统</param>
    /// <param name="arch">目标架构</param>
    /// <param name="runtime">目标运行时主版本号</param>
    /// <param name="isWindows">是否Windows。用于优先挑选 zip 或 tar.gz，null表示不关心</param>
    /// <returns>匹配的安装包</returns>
    private static InstallPackage? SelectPackage(IEnumerable<InstallPackage> packages, OSKind os, CpuArch arch, String? runtime, Boolean? isWindows)
    {
        var osName = os + "";
        var archName = arch + "";

        return packages
            .Where(e => e.Kind.IsNullOrEmpty() || e.Kind.EqualIgnoreCase("agent"))
            .Where(e => e.OS.IsNullOrEmpty() || e.OS.EqualIgnoreCase("Any") || e.OS.EqualIgnoreCase(osName))
            .Where(e => e.Arch.IsNullOrEmpty() || e.Arch.EqualIgnoreCase("Any") || e.Arch.EqualIgnoreCase(archName))
            .OrderByDescending(e => os != OSKind.Any && e.OS.EqualIgnoreCase(osName))
            .ThenByDescending(e => arch != CpuArch.Any && e.Arch.EqualIgnoreCase(archName))
            .ThenByDescending(e => isWindows != null && IsZip(e.FileName) == isWindows)
            .ThenByDescending(e => IsRuntimeMatch(e.Runtime, runtime))
            .FirstOrDefault();
    }

    private static Boolean IsZip(String? fileName) => fileName != null && fileName.EndsWithIgnoreCase(".zip");

    private static Boolean IsRuntimeMatch(String? target, String? runtime)
    {
        if (target.IsNullOrEmpty() || target == "*") return true;
        if (runtime.IsNullOrEmpty()) return false;

        return target.EqualIgnoreCase(runtime) || (target + ".").StartsWithIgnoreCase(runtime + ".");
    }

    private static Boolean IsOSMatch(OSKind pkg, OSKind target) => pkg == OSKind.Any || target == OSKind.Any || pkg == target;

    private static Boolean IsArchMatch(CpuArch pkg, CpuArch target) => pkg == CpuArch.Any || target == CpuArch.Any || pkg == target;

    private static String? ReadResource(String name)
    {
        var assembly = typeof(InstallService).Assembly;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    #endregion

    #region 数据查询
    /// <summary>查询启用中的产品版本。直接查库而不是走实体缓存：后台发布后下一次刷新清单就应该看到</summary>
    /// <param name="max">最大条数</param>
    /// <returns>产品版本，新到旧</returns>
    private static IList<ProductRelease> QueryReleases(Int32 max)
    {
        var exp = new WhereExpression();
        exp &= ProductRelease._.ProductCode == AgentProductCode;
        exp &= ProductRelease._.Enable == true;

        return ProductRelease.FindAll(exp, "Id Desc", null, 0, max);
    }

    /// <summary>查询启用中的 .NET 运行时安装包</summary>
    /// <param name="max">最大条数</param>
    /// <returns>运行时安装包，新到旧</returns>
    private static IList<DotNetPackage> QueryRuntimes(Int32 max)
    {
        var exp = new WhereExpression();
        exp &= DotNetPackage._.Enable == true;
        exp &= DotNetPackage._.Kind == "runtime";

        return DotNetPackage.FindAll(exp, "Id Desc", null, 0, max);
    }

    /// <summary>为指定系统架构与 .NET 主版本挑选运行时安装包。优先匹配主版本，缺失时退回该平台最新运行时</summary>
    /// <param name="os">目标系统</param>
    /// <param name="arch">目标架构</param>
    /// <param name="major">.NET主版本号</param>
    /// <returns>运行时安装包</returns>
    private static DotNetPackage? FindRuntimeEntity(OSKind os, CpuArch arch, String? major)
    {
        var list = QueryRuntimes(200)
            .Where(e => IsOSMatch(e.OSKind, os) && IsArchMatch(e.Architecture, arch))
            .OrderByDescending(e => e.OSKind != OSKind.Any && e.OSKind == os)
            .ThenByDescending(e => e.Architecture != CpuArch.Any && e.Architecture == arch)
            .ThenByDescending(e => e.Id)
            .ToList();

        if (!major.IsNullOrEmpty() && major != "*")
        {
            var hit = list.FirstOrDefault(e => e.Version != null && e.Version.StartsWithIgnoreCase(major + "."));
            if (hit != null) return hit;
        }

        return list.FirstOrDefault();
    }
    #endregion

    #region 清单构建
    private InstallManifest GetCatalog()
    {
        const String key = "install:catalog";
        var cache = _cacheProvider.Cache;
        var catalog = cache.Get<InstallManifest>(key);
        if (catalog == null)
        {
            catalog = BuildCatalog();
            cache.Set(key, catalog, CatalogExpire);
        }

        return catalog;
    }

    private InstallManifest BuildCatalog()
    {
        var model = new InstallManifest { UpdateTime = DateTime.Now };

        // 产品版本：只取完整版 StarAgent，新版本在前。通道不再参与展示，是否上架由 Enable 控制
        foreach (var release in QueryReleases(20))
        {
            var packages = ProductPackage.FindAll(ProductPackage._.ReleaseId == release.Id);
            if (packages == null || packages.Count == 0) continue;

            var version = new InstallVersion
            {
                Id = release.Id,
                Version = release.Version,
                Force = release.Force,
            };
            foreach (var pkg in packages) version.Packages.Add(ToPackage(pkg, release.Version));

            model.Versions.Add(version);
        }

        model.Runtimes = BuildRuntimeList();
        model.Mirror = BuildMirror();

        return model;
    }

    /// <summary>页面展示的运行时清单。每个系统架构只保留最近两个主版本的最新包，避免列表过长</summary>
    /// <returns>运行时安装包</returns>
    private static List<InstallPackage> BuildRuntimeList()
    {
        var list = new List<InstallPackage>();

        foreach (var group in QueryRuntimes(200).GroupBy(e => new { e.OSKind, e.Architecture }))
        {
            var majors = group
                .GroupBy(e => MajorOf(e.Version))
                .OrderByDescending(e => e.Key.ToInt(-1))
                .Take(RuntimeMajorKeep);

            foreach (var major in majors)
            {
                var newest = major
                    .OrderByDescending(e => e.Version)
                    .ThenByDescending(e => e.Id)
                    .First();
                list.Add(ToPackage(newest));
            }
        }

        return list
            .OrderBy(e => e.OS)
            .ThenByDescending(e => MajorOf(e.Version).ToInt(-1))
            .ThenByDescending(e => e.Version)
            .ToList();
    }

    /// <summary>取版本号的主版本段，如 10.0.9 → 10</summary>
    /// <param name="version">版本号</param>
    /// <returns>主版本段</returns>
    private static String MajorOf(String? version)
    {
        if (version.IsNullOrEmpty()) return "";

        var p = version.IndexOf('.');
        return p > 0 ? version[..p] : version;
    }

    /// <summary>后台维护的安装包</summary>
    /// <param name="pkg">发布包</param>
    /// <param name="version">所属版本号</param>
    /// <returns>清单安装包</returns>
    private static InstallPackage ToPackage(ProductPackage pkg, String? version) => new()
    {
        Id = pkg.Id,
        Kind = IsNative(pkg.TargetRuntime) ? "native" : "agent",
        Version = version,
        OS = pkg.OSKind + "",
        Arch = pkg.Architecture + "",
        Runtime = pkg.TargetRuntime,
        FileName = pkg.FileName,
        Url = $"/install/download/agent/{pkg.Id}",
        Size = pkg.Size,
        FileHash = pkg.FileHash,
        Origin = "db",
    };

    /// <summary>后台维护的.NET运行时包</summary>
    /// <param name="pkg">运行时包</param>
    /// <returns>清单安装包</returns>
    private static InstallPackage ToPackage(DotNetPackage pkg) => new()
    {
        Id = pkg.Id,
        Kind = "runtime",
        Version = pkg.Version,
        OS = pkg.OSKind + "",
        Arch = pkg.Architecture + "",
        Runtime = null,
        FileName = pkg.FileName,
        Url = $"/install/download/runtime/{pkg.Id}",
        Size = pkg.Size,
        FileHash = pkg.FileHash,
        Origin = "db",
    };

    private static BootstrapPackage? ToBootstrapPackage(DotNetPackage? pkg) => pkg == null ? null : new BootstrapPackage
    {
        Version = pkg.Version,
        Kind = pkg.Kind,
        FileName = pkg.FileName,
        Source = pkg.Source,
        Size = pkg.Size,
        FileHash = pkg.FileHash,
    };

    private static BootstrapPackage? ToBootstrapPackage(ProductPackage? pkg, String? version) => pkg == null ? null : new BootstrapPackage
    {
        Version = version,
        FileName = pkg.FileName,
        Source = pkg.Source,
        Size = pkg.Size,
        FileHash = pkg.FileHash,
    };

    private static Boolean IsNativeAgent(String? productCode) =>
        productCode.EqualIgnoreCase("StarAgent-Go", "StarAgent-Rust", "StarAgent-Cpp");

    private static Boolean IsNative(String? runtime) =>
        runtime.EqualIgnoreCase("go", "rust", "cpp");

    /// <summary>公共镜像兜底清单。服务端未维护对应包时使用，文件名与官方下载目录约定一致</summary>
    /// <returns>镜像安装包</returns>
    private List<InstallPackage> BuildMirror()
    {
        var baseUrl = _setting.AgentDownloadBaseUrl;
        if (baseUrl.IsNullOrEmpty()) baseUrl = "https://x.newlifex.com/star/";
        baseUrl = baseUrl.EnsureEnd("/");

        var list = new List<InstallPackage>();

        void Add(String fileName, String version, String runtime, String os, String arch)
        {
            list.Add(new InstallPackage
            {
                Kind = "agent",
                Version = version,
                OS = os,
                Arch = arch,
                Runtime = runtime,
                FileName = fileName,
                Url = baseUrl + fileName,
                Origin = "mirror",
            });
        }

        // 新版本在前，脚本挑选时直接取首个匹配项
        Add("staragent10.tar.gz", "net10.0", "10", "Linux", "Any");
        Add("staragent10.zip", "net10.0", "10", "Windows", "Any");
        Add("staragent90.tar.gz", "net9.0", "9", "Linux", "Any");
        Add("staragent90.zip", "net9.0", "9", "Windows", "Any");
        Add("staragent80.tar.gz", "net8.0", "8", "Linux", "Any");
        Add("staragent80.zip", "net8.0", "8", "Windows", "Any");
        Add("staragent60.tar.gz", "net6.0", "6", "Linux", "Any");
        Add("staragent60.zip", "net6.0", "6", "Windows", "Any");
        Add("staragent45.zip", "net4.5", "4", "Windows", "Any");
        Add("staragent31.tar.gz", "netcoreapp3.1", "3.1", "Linux", "Mips64");
        Add("staragent31.zip", "netcoreapp3.1", "3.1", "Windows", "Mips64");

        return list;
    }
    #endregion
}
