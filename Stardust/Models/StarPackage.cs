using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Serialization;

namespace Stardust.Models;

/// <summary>星尘应用包清单。zip 包根目录的 star.json</summary>
/// <remarks>
/// 一个包可以包含多个应用，每个应用对应一个 <see cref="ServiceInfo"/>。
/// 包名格式为“项目名/应用名”，例如 NewLife/StarServer。
/// </remarks>
public class StarPackage
{
    #region 属性
    /// <summary>格式版本。当前为 1</summary>
    public Int32 Schema { get; set; } = 1;

    /// <summary>包名。项目名/应用名，例如 NewLife/StarServer</summary>
    public String? Name { get; set; }

    /// <summary>版本。例如 8.0.21、4.0.2026.1123</summary>
    public String? Version { get; set; }

    /// <summary>显示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>说明</summary>
    public String? Description { get; set; }

    /// <summary>目标操作系统。支持名称或数字，0/空表示通用</summary>
    public String? OS { get; set; }

    /// <summary>目标CPU架构。支持名称或数字，0/空表示通用</summary>
    public String? Arch { get; set; }

    /// <summary>应用集合。至少一个</summary>
    public StarPackageApp[] Apps { get; set; } = [];
    #endregion

    #region 解析
    /// <summary>从 Json 字符串解析包清单</summary>
    /// <param name="json">star.json 内容</param>
    /// <returns>包清单</returns>
    public static StarPackage Parse(String json)
    {
        if (json.IsNullOrEmpty()) throw new ArgumentNullException(nameof(json));

        // 先按 camelCase 解析，兼容标准 star.json 写法；失败或关键字段为空时再按默认命名解析
        StarPackage? model = null;
        try
        {
            model = JsonHelper.ToJsonEntity<StarPackage>(json, new JsonOptions { PropertyNaming = PropertyNaming.CamelCase });
        }
        catch
        {
            // 忽略，尝试默认命名
        }

        if (model == null || model.Name.IsNullOrEmpty() || model.Version.IsNullOrEmpty())
            model = json.ToJsonEntity<StarPackage>();

        if (model == null) throw new InvalidOperationException("Invalid star.json.");

        return model;
    }

    /// <summary>从文件加载包清单</summary>
    /// <param name="file">star.json 文件路径</param>
    /// <returns>包清单</returns>
    public static StarPackage Load(String file)
    {
        if (file.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));

