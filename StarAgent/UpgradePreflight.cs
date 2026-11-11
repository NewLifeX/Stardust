using NewLife;
using NewLife.Serialization;

namespace StarAgent;

/// <summary>升级预检</summary>
/// <remarks>
/// 判断覆盖到位的新版本能否在本机跑起来。框架依赖发布的程序一旦缺少 runtimeconfig.json 要求的 .NET 运行时，
/// .NET 主机会在托管代码执行之前直接退出（Linux 实测退出码 150），冒烟测试与“重启服务”都不可能成功。
/// 这类失败必须在把控制权交出去之前识别：重启服务会结束仍在正常工作的旧进程，而新版本又起不来，节点将彻底失联。
/// <para>运行时版本按 .NET 主机的真实前滚规则判定：Disable 要求精确版本，Major/LatestMajor 允许跨主版本，
/// 其余策略只允许同一主版本内向前且补丁号不得低于要求；环境变量 DOTNET_ROLL_FORWARD / DOTNET_ROLL_FORWARD_TO_PRERELEASE 优先于 runtimeconfig。</para>
/// <para>预检只用于提前记录原因、跳过注定失败的重试，不参与“升级失败后做什么”的决策：冒烟测试失败一律回滚旧文件，
/// 绝不通过重启服务去赌新版能否起来。</para>
/// </remarks>
public static class UpgradePreflight
{
    /// <summary>检查新版本所需的 .NET 运行时是否已安装</summary>
    /// <param name="dir">新版本文件所在目录</param>
    /// <param name="name">应用名称，不含扩展名</param>
    /// <param name="installed">已安装运行时探测。参数为框架名，返回版本号列表；为空时探测本机</param>
    /// <returns>缺少运行时返回缺失说明；可以运行或无法判定时返回 null</returns>
    public static String? CheckRuntime(String? dir, String name, Func<String, IList<String>>? installed = null)
    {
        if (dir.IsNullOrEmpty() || name.IsNullOrEmpty()) return null;

        var file = Path.Combine(dir, name + ".runtimeconfig.json").GetFullPath();
        if (!File.Exists(file)) return null;

        var options = ReadRuntimeOptions(file);
        if (options == null) return null;

        // 自包含发布不依赖本机运行时
        if (options.ContainsKey("includedFrameworks")) return null;

        var required = new List<String[]>();
        CollectFrameworks(options["framework"], required);
        if (options["frameworks"] is IList<Object> fws)
        {
            foreach (var item in fws) CollectFrameworks(item, required);
        }
        if (required.Count == 0) return null;

        var rollForward = GetRollForward(options);

        installed ??= GetInstalledVersions;

        var missing = new List<String>();
        foreach (var item in required)
        {
            var framework = item[0];
            var version = item[1];

            var versions = installed(framework);

            // 探测不到任何已安装版本时无法判定，交给冒烟测试
            if (versions == null || versions.Count == 0) continue;

            var satisfied = false;
            foreach (var ver in versions)
            {
                if (IsSatisfied(ver, version, rollForward))
                {
                    satisfied = true;
                    break;
                }
            }
            if (satisfied) continue;

            missing.Add($"{framework} {version} (installed: {versions.Join(",")})");
        }

        return missing.Count == 0 ? null : $"Requires {missing.Join("; ")}";
    }

    #region 辅助
    private static IDictionary<String, Object?>? ReadRuntimeOptions(String file)
    {
        try
        {
            var dic = JsonParser.Decode(File.ReadAllText(file));

            return dic != null && dic["runtimeOptions"] is IDictionary<String, Object?> options ? options : null;
        }
        catch
        {
            // 解析不了就不再判定，交给冒烟测试
            return null;
        }
    }

    private static void CollectFrameworks(Object? value, List<String[]> list)
    {
        if (value is not IDictionary<String, Object?> dic) return;

        var name = dic["name"] as String;
        var version = dic["version"] as String;
        if (!name.IsNullOrEmpty() && !version.IsNullOrEmpty()) list.Add([name, version]);
    }

    private static Boolean IsSatisfied(String installed, String required, String? rollForward)
    {
        if (!TryParse(installed, out var ver) || !TryParse(required, out var target)) return installed.EqualIgnoreCase(required);

        // 预览版运行时：默认不作为候选；即便放开，也不能用同版本或更低版本的预览版顶替正式版
        if (HasPrerelease(installed) && !HasPrerelease(required) && (!PrereleaseEnabled || ver <= target)) return false;

        // 策略可写成逗号分隔的多个值，命中任意一个即可
        foreach (var item in (rollForward + "").Split(',', ';'))
        {
            if (IsSatisfied(ver, target, item.Trim())) return true;
        }

        return false;
    }

