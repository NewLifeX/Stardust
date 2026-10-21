using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using Stardust.Models;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Stardust.Data.Nodes;

public partial class DotNetPackage : Entity<DotNetPackage>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static DotNetPackage()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(OSKind));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 处理当前已登录用户信息，可以由UserInterceptor拦截器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化DotNetPackage[dotNet安装包]数据……");

    //    var entity = new DotNetPackage();
    //    entity.Version = "abc";
    //    entity.Kind = "abc";
    //    entity.OSKind = 0;
    //    entity.Architecture = "abc";
    //    entity.FileName = "abc";
    //    entity.Source = "abc";
    //    entity.Size = 0;
    //    entity.FileHash = "abc";
    //    entity.Enable = true;
    //    entity.Force = true;
    //    entity.Channel = 0;
    //    entity.AutoImport = true;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化DotNetPackage[dotNet安装包]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Version From DotNetPackage Where CreateTime>'2020-01-24 00:00:00' Group By Version Order By Id Desc limit 20
    static readonly FieldCache<DotNetPackage> _VersionCache = new(nameof(Version))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取版本号列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetVersionList() => _VersionCache.FindAllName();

    /// <summary>按版本号查询所有安装包</summary>
    /// <param name="version">版本号</param>
    public static IList<DotNetPackage> FindAllByVersion(String version) => FindAll(_.Version == version);
    #endregion

    #region 业务操作
    /// <summary>将节点具体的OSKinds映射为通用的OSKind</summary>
    private static OSKind GetOSKind(Stardust.Models.OSKinds os)
    {
        var v = (Int32)os;
        if (v >= 10 && v < 100 || v == 70) return OSKind.Windows;
        if (v >= 100 && v < 200) return OSKind.Linux;
        if (v >= 200 && v < 300) return OSKind.LinuxMusl;
        if (v >= 400 && v < 500) return OSKind.OSX;

        return OSKind.Any;
    }

    /// <summary>将节点架构字符串映射为CpuArch枚举</summary>
    private static CpuArch GetCpuArch(String arch)
    {
        if (arch.IsNullOrEmpty()) return CpuArch.Any;

        if (arch.EqualIgnoreCase("X86", "x86", "X32", "x32")) return CpuArch.X86;
        if (arch.EqualIgnoreCase("X64", "x64", "AMD64", "amd64")) return CpuArch.X64;
        if (arch.EqualIgnoreCase("Arm64", "arm64", "AArch64", "aarch64")) return CpuArch.Arm64;
        if (arch.EqualIgnoreCase("Arm32", "arm32", "Arm", "arm")) return CpuArch.Arm;
        if (arch.EqualIgnoreCase("LoongArch64", "loongarch64", "LA64", "la64")) return CpuArch.LA64;
        if (arch.EqualIgnoreCase("RiscV64", "riscv64")) return CpuArch.RiscV64;
        if (arch.EqualIgnoreCase("Mips64", "mips64")) return CpuArch.Mips64;

        return CpuArch.Any;
    }

    /// <summary>获取有效包列表</summary>
    /// <param name="channel">升级通道</param>
    /// <returns></returns>
    public static IList<DotNetPackage> GetValids(NodeChannels channel)
    {
        var list = Meta.Cache.FindAll(e => e.Enable);
        if (list.Count == 0) return list;

        if (channel >= NodeChannels.Release) list = list.Where(e => e.Channel == channel).ToList();

        // 按照编号降序
        list = list.OrderByDescending(e => e.Id).Take(100).ToList();

        return list;
    }

    /// <summary>匹配节点，找到适合指定节点的dotNet安装包</summary>
    /// <param name="node">节点</param>
    /// <returns>匹配的安装包，若无匹配返回null</returns>
    public static DotNetPackage Match(Node node) => Match(node, null);

    /// <summary>匹配节点，找到适合指定节点的dotNet安装包</summary>
    /// <param name="node">节点</param>
    /// <param name="kind">安装类型。null表示自动判断：Linux→aspnet，Win桌面→desktop，Win服务器→host</param>
    /// <returns>匹配的安装包，若无匹配返回null</returns>
    public static DotNetPackage Match(Node node, String kind)
    {
        // 将节点信息映射为DotNetPackage的枚举
        var os = GetOSKind(node.OSKind);
        var arch = GetCpuArch(node.Architecture);

        // 自动判断安装类型
        if (kind.IsNullOrEmpty())
        {
            if (os == OSKind.Linux || os == OSKind.LinuxMusl)
                kind = "aspnet";
            else if (os == OSKind.Windows)
            {
                // 从详细OS判断桌面版还是服务器版
                var osDetail = node.OSKind;
                var v = (Int32)osDetail;
                // Windows 桌面版 (win10/win11/win7)
                if (osDetail == Stardust.Models.OSKinds.Win10 || osDetail == Stardust.Models.OSKinds.Win11 || osDetail == Stardust.Models.OSKinds.Win7)
                    kind = "desktop";
                // Windows 服务器版
                else if (v == 68 || v == 64 || v == 66 || v == 69 || v == 72 || v == 70)
                    kind = "host";
                else
                    kind = "desktop";
            }
        }

        if (kind.IsNullOrEmpty()) return null;

        var list = GetValids(NodeChannels.Release);
        if (list.Count == 0) return null;

        // 按匹配度排序：精确匹配 OS + Arch + Kind，Version 降序
        var pkg = list
            .Where(e => e.Kind == kind)
            .Where(e => e.OSKind == os || e.OSKind == 0)
            .Where(e => e.Architecture == arch || e.Architecture == 0)
            .OrderByDescending(e => e.Id)
            .FirstOrDefault();

        return pkg;
    }

    /// <summary>根据节点自动判断最合适的安装类型</summary>
    /// <param name="node">节点</param>
    /// <returns>安装类型，如 aspnet/desktop/host/runtime</returns>
    public static String GetKindForNode(Node node)
    {
        var os = GetOSKind(node.OSKind);

        if (os == OSKind.Linux || os == OSKind.LinuxMusl)
            return "aspnet";

        if (os == OSKind.Windows)
        {
            var osDetail = node.OSKind;
            var v = (Int32)osDetail;
            // Windows 桌面版 (win10/win11/win7)
            if (osDetail == Stardust.Models.OSKinds.Win10 || osDetail == Stardust.Models.OSKinds.Win11 || osDetail == Stardust.Models.OSKinds.Win7)
                return "desktop";
            // Windows 服务器版
            if (v == 68 || v == 64 || v == 66 || v == 69 || v == 72 || v == 70)
                return "host";

            return "desktop";
        }

        return "runtime";
    }

    /// <summary>获取安装类型列表，用于前台下拉选择。空字符串表示自动</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetKindList()
    {
        return new Dictionary<String, String>
        {
            ["auto"] = "自动",
            ["runtime"] = "runtime",
            ["aspnet"] = "aspnet",
            ["desktop"] = "desktop",
            ["host"] = "host",
            ["sdk"] = "sdk",
        };
    }

    /// <summary>为指定节点解析版本+类型到最优的安装包。逐节点匹配OS/Architecture</summary>
    /// <param name="version">版本号</param>
    /// <param name="kind">安装类型。auto或空表示自动判断</param>
    /// <param name="node">目标节点</param>
    /// <returns>匹配的安装包，若无匹配返回null</returns>
    public static DotNetPackage ResolveForNode(String version, String kind, Node node)
    {
        // 自动判断安装类型
        if (kind.IsNullOrEmpty() || kind.EqualIgnoreCase("auto"))
            kind = GetKindForNode(node);

        if (kind.IsNullOrEmpty()) return null;

        var os = GetOSKind(node.OSKind);
        var arch = GetCpuArch(node.Architecture);

        // 查找匹配的包。使用 FindAll 按条件过滤
        var list = Meta.Cache.FindAll(e => e.Enable && e.Kind == kind);
        if (list.Count == 0) return null;

        // 在内存中按版本号过滤
        list = list.Where(e => e.Version == version).ToList();
        if (list.Count == 0) return null;

        return list
            .Where(e => e.OSKind == os || e.OSKind == 0)
            .Where(e => e.Architecture == arch || e.Architecture == 0)
            .OrderByDescending(e => e.OSKind == os ? 1 : 0)
            .ThenByDescending(e => e.Architecture == arch ? 1 : 0)
            .ThenByDescending(e => e.Id)
            .FirstOrDefault();
    }

    /// <summary>为节点解析安装包并做兼容性检查，只用于手工推送。
    /// 版本为空或auto时，按节点操作系统、架构和安装类型自动匹配最新的启用安装包，随后检查操作系统与GLIBC兼容性。</summary>
    /// <param name="node">节点</param>
    /// <param name="version">版本号。为空或auto时自动匹配最新启用版本</param>
    /// <param name="kind">安装类型。为空或auto时按节点自动判断</param>
    /// <param name="reason">不可推送时的原因描述；可推送时为null</param>
    /// <returns>可推送的安装包；无匹配或检查不通过时返回null</returns>
    public static DotNetPackage ResolveForPush(Node node, String version, String kind, out String reason)
    {
        reason = null;
        if (node == null)
        {
            reason = L.T("Node cannot be empty.");
            return null;
        }

        // 自动匹配版本：留空或auto表示取最新的启用安装包
        var auto = version.IsNullOrEmpty() || version.EqualIgnoreCase("auto");

        // 自动判断安装类型
        if (kind.IsNullOrEmpty() || kind.EqualIgnoreCase("auto")) kind = null;

        // 解析安装包。指定版本时按 OS/Arch 精确匹配
        var pkg = auto ? Match(node, kind) : ResolveForNode(version, kind, node);
        if (pkg == null)
        {
            reason = auto ? L.T("No matching package was found.") : L.T("No matching package was found (version={0}, kind={1}).", version, kind);
            return null;
        }

        // 操作系统兼容性检查（如 Ubuntu18 无法安装 .NET10）
        if (!IsOSCompatible(node, pkg.Version))
        {
            reason = L.T("OS [{0}] is not compatible with .NET {1}", node.OS, pkg.Version);
            return null;
        }

        // GLIBC版本检查（Linux节点上报了CLibVersion时启用）
        var minGLibc = GetDotNetMinGLibcVersion(pkg.Version);
        if (minGLibc != null && !CheckGLibc(node, minGLibc))
        {
            reason = L.T("GLIBC [{0}] does not meet the minimum requirement {1} of .NET {2}", node.CLibVersion, minGLibc, GetNetMajorVersion(pkg.Version));
            return null;
        }

        return pkg;
    }

    /// <summary>检查节点操作系统是否兼容目标.NET版本。防止向过旧的操作系统推送不支持的.NET运行时</summary>
    /// <param name="node">节点</param>
    /// <param name="version">目标.NET版本号，如 10.0.9</param>
    /// <returns>兼容返回true，不兼容返回false</returns>
    public static Boolean IsOSCompatible(Node node, String version)
    {
        if (node.OS.IsNullOrEmpty() || version.IsNullOrEmpty()) return true;
        if (!System.Version.TryParse(version.TrimStart('v', 'V'), out var ver)) return true;

        var os = node.OS;
        var major = ver.Major;

        // 提取操作系统名称和版本号
        Double osVer = 0;
        var dist = "";

        // 匹配常见 Linux 发行版
        if (os.StartsWithIgnoreCase("Ubuntu"))
        {
            dist = "Ubuntu";
            // 实际为 PRETTY_NAME，如 "Ubuntu 18.04.5 LTS"；取前两段拼成两段版本号（18.04.5 → 18.04），兼容两段格式
            var part = os.Split(' ').Skip(1).FirstOrDefault();
            var ps = part?.Split('.');
            if (ps != null && ps.Length >= 2)
                Double.TryParse($"{ps[0]}.{ps[1]}", out osVer);
            else
                Double.TryParse(part, out osVer);
        }
        else if (os.StartsWithIgnoreCase("Debian"))
        {
            dist = "Debian";
            // "Debian GNU/Linux 11 (bullseye)" → 11
            foreach (var s in os.Split(' '))
            {
                if (Double.TryParse(s, out var v)) { osVer = v; break; }
            }
        }
        else if (os.StartsWithIgnoreCase("CentOS") || os.StartsWithIgnoreCase("RHEL") || os.StartsWithIgnoreCase("Red Hat"))
        {
            dist = "RHEL";
            // "CentOS Linux 7 (Core)" → 7
            foreach (var s in os.Split(' '))
            {
                if (Double.TryParse(s, out var v)) { osVer = v; break; }
            }
        }

        // 检查兼容性
        if (osVer > 0)
        {
            if (major >= 10)
            {
                if (dist == "Ubuntu") return osVer >= 22.04;
                if (dist == "Debian") return osVer >= 12;
                if (dist == "RHEL") return osVer >= 9;
            }
            else if (major >= 8)
            {
                if (dist == "Ubuntu") return osVer >= 20.04;
                if (dist == "Debian") return osVer >= 11;
                if (dist == "RHEL") return osVer >= 8;
            }
            else if (major >= 6)
            {
                if (dist == "Ubuntu") return osVer >= 16.04;
                if (dist == "Debian") return osVer >= 10;
                if (dist == "RHEL") return osVer >= 7;
            }
        }

        // 未知操作系统或无法识别版本时，默认兼容（不阻塞推送）
        return true;
    }

    /// <summary>获取指定.NET版本要求的最低GLIBC版本（硬编码，每年随.NET大版本更新一次）</summary>
    /// <param name="version">.NET版本号，如 10.0.9</param>
    /// <returns>最低GLIBC版本号，如 2.17；未知时返回 null</returns>
    /// <remarks>
    /// .NET 版本与 glibc 兼容性历史：
    /// .NET 6/7/8 → glibc 2.17+（CentOS 7 及更新）
    /// .NET 9/10  → glibc 2.27+（CentOS 8/Ubuntu 18.04 及更新）
    /// 首次发行年份：.NET 6=2021, .NET 7=2022, .NET 8=2023, .NET 9=2024, .NET 10=2025
    /// </remarks>
    public static String GetDotNetMinGLibcVersion(String version)
    {
        if (version.IsNullOrEmpty()) return null;
        if (!System.Version.TryParse(version.TrimStart('v', 'V'), out var ver)) return null;

        var major = ver.Major;

        // .NET 9+ 要求 glibc 2.27+
        if (major >= 9) return "2.27";
        // .NET 6/7/8 支持 glibc 2.17+
        if (major >= 6) return "2.17";

        return null;
    }

    /// <summary>获取.NET版本的主要代号字符串，用于日志显示</summary>
    private static String GetNetMajorVersion(String version)
    {
        if (version.IsNullOrEmpty()) return "";
        if (!System.Version.TryParse(version.TrimStart('v', 'V'), out var ver)) return "";
        return $".NET {ver.Major}";
    }

    /// <summary>检查节点的GLIBC版本是否满足最低要求</summary>
    /// <param name="node">节点</param>
    /// <param name="minVersion">最低GLIBC版本号，如 2.17</param>
    /// <returns>满足返回true，不满足返回false</returns>
    /// <remarks>
    /// 仅当节点为Linux且上报了CLibVersion时启用精确检查。
    /// CLibVersion 格式示例：glibc 2.17、glibc 2.27、musl 1.2.2；可能形如 glibc 2.17;glibcxx 3.4.30
    /// 使用 System.Version 逐段比较 major.minor，忽略后缀 patch 版本。
    /// </remarks>
    public static Boolean CheckGLibc(Node node, String minVersion)
    {
        // 非 Linux 或未上报 CLibVersion 时跳过检查
        if (node.CLibVersion.IsNullOrEmpty()) return true;
        if (!node.OS.StartsWithIgnoreCase("Linux") && !node.OS.StartsWithIgnoreCase("CentOS") &&
            !node.OS.StartsWithIgnoreCase("Ubuntu") && !node.OS.StartsWithIgnoreCase("Debian") &&
            !node.OS.StartsWithIgnoreCase("RHEL") && !node.OS.StartsWithIgnoreCase("Red Hat"))
            return true;

        if (!System.Version.TryParse(minVersion, out var min)) return true;

        // 从 CLibVersion 中提取 glibc 版本号
        // 格式：glibc 2.17 或 glibc 2.17;glibcxx 3.4.30
        var clib = node.CLibVersion;
        var p = clib.IndexOf(';');
        if (p > 0) clib = clib.Substring(0, p);
        clib = clib.Trim();

        // 提取 glibc x.y 或 musl x.y.z
        if (!clib.StartsWithIgnoreCase("glibc ") && !clib.StartsWithIgnoreCase("musl ")) return true;

        var verStr = clib.Substring(clib.IndexOf(' ') + 1).Trim();
        if (verStr.IsNullOrEmpty()) return true;

        if (!System.Version.TryParse(verStr, out var current)) return true;

        // 只比较 major.minor，patch 版本不影响兼容性
        var currentMajorMinor = current.Major * 10000 + current.Minor;
        var minMajorMinor = min.Major * 10000 + min.Minor;

        return currentMajorMinor >= minMajorMinor;
    }

    /// <summary>获取节点的推荐升级列表。按版本从高到低排序，自动过滤不兼容项</summary>
    /// <param name="node">节点</param>
    /// <returns>推荐列表，每项包含安装包和显示标签</returns>
    public static IList<(DotNetPackage Package, String Label)> GetRecommendations(Node node)
    {
        var result = new List<(DotNetPackage, String)>();

        var current = node.Framework;
        System.Version.TryParse(current?.TrimStart('v', 'V'), out var currentVer);

        var os = GetOSKind(node.OSKind);
        var arch = GetCpuArch(node.Architecture);

        var packages = Meta.Cache.FindAll(e => e.Enable)
            .Where(e => e.OSKind == os || e.OSKind == 0)
            .Where(e => e.Architecture == arch || e.Architecture == 0)
            .OrderByDescending(e => e.Id)
            .ToList();

        // 去重：同 (Version, Kind) 只保留一条（最高匹配度那条已在上面排序）
        var seen = new HashSet<String>();
        foreach (var pkg in packages)
        {
            var key = $"{pkg.Version}-{pkg.Kind}";
            if (!seen.Add(key)) continue;

            // 检查版本是否高于当前已安装版本
            if (currentVer != null)
            {
                if (!System.Version.TryParse(pkg.Version.TrimStart('v', 'V'), out var pkgVer)) continue;
                if (pkgVer <= currentVer) continue;
            }

            var label = $"{pkg.Version} {pkg.Kind} ({pkg.OSKind} {pkg.Architecture})";
            result.Add((pkg, label));
        }

        return result;
    }
    #endregion
}