        return Parse(File.ReadAllText(file, Encoding.UTF8));
    }
    #endregion

    #region 校验
    /// <summary>校验包清单的结构。不校验运行时参数是否齐全</summary>
    /// <returns>错误列表，空表示通过</returns>
    public virtual IList<String> Validate()
    {
        var errors = new List<String>();

        if (Schema != 1) errors.Add($"Unsupported schema [{Schema}], expected 1.");

        if (Name.IsNullOrEmpty())
            errors.Add("Package name cannot be empty.");
        else if (!Regex.IsMatch(Name, @"^[a-z0-9][a-z0-9._-]*/[a-z0-9][a-z0-9._-]*$", RegexOptions.IgnoreCase))
            errors.Add($"Invalid package name [{Name}]. Expected publisher/name.");
        else
        {
            var shortName = ShortName;
            if (shortName.IsNullOrEmpty()) errors.Add($"Invalid package name [{Name}].");
        }

        if (Version.IsNullOrEmpty())
            errors.Add("Package version cannot be empty.");
        else if (Version.Contains('/') || Version.Contains('\\') || Version.Length > 50)
            errors.Add($"Invalid package version [{Version}].");

        try
        {
            ResolveOS();
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        try
        {
            ResolveArch();
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        if (Apps == null || Apps.Length == 0)
        {
            errors.Add("At least one application is required.");
            return errors;
        }

        var appNames = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var mainCount = 0;
        foreach (var app in Apps)
        {
            if (app.Name.IsNullOrEmpty())
                errors.Add("Application name cannot be empty.");
            else if (!Regex.IsMatch(app.Name, @"^[A-Za-z0-9][A-Za-z0-9._-]*$"))
                errors.Add($"Invalid application name [{app.Name}].");
            else if (!appNames.Add(app.Name))
                errors.Add($"Duplicate application name [{app.Name}].");

            if (app.FileName.IsNullOrEmpty())
                errors.Add($"Application [{app.Name}] fileName cannot be empty.");
            else if (!IsSafeRelativePath(app.FileName))
                errors.Add($"Application [{app.Name}] fileName must be a relative path without '..': {app.FileName}");

            if (!app.WorkingDirectory.IsNullOrEmpty() && !IsSafeRelativePath(app.WorkingDirectory))
                errors.Add($"Application [{app.Name}] workingDirectory must be a relative path without '..': {app.WorkingDirectory}");

            if (!app.ServiceName.IsNullOrEmpty() && !Regex.IsMatch(app.ServiceName, @"^[A-Za-z0-9][A-Za-z0-9._-]*$"))
                errors.Add($"Invalid service name [{app.ServiceName}] in application [{app.Name}].");

            if (app.Ports != null)
            {
                foreach (var port in app.Ports)
                {
                    if (port is <= 0 or > 65535)
                        errors.Add($"Invalid port [{port}] in application [{app.Name}].");
                }
            }

            if (app.Main) mainCount++;
        }

        if (mainCount > 1) errors.Add("Only one application can be marked as main.");

        return errors;
    }

    #endregion

    #region 平台
    /// <summary>解析目标操作系统</summary>
    /// <returns>操作系统枚举，空或 0 表示通用</returns>
    public virtual OSKind ResolveOS()
    {
        var value = OS?.Trim();
        if (value.IsNullOrEmpty()) return OSKind.Any;

        if (Int32.TryParse(value, out var num))
        {
            if (Enum.IsDefined(typeof(OSKind), num)) return (OSKind)num;
            throw new InvalidOperationException($"Invalid os value [{OS}].");
        }

        var name = value.Replace("-", "").Replace("_", "");
        switch (name.ToLowerInvariant())
        {
            case "any":
            case "all":
                return OSKind.Any;
            case "windows":
            case "win":
                return OSKind.Windows;
            case "linux":
                return OSKind.Linux;
            case "linuxmusl":
            case "musl":
            case "alpine":
                return OSKind.LinuxMusl;
            case "osx":
            case "macos":
            case "mac":
                return OSKind.OSX;
            case "android":
                return OSKind.Android;
            case "ios":
                return OSKind.iOS;
            default:
                if (Enum.TryParse<OSKind>(name, true, out var os)) return os;
                throw new InvalidOperationException($"Invalid os value [{OS}].");
        }
    }

    /// <summary>解析目标CPU架构</summary>
    /// <returns>架构枚举，空或 0 表示通用</returns>
    public virtual CpuArch ResolveArch()
    {
        var value = Arch?.Trim();
        if (value.IsNullOrEmpty()) return CpuArch.Any;

        if (Int32.TryParse(value, out var num))
        {
            if (Enum.IsDefined(typeof(CpuArch), num)) return (CpuArch)num;
            throw new InvalidOperationException($"Invalid arch value [{Arch}].");
        }

        var name = value.Replace("-", "").Replace("_", "");
        switch (name.ToLowerInvariant())
        {
            case "any":
            case "all":
                return CpuArch.Any;
            case "x86":
            case "i386":
            case "i686":
                return CpuArch.X86;
            case "x64":
            case "x8664":
            case "amd64":
                return CpuArch.X64;
            case "arm":
            case "arm32":
                return CpuArch.Arm;
            case "arm64":
            case "aarch64":
                return CpuArch.Arm64;
            case "la64":
            case "loongarch64":
                return CpuArch.LA64;
            case "riscv64":
                return CpuArch.RiscV64;
            case "mips64":
                return CpuArch.Mips64;
            default:
                if (Enum.TryParse<CpuArch>(name, true, out var arch)) return arch;
                throw new InvalidOperationException($"Invalid arch value [{Arch}].");
        }
    }
    #endregion

    #region 转换
    /// <summary>包短名。例如 NewLife/StarServer 的 StarServer</summary>
    public virtual String ShortName
    {
        get
        {
            var name = Name;
            if (name.IsNullOrEmpty()) return String.Empty;

            var index = name.LastIndexOf('/');
            return index >= 0 ? name[(index + 1)..] : name;
        }
    }

    /// <summary>发布者。例如 NewLife/StarServer 的 NewLife</summary>
    public virtual String Publisher
    {
        get
        {
            var name = Name;
            if (name.IsNullOrEmpty()) return String.Empty;

            var index = name.LastIndexOf('/');
            return index > 0 ? name[..index] : String.Empty;
        }
    }

    /// <summary>获取接收命令行参数的主应用。没有标记 main 时取第一个应用</summary>
    /// <returns>主应用</returns>
    public virtual StarPackageApp? GetMainApp()
    {
        if (Apps == null || Apps.Length == 0) return null;

        foreach (var app in Apps)
        {
            if (app.Main) return app;
        }

        return Apps[0];
    }

    /// <summary>把包清单转换为应用服务集合</summary>
    /// <param name="root">包解压根目录</param>
    /// <returns>应用服务集合</returns>
    public virtual IList<ServiceInfo> ToServices(String root)
    {
        if (root.IsNullOrEmpty()) throw new ArgumentNullException(nameof(root));

        var list = new List<ServiceInfo>();
        if (Apps == null) return list;

        foreach (var app in Apps)
        {
            var service = ToService(app, root);
            list.Add(service);
        }

        return list;
    }

    /// <summary>把单个应用转换为应用服务</summary>
    /// <param name="app">应用</param>
    /// <param name="root">包解压根目录</param>
    /// <returns>应用服务</returns>
    public virtual ServiceInfo ToService(StarPackageApp app, String root)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        var file = app.FileName;
        if (file.IsNullOrEmpty()) throw new InvalidOperationException($"Application [{app.Name}] fileName cannot be empty.");

        var fileName = file;
        if (!file.Contains('/') && !file.Contains('\\'))
        {
            // 不带路径分隔符时，优先当作包内文件；不存在则按系统命令处理
            var full = Path.GetFullPath(Path.Combine(root, file));
            if (File.Exists(full)) fileName = full;
        }
        else
        {
            fileName = Path.GetFullPath(Path.Combine(root, file));
        }

        var workDir = app.WorkingDirectory;
        if (!workDir.IsNullOrEmpty())
            workDir = Path.GetFullPath(Path.Combine(root, workDir));
        else if (!fileName.Contains('/') && !fileName.Contains('\\'))
            workDir = root;
        else
            workDir = Path.GetDirectoryName(fileName) ?? root;

        var service = new ServiceInfo
        {
            Name = GetServiceName(app),
            FileName = fileName,
            Arguments = app.Arguments,
            WorkingDirectory = workDir,
            UserName = app.UserName,
            Enable = app.AutoStart ?? true,
            Mode = app.Mode == DeployMode.Default ? DeployMode.Standard : app.Mode,
            AllowMultiple = app.AllowMultiple,
            Environments = app.Environments,
            AutoStop = app.AutoStop,
            ReloadOnChange = app.ReloadOnChange,
            MaxMemory = app.MaxMemory,
            OomScoreAdjust = app.OomScoreAdjust,
            Priority = app.Priority,
            HealthCheck = app.HealthCheck,

            Package = Name,
            PackageVersion = Version,
            PackageApp = app.Name,
        };

        return service;
    }

    /// <summary>获取应用的服务名。默认使用“包短名-应用名”，应用名等于包短名时只用包短名</summary>
    /// <param name="app">应用</param>
    /// <returns>服务名</returns>
    public virtual String GetServiceName(StarPackageApp app)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        var name = app.ServiceName;
        if (name.IsNullOrEmpty())
        {
            var shortName = ShortName;
            name = app.Name.EqualIgnoreCase(shortName) ? shortName : $"{shortName}-{app.Name}";
        }

        return SanitizeName(name);
    }

    /// <summary>清理服务名中的非法字符</summary>
    /// <param name="name">原始名称</param>
    /// <returns>清理后的名称</returns>
    public static String SanitizeName(String? name)
    {
        if (name.IsNullOrEmpty()) return String.Empty;

        return name.Replace('/', '-').Replace('\\', '-').Replace(':', '-').Trim();
    }

    /// <summary>判断是否为安全的相对路径</summary>
    /// <param name="path">路径</param>
    /// <returns>是否安全</returns>
    public static Boolean IsSafeRelativePath(String? path)
    {
        if (path.IsNullOrEmpty()) return false;
        if (path.StartsWith("/") || path.StartsWith("\\")) return false;
        if (path.Length >= 2 && path[1] == ':') return false;

        foreach (var item in path.Split('/', '\\'))
        {
            if (item == "..") return false;
        }

        return true;
    }
    #endregion
}

