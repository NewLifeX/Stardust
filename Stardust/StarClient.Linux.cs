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

        // 采集桌面Linux的分辨率和DPI
        FillDisplayInfo(di);

        // GPU信息
        di.GPU = GetGpuInfoLinux();
    }

    /// <summary>填充Linux心跳专属信息</summary>
    /// <param name="request">心跳请求</param>
    public static void FillPingOnLinux(PingInfo request)
    {
        try
        {
            // 获取系统负载
            request.SystemLoad = GetLoad1();

            // 获取磁盘IOPS
            request.DiskIOPS = GetDiskIOPSLinux();
        }
        catch { }
    }

    private static Double GetLoad1()
    {
        try
        {
            // 读取 /proc/loadavg，格式: "0.00 0.01 0.05 1/156 1234"
            // 第一个数字是1分钟平均负载
            var file = "/proc/loadavg";
            if (File.Exists(file))
            {
                var content = File.ReadAllText(file);
                var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    return parts[0].ToDouble();
            }
        }
        catch { }
        return 0;
    }

    private static Int32 GetDiskIOPSLinux()
    {
        try
        {
            // 读取 /proc/diskstats
            // 格式: 主设备号 次设备号 设备名 读完成次数 ... 写完成次数 ...
            // 例如: "8 0 sda 12345 ... 67890 ..."
            var file = "/proc/diskstats";
            if (!File.Exists(file)) return 0;

            var lines = File.ReadAllLines(file);
            var totalReads = 0L;
            var totalWrites = 0L;

            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 14) continue;

                var deviceName = parts[2];
                // 只统计主磁盘（如sda、nvme0n1等），跳过分区（如sda1）
                if (deviceName.StartsWith("loop") || deviceName.StartsWith("ram") || deviceName.StartsWith("dm-"))
                    continue;
                if (Regex.IsMatch(deviceName, @"^[a-z]+\d+$") && !deviceName.StartsWith("nvme"))
                    continue;
                if (Regex.IsMatch(deviceName, @"^nvme\d+n\d+p\d+$"))
                    continue;

                // 第4列是读完成次数，第8列是写完成次数
                if (parts.Length > 3) totalReads += parts[3].ToLong();
                if (parts.Length > 7) totalWrites += parts[7].ToLong();
            }

            // 这里返回的是累计值，实际IOPS需要两次采样计算差值
            // 为简化处理，这里返回累计值除以开机时间估算
            var uptime = GetUptime();
            if (uptime > 0)
                return (Int32)((totalReads + totalWrites) / uptime);

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static Double GetUptime()
    {
        try
        {
            var file = "/proc/uptime";
            if (File.Exists(file))
            {
                var content = File.ReadAllText(file);
                var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    return parts[0].ToDouble();
            }
        }
        catch { }
        return 0;
    }

    private static String? GetGpuInfoLinux()
    {
        try
        {
            var gpuList = new List<String>();

            // 方式1：通过 lspci 获取显卡信息
            var rs = Execute("lspci", null);
            if (!rs.IsNullOrEmpty())
            {
                var lines = rs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // 查找 VGA compatible controller 或 3D controller
                    if (line.Contains("VGA compatible controller") || line.Contains("3D controller"))
                    {
                        // 格式: "00:02.0 VGA compatible controller: Intel Corporation ..."
                        var idx = line.IndexOf(':');
                        if (idx > 0)
                        {
                            idx = line.IndexOf(':', idx + 1);
                            if (idx > 0)
                            {
                                var name = line[(idx + 1)..].Trim();
                                // 移除括号中的详细型号，保留主要信息
                                var bracketIdx = name.IndexOf('(');
                                if (bracketIdx > 0) name = name[..bracketIdx].Trim();

                                if (!name.IsNullOrEmpty() && !gpuList.Contains(name))
                                    gpuList.Add(name);
                            }
                        }
                    }
                }
            }

            // 方式2：通过 nvidia-smi 获取NVIDIA显卡信息（如果存在）
            if (gpuList.Count == 0)
            {
                rs = Execute("nvidia-smi", "-L");
                if (!rs.IsNullOrEmpty())
                {
                    var lines = rs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        // 格式: "GPU 0: NVIDIA GeForce RTX 3080 (UUID: ...)"
                        if (line.StartsWith("GPU"))
                        {
                            var match = Regex.Match(line, @"GPU \d+: (.+?)\s*\(");
                            if (match.Success)
                            {
                                var name = match.Groups[1].Value.Trim();
                                if (!name.IsNullOrEmpty() && !gpuList.Contains(name))
                                    gpuList.Add(name);
                            }
                        }
                    }
                }
            }

            return gpuList.Count > 0 ? gpuList.Join(",") : null;
        }
        catch
        {
            return null;
        }
    }

    #region 显示信息采集
    /// <summary>填充显示信息（分辨率和DPI）</summary>
    /// <param name="di">节点信息</param>
    private static void FillDisplayInfo(NodeInfo di)
    {
        try
        {
            // 方式1：通过 xrandr 获取分辨率
            if (di.Resolution.IsNullOrEmpty())
                di.Resolution = GetResolutionByXrandr();

            // 方式2：通过 /sys/class/drm 获取分辨率（无需X环境）
            if (di.Resolution.IsNullOrEmpty())
                di.Resolution = GetResolutionByDrm();

            // 方式1：通过 xdpyinfo 获取 DPI
            if (di.Dpi.IsNullOrEmpty())
                di.Dpi = GetDpiByXdpyinfo();

            // 方式2：通过 xrdb 获取 Xft.dpi 设置
            if (di.Dpi.IsNullOrEmpty())
                di.Dpi = GetDpiByXrdb();

            // 方式3：通过 GNOME/KDE 配置获取缩放比例推算 DPI
            if (di.Dpi.IsNullOrEmpty())
                di.Dpi = GetDpiByGSettings();
        }
        catch { }
    }

    /// <summary>通过 xrandr 获取当前分辨率</summary>
    private static String? GetResolutionByXrandr()
    {
        try
        {
            // 检查 DISPLAY 环境变量，没有则无法使用 X 命令
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (display.IsNullOrEmpty()) return null;

            var output = Execute("xrandr", "--current");
            if (output.IsNullOrEmpty()) return null;

            // 解析 "current 1920 x 1080" 格式
            var match = Regex.Match(output, @"current\s+(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var w = match.Groups[1].Value.ToInt();
                var h = match.Groups[2].Value.ToInt();
                if (w > 0 && h > 0) return $"{w}*{h}";
            }

            // 备选：解析带 "*" 标记的当前模式行，如 "1920x1080     60.00*+"
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (!line.Contains('*')) continue;

                match = Regex.Match(line.Trim(), @"^(\d+)x(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var w = match.Groups[1].Value.ToInt();
                    var h = match.Groups[2].Value.ToInt();
                    if (w > 0 && h > 0) return $"{w}*{h}";
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>通过 /sys/class/drm 获取分辨率（无需X环境）</summary>
    private static String? GetResolutionByDrm()
    {
        try
        {
            var drmPath = "/sys/class/drm";
            if (!Directory.Exists(drmPath)) return null;

            // 查找已连接的显示器
            var cards = Directory.GetDirectories(drmPath, "card*-*");
            foreach (var card in cards)
            {
                try
                {
                    var statusFile = Path.Combine(card, "status");
                    var modesFile = Path.Combine(card, "modes");

                    // 检查连接状态
                    if (File.Exists(statusFile))
                    {
                        var status = File.ReadAllText(statusFile).Trim();
                        if (!status.EqualIgnoreCase("connected")) continue;
                    }

                    // 读取支持的模式，第一行通常是当前/最佳模式
                    if (File.Exists(modesFile))
                    {
                        var modes = File.ReadAllLines(modesFile);
                        if (modes.Length > 0)
                        {
                            var mode = modes[0].Trim();
                            var match = Regex.Match(mode, @"^(\d+)x(\d+)");
                            if (match.Success)
                            {
                                var w = match.Groups[1].Value.ToInt();
                                var h = match.Groups[2].Value.ToInt();
                                if (w > 0 && h > 0) return $"{w}*{h}";
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    /// <summary>通过 xdpyinfo 获取 DPI</summary>
    private static String? GetDpiByXdpyinfo()
    {
        try
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (display.IsNullOrEmpty()) return null;

            var output = Execute("xdpyinfo", null);
            if (output.IsNullOrEmpty()) return null;

            // 解析 "resolution:    96x96 dots per inch" 格式
            var match = Regex.Match(output, @"resolution:\s*(\d+)x(\d+)\s*dots per inch", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var dpiX = match.Groups[1].Value.ToInt();
                var dpiY = match.Groups[2].Value.ToInt();
                if (dpiX > 0 && dpiY > 0) return $"{dpiX}*{dpiY}";
            }
        }
        catch { }

        return null;
    }

    /// <summary>通过 xrdb 获取 Xft.dpi 设置</summary>
    private static String? GetDpiByXrdb()
    {
        try
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (display.IsNullOrEmpty()) return null;

            var output = Execute("xrdb", "-query");
            if (output.IsNullOrEmpty()) return null;

            // 解析 "Xft.dpi:	96" 格式
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (!line.StartsWith("Xft.dpi", StringComparison.OrdinalIgnoreCase)) continue;

                var parts = line.Split(':', '\t');
                if (parts.Length >= 2)
                {
                    var dpi = parts[1].Trim().ToInt();
                    if (dpi > 0) return $"{dpi}*{dpi}";
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>通过 gsettings 获取 GNOME 缩放比例推算 DPI</summary>
    private static String? GetDpiByGSettings()
    {
        try
        {
            // 尝试 GNOME 的缩放因子
            var output = Execute("gsettings", "get org.gnome.desktop.interface scaling-factor");
            if (!output.IsNullOrEmpty())
            {
                // 返回格式: "uint32 1" 或 "1"
                var match = Regex.Match(output, @"(\d+)");
                if (match.Success)
                {
                    var scale = match.Groups[1].Value.ToInt();
                    if (scale > 0)
                    {
                        var dpi = 96 * scale;
                        return $"{dpi}*{dpi}";
                    }
                }
            }

            // 尝试 GNOME 文本缩放因子
            output = Execute("gsettings", "get org.gnome.desktop.interface text-scaling-factor");
            if (!output.IsNullOrEmpty())
            {
                // 返回格式: "1.0" 或 "1.25"
                var scale = output.Trim().Trim('\'').ToDouble();
                if (scale > 0)
                {
                    var dpi = (Int32)(96 * scale);
                    return $"{dpi}*{dpi}";
                }
            }
        }
        catch { }

        return null;
    }
    #endregion

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