    /// <summary>按单个前滚策略判断已安装版本是否满足要求</summary>
    /// <param name="installed">已安装版本</param>
    /// <param name="required">要求的版本</param>
    /// <param name="rollForward">前滚策略，空表示默认 Minor</param>
    /// <returns>是否满足</returns>
    /// <remarks>
    /// 语义与 .NET 主机一致：Disable 要求精确版本；Major/LatestMajor 允许跨主版本；
    /// 其余策略（默认 Minor、LatestPatch、LatestMinor）只允许同一主版本内向前，且补丁号不得低于要求。
    /// </remarks>
    private static Boolean IsSatisfied(Version installed, Version required, String? rollForward)
    {
        if (rollForward.EqualIgnoreCase("Disable")) return installed == required;

        if (rollForward.EqualIgnoreCase("Major", "LatestMajor")) return installed >= required;

        return installed.Major == required.Major && installed >= required;
    }

    /// <summary>是否为预览版版本号</summary>
    /// <param name="version">版本号</param>
    /// <returns>预览版返回 true</returns>
    private static Boolean HasPrerelease(String? version) => !version.IsNullOrEmpty() && version.IndexOf('-') > 0;

    /// <summary>是否允许预览版参与前滚</summary>
    private static Boolean PrereleaseEnabled => Environment.GetEnvironmentVariable("DOTNET_ROLL_FORWARD_TO_PRERELEASE").EqualIgnoreCase("1", "true");

    /// <summary>读取前滚策略。环境变量优先于 runtimeconfig，与 .NET 主机一致</summary>
    /// <param name="options">runtimeOptions 节点</param>
    /// <returns>前滚策略；未指定时返回 null</returns>
    private static String? GetRollForward(IDictionary<String, Object?> options)
    {
        var value = Environment.GetEnvironmentVariable("DOTNET_ROLL_FORWARD");
        if (!value.IsNullOrEmpty()) return value;

        value = options["rollForward"] as String;
        if (!value.IsNullOrEmpty()) return value;

        return options["configProperties"] is IDictionary<String, Object?> props
            ? props["System.RuntimeFramework.RollForward"] as String
            : null;
    }

    private static Boolean TryParse(String? value, out Version version)
    {
        version = null!;
        if (value.IsNullOrEmpty()) return false;

        // 预览版形如 9.0.0-preview.7.24405.7，主次版本号之后的部分不参与比较
        var p = value.IndexOfAny(['-', '+']);
        if (p > 0) value = value[..p];

        return Version.TryParse(value, out version!);
    }

    /// <summary>枚举本机已安装的指定框架版本</summary>
    /// <param name="framework">框架名，如 Microsoft.NETCore.App</param>
    /// <returns>版本号列表</returns>
    private static IList<String> GetInstalledVersions(String framework)
    {
        var list = new List<String>();

        foreach (var root in GetDotNetRoots())
        {
            try
            {
                var dir = Path.Combine(root, "shared", framework);
                if (!Directory.Exists(dir)) continue;

                foreach (var item in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(item);
                    if (!name.IsNullOrEmpty() && !list.Contains(name)) list.Add(name);
                }
            }
            catch
            {
                // 目录不可访问时跳过
            }
        }

        return list;
    }

    /// <summary>枚举可能的 .NET 安装根目录</summary>
    /// <returns>安装根目录列表</returns>
    private static IList<String> GetDotNetRoots()
    {
        var list = new List<String>();

        var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!root.IsNullOrEmpty()) list.Add(root);

        // 当前进程所加载运行时的安装根目录：<root>/shared/Microsoft.NETCore.App/<version>
        var core = typeof(Object).Assembly.Location;
        if (!core.IsNullOrEmpty())
        {
            var dir = Path.GetDirectoryName(core);
            for (var i = 0; i < 3 && !dir.IsNullOrEmpty(); i++)
            {
                dir = Path.GetDirectoryName(dir);
            }
            if (!dir.IsNullOrEmpty()) list.Add(dir);
        }

        if (Runtime.Windows)
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!dir.IsNullOrEmpty()) list.Add(Path.Combine(dir, "dotnet"));
        }
        else if (Runtime.OSX)
        {
            list.Add("/usr/local/share/dotnet");
            list.Add("/opt/homebrew/share/dotnet");
        }
        else
        {
            list.Add("/usr/share/dotnet");
            list.Add("/usr/lib/dotnet");
            list.Add("/usr/local/share/dotnet");
        }

        var roots = new List<String>();
        foreach (var item in list)
        {
            if (!item.IsNullOrEmpty() && !roots.Contains(item)) roots.Add(item);
        }

        return roots;
    }
    #endregion
}