/// <summary>星尘包应用。对应 star.json 的 apps[]</summary>
public class StarPackageApp
{
    #region 属性
    /// <summary>应用名。包内唯一</summary>
    public String? Name { get; set; }

    /// <summary>显示名</summary>
    public String? DisplayName { get; set; }

    /// <summary>服务名。为空时自动生成“包短名-应用名”</summary>
    public String? ServiceName { get; set; }

    /// <summary>启动文件。相对包根目录，也可以是 ping 这类系统命令</summary>
    public String? FileName { get; set; }

    /// <summary>启动参数。使用明确的实际值，不做变量展开</summary>
    public String? Arguments { get; set; }

    /// <summary>工作目录。相对包根目录，为空时使用启动文件所在目录</summary>
    public String? WorkingDirectory { get; set; }

    /// <summary>用户。以该用户执行应用</summary>
    public String? UserName { get; set; }

    /// <summary>环境变量。key=value;key2=value2</summary>
    public String? Environments { get; set; }

    /// <summary>健康检查。http/tcp/udp 地址</summary>
    public String? HealthCheck { get; set; }

    /// <summary>工作模式。Standard/Shadow/Hosted/Task，默认 Standard</summary>
    public DeployMode Mode { get; set; } = DeployMode.Standard;

    /// <summary>端口列表。启动前检查占用</summary>
    public Int32[]? Ports { get; set; }

    /// <summary>是否主应用。接收命令行透传参数，最多一个</summary>
    public Boolean Main { get; set; }

    /// <summary>依赖的应用名。v1 仅按数组顺序启动</summary>
    public String[]? DependsOn { get; set; }

    /// <summary>最大内存。单位M，0不限制</summary>
    public Int32 MaxMemory { get; set; }

    /// <summary>OOM 分值。Linux 下 OOM Killer 选择目标的优先级</summary>
    public Int32 OomScoreAdjust { get; set; }

    /// <summary>优先级</summary>
    public ProcessPriority Priority { get; set; }

    /// <summary>检测变动。文件改变时自动重启</summary>
    public Boolean ReloadOnChange { get; set; }

    /// <summary>启动挂钩。注入星尘监控</summary>
    public Boolean StartupHook { get; set; }

    /// <summary>允许多实例</summary>
    public Boolean AllowMultiple { get; set; }

    /// <summary>随宿主退出自动停止</summary>
    public Boolean AutoStop { get; set; }

    /// <summary>是否自动启动。为空时默认 true</summary>
    public Boolean? AutoStart { get; set; }
    #endregion
}

