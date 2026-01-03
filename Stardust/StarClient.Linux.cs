using System.Diagnostics;
using System.Text.RegularExpressions;
using NewLife;
using Stardust.Models;

namespace Stardust;

/// <summary>星星客户端 - Linux平台信息采集</summary>
public partial class StarClient
{
    /// <summary>填充Linux专属信息</summary>
    /// <param name="di">节点信息</param>
    public static void FillOnLinux(NodeInfo di)
    {
        di.MaxOpenFiles = Execute("bash", "-c \"ulimit -n\"")?.Trim().ToInt() ?? 0;

        // 获取GLIBC/musl版本
        di.LibcVersion = GetLibcVersion();

        // 很多Linux系统没有xrandr命令
        //var xrandr = Execute("xrandr", "-q");
        //if (!xrandr.IsNullOrEmpty())
        //{
        //    var current = xrandr.Substring("current", ",").Trim();
        //    if (!current.IsNullOrEmpty())
        //    {
        //        var ss = current.SplitAsInt("x");
        //        if (ss.Length >= 2) di.Resolution = $"{ss[0]}*{ss[1]}";
        //    }
        //}
    }

    #region Libc版本检测
    /// <summary>获取GLIBC或musl libc版本</summary>
    /// <returns></returns>
    private static String? GetLibcVersion()
    {
        // 方式1：尝试通过 ldd --version 获取版本
        var version = GetLibcVersionByLdd();
        if (!version.IsNullOrEmpty()) return version;

        // 方式2：尝试通过动态链接器获取 musl 版本
        version = GetMuslVersionByLoader();
        if (!version.IsNullOrEmpty()) return version;

        // 方式3：尝试通过 /lib 目录下的 libc 文件推断
        version = GetLibcVersionByLibrary();
        if (!version.IsNullOrEmpty()) return version;

        return null;
    }

    /// <summary>通过 ldd --version 获取 libc 版本</summary>
    private static String? GetLibcVersionByLdd()
    {
        // 检查 ldd 是否存在，避免报错
        if (!File.Exists("/usr/bin/ldd") && !File.Exists("/bin/ldd"))
        {
            // 尝试通过 which 命令检测
            var which = Execute("which", "ldd");
            if (which.IsNullOrEmpty()) return null;
        }

        var output = Execute("ldd", "--version");
        if (output.IsNullOrEmpty()) return null;

        // 解析第一行，提取版本号
        var line = output.Split('\n').FirstOrDefault();
        if (line.IsNullOrEmpty()) return null;

        // GNU libc / glibc 格式: ldd (Ubuntu GLIBC 2.31-0ubuntu9.9) 2.31
        // 或: ldd (GNU libc) 2.31
        if (line.Contains("GLIBC") || line.Contains("GNU libc"))
        {
            var parts = line.Split(' ');
            var ver = parts.LastOrDefault()?.Trim();
            if (!ver.IsNullOrEmpty() && Char.IsDigit(ver[0]))
                return $"glibc {ver}";
        }

        // musl libc 格式: musl libc (x86_64) Version 1.2.2
        if (line.Contains("musl"))
        {
            var ver = ExtractVersionNumber(line, "Version");
            if (!ver.IsNullOrEmpty())
                return $"musl {ver}";
        }

        return null;
    }

    /// <summary>通过动态链接器获取 musl 版本</summary>
    private static String? GetMuslVersionByLoader()
    {
        // musl 动态链接器路径列表，覆盖常见架构
        var loaders = new[]
        {
            "/lib/ld-musl-x86_64.so.1",
            "/lib/ld-musl-aarch64.so.1",
            "/lib/ld-musl-armhf.so.1",
            "/lib/ld-musl-arm.so.1",
            "/lib/ld-musl-i386.so.1",
            "/lib/ld-musl-mips.so.1",
            "/lib/ld-musl-mipsel.so.1",
            "/lib/ld-musl-mips64.so.1",
            "/lib/ld-musl-riscv64.so.1",
        };

        foreach (var loader in loaders)
        {
            if (!File.Exists(loader)) continue;

            // 直接执行动态链接器，它会输出版本信息到 stderr
            var output = ExecuteWithStdErr(loader, "");
            if (!output.IsNullOrEmpty() && output.Contains("musl"))
            {
                // 查找包含 Version 的行
                var line = output.Split('\n').FirstOrDefault(e => e.Contains("Version"));
                if (!line.IsNullOrEmpty())
                {
                    var ver = ExtractVersionNumber(line, "Version");
                    if (!ver.IsNullOrEmpty())
                        return $"musl {ver}";
                }
            }
        }

        return null;
    }

    /// <summary>通过 /lib 目录下的 libc 文件推断版本</summary>
    private static String? GetLibcVersionByLibrary()
    {
        var libPaths = new[] { "/lib", "/lib64", "/lib/x86_64-linux-gnu", "/lib/aarch64-linux-gnu" };

        foreach (var libPath in libPaths)
        {
            if (!Directory.Exists(libPath)) continue;

            try
            {
                // 1. 查找 glibc: libc.so.6 或 libc-2.31.so
                var version = FindGlibcVersion(libPath);
                if (!version.IsNullOrEmpty()) return version;

                // 2. 查找 musl: libc.musl-*
                version = FindMuslVersion(libPath);
                if (!version.IsNullOrEmpty()) return version;

                // 3. 查找 uClibc: libuClibc-*
                version = FindUClibcVersion(libPath);
                if (!version.IsNullOrEmpty()) return version;
            }
            catch { }
        }

        return null;
    }

    /// <summary>在指定目录查找 glibc 版本</summary>
    /// <param name="libPath">库文件目录</param>
    private static String? FindGlibcVersion(String libPath)
    {
        var files = Directory.GetFiles(libPath, "libc.so*");
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);

            // 从文件名提取版本: libc-2.31.so
            if (name.StartsWith("libc-") && name.EndsWith(".so"))
            {
                var ver = name[5..^3];
                if (!ver.IsNullOrEmpty() && Char.IsDigit(ver[0]))
                    return $"glibc {ver}";
            }

            // 从 libc.so.6 文件内容提取版本
            if (name == "libc.so.6" || name == "libc.so")
            {
                var ver = GetGlibcVersionByStrings(file);
                if (!ver.IsNullOrEmpty())
                    return $"glibc {ver}";
            }
        }

        return null;
    }

    /// <summary>在指定目录查找 musl 版本</summary>
    /// <param name="libPath">库文件目录</param>
    private static String? FindMuslVersion(String libPath)
    {
        var files = Directory.GetFiles(libPath, "libc.musl*");
        if (files.Length == 0) return null;

        // 尝试从文件内容提取版本
        var ver = GetMuslVersionByStrings(files[0]);
        if (!ver.IsNullOrEmpty())
            return $"musl {ver}";

        // 至少知道是 musl
        return "musl";
    }

    /// <summary>在指定目录查找 uClibc 版本</summary>
    /// <param name="libPath">库文件目录</param>
    private static String? FindUClibcVersion(String libPath)
    {
        var files = Directory.GetFiles(libPath, "libuClibc*");
        if (files.Length == 0) return null;

        var name = Path.GetFileName(files[0]);

        // 从文件名提取版本: libuClibc-1.0.34.so
        if (name.StartsWith("libuClibc-") && name.EndsWith(".so"))
        {
            var ver = name[10..^3];
            if (!ver.IsNullOrEmpty() && Char.IsDigit(ver[0]))
                return $"uClibc {ver}";
        }

        // 尝试从文件内容提取版本
        var version = GetUClibcVersionByStrings(files[0]);
        if (!version.IsNullOrEmpty())
            return $"uClibc {version}";

        return "uClibc";
    }
    #endregion

    #region Strings命令提取版本
    /// <summary>通过 strings 命令从 glibc 文件提取版本</summary>
    /// <param name="libcPath">libc文件路径</param>
    private static String? GetGlibcVersionByStrings(String libcPath)
    {
        // 使用简单的 grep 获取包含 GLIBC_ 的行，然后在代码中解析
        var output = Execute("sh", $"-c \"strings '{libcPath}' 2>/dev/null | grep GLIBC_ | head -50\"");
        if (output.IsNullOrEmpty()) return null;

        // 解析所有 GLIBC_x.xx 版本，取最大值
        Version? maxVersion = null;
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            // 匹配 GLIBC_2.38 格式
            var match = Regex.Match(line, @"GLIBC_(\d+\.\d+)");
            if (match.Success && Version.TryParse(match.Groups[1].Value, out var ver))
            {
                if (maxVersion == null || ver > maxVersion)
                    maxVersion = ver;
            }
        }

        return maxVersion?.ToString();
    }

    /// <summary>通过 strings 命令从 musl 文件提取版本</summary>
    /// <param name="libcPath">libc文件路径</param>
    private static String? GetMuslVersionByStrings(String libcPath)
    {
        // 获取包含 musl 或 Version 的行
        var output = Execute("sh", $"-c \"strings '{libcPath}' 2>/dev/null | grep -i version | head -20\"");
        if (output.IsNullOrEmpty()) return null;

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            // 查找 "Version x.x.x" 格式
            var ver = ExtractVersionNumber(line, "Version");
            if (!ver.IsNullOrEmpty())
                return ver;
        }

        return null;
    }

    /// <summary>通过 strings 命令从 uClibc 文件提取版本</summary>
    /// <param name="libcPath">libc文件路径</param>
    private static String? GetUClibcVersionByStrings(String libcPath)
    {
        // 获取包含 uClibc 的行
        var output = Execute("sh", $"-c \"strings '{libcPath}' 2>/dev/null | grep -i uclibc | head -10\"");
        if (output.IsNullOrEmpty()) return null;

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            // 匹配 uClibc-1.0.34 或 uClibc 1.0.34 格式
            var match = Regex.Match(line, @"uClibc[- ]?(\d+\.\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>从文本中提取版本号</summary>
    /// <param name="text">包含版本信息的文本</param>
    /// <param name="prefix">版本号前缀关键字，如 "Version"</param>
    private static String? ExtractVersionNumber(String text, String prefix)
    {
        var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var ver = text[(idx + prefix.Length)..].Trim();

        // 取第一个空格或换行前的内容
        var endIdx = ver.IndexOfAny([' ', '\n', '\r', '\t']);
        if (endIdx > 0) ver = ver[..endIdx];

        // 验证是否为有效版本号
        if (!ver.IsNullOrEmpty() && Char.IsDigit(ver[0]))
            return ver;

        return null;
    }
    #endregion

    #region 进程执行
    /// <summary>执行命令并获取标准输出和错误输出</summary>
    /// <param name="cmd">命令</param>
    /// <param name="arguments">参数</param>
    private static String? ExecuteWithStdErr(String cmd, String? arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(cmd, arguments ?? "")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(psi);
            if (process == null) return null;

            if (!process.WaitForExit(3_000))
            {
                process.Kill();
                return null;
            }

            // musl 动态链接器将版本信息输出到 stderr
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            return !stderr.IsNullOrEmpty() ? stderr : stdout;
        }
        catch
        {
            return null;
        }
    }
    #endregion
}
