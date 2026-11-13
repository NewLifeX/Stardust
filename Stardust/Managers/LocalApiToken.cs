using System.Diagnostics;
using NewLife;
using NewLife.Log;

namespace Stardust.Managers;

/// <summary>本机 StarAgent 本地 RPC 共享令牌</summary>
/// <remarks>
/// <para>代理的 UDP 本机接口只靠来源地址判断“本机”，挡不住同机低权限进程（甚至被攻陷的普通进程）调用
/// StartService/StopService/RestartService/SetServer，而代理以 root/SYSTEM 运行，等于把这些能力提权开放。</para>
/// <para>代理启动时生成随机令牌写入 <c>local.token</c>（仅属主可读），本机调用方读取同一文件并在请求中带上令牌；
/// <c>Info</c> 保持匿名，供局域网发现与非特权应用做健康探测。</para>
/// <para>令牌位置按“环境变量 → 代理同目录 → 系统共享目录”顺序解析，环境变量便于容器与自定义部署：
/// <c>STAR_LOCAL_TOKEN</c> 直接给出令牌，<c>STAR_LOCAL_TOKEN_FILE</c> 给出令牌文件路径。</para>
/// </remarks>
public static class LocalApiToken
{
    /// <summary>令牌文件名</summary>
    public const String FileName = "local.token";

    /// <summary>直接指定令牌的环境变量。优先级最高</summary>
    public const String EnvToken = "STAR_LOCAL_TOKEN";

    /// <summary>指定令牌文件路径的环境变量</summary>
    public const String EnvFile = "STAR_LOCAL_TOKEN_FILE";

    /// <summary>获取系统级共享令牌文件路径。代理以 root/SYSTEM 运行时才可写入</summary>
    /// <returns>共享令牌文件全路径</returns>
    public static String GetSharedPath()
    {
        if (Runtime.Windows) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StarAgent", FileName);

        return "/etc/staragent/local.token";
    }

    /// <summary>获取当前应使用的令牌文件路径</summary>
    /// <remarks>环境变量优先；其次代理同目录（CLI 与工具通常与代理一起部署）；再次系统共享目录</remarks>
    /// <returns>令牌文件全路径，均不存在时返回代理同目录下的默认路径</returns>
    public static String GetFilePath()
    {
        var file = Environment.GetEnvironmentVariable(EnvFile);
        if (!file.IsNullOrEmpty()) return file.GetFullPath();

        var local = Path.Combine(BaseDirectory, FileName);
        if (File.Exists(local)) return local;

        var shared = GetSharedPath();
        return File.Exists(shared) ? shared : local;
    }

    /// <summary>代理所在目录</summary>
    private static String BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>读取令牌。不存在时返回空</summary>
    /// <returns>令牌字符串</returns>
    public static String? Read()
    {
        var token = Environment.GetEnvironmentVariable(EnvToken);
        if (!token.IsNullOrEmpty()) return token.Trim();

        return ReadFile(GetFilePath());
    }

    /// <summary>确保令牌存在。代理启动时调用，首次生成后长期沿用，重启不换令牌</summary>
    /// <returns>令牌字符串</returns>
    public static String Ensure()
    {
        // 显式指定令牌时不落盘，调用方按同一环境变量读取
        var env = Environment.GetEnvironmentVariable(EnvToken);
        if (!env.IsNullOrEmpty()) return env.Trim();

        var file = Environment.GetEnvironmentVariable(EnvFile);
        if (file.IsNullOrEmpty()) file = Path.Combine(BaseDirectory, FileName);

        var token = ReadFile(file);
        if (!token.IsNullOrEmpty())
        {
            Protect(file);
            return token;
        }

        // 共享位置已有则沿用，避免代理升级或换目录后令牌漂移
        var shared = GetSharedPath();
        if (!shared.EqualIgnoreCase(file)) token = ReadFile(shared);
        if (token.IsNullOrEmpty()) token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        WriteFile(file, token);

        // 共享位置供跨目录调用方读取，无权限时忽略
        if (!shared.EqualIgnoreCase(file))
        {
            try
            {
                WriteFile(shared, token);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("Failed to write the shared local token to [{0}]: {1}", shared, ex.Message);
            }
        }

        return token;
    }

    /// <summary>校验令牌。与本地文件中的令牌做常量时间比较</summary>
    /// <param name="token">调用方携带的令牌</param>
    /// <returns>是否匹配</returns>
    public static Boolean Verify(String? token) => Verify(token, Read());

    /// <summary>校验令牌。常量时间比较，避免按字节比较泄露前缀信息</summary>
    /// <param name="token">调用方携带的令牌</param>
    /// <param name="secret">本地令牌</param>
    /// <returns>是否匹配</returns>
    public static Boolean Verify(String? token, String? secret)
    {
        if (token.IsNullOrEmpty() || secret.IsNullOrEmpty()) return false;

        token = token.Trim();
        if (token.Length != secret.Length) return false;

        var diff = 0;
        for (var i = 0; i < secret.Length; i++) diff |= token[i] ^ secret[i];

        return diff == 0;
    }

    private static String? ReadFile(String file)
    {
        try
        {
            if (!File.Exists(file)) return null;

            var token = File.ReadAllText(file).Trim();
            return token.IsNullOrEmpty() ? null : token;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteFile(String file, String token)
    {
        var dir = Path.GetDirectoryName(file);
        if (!dir.IsNullOrEmpty()) Directory.CreateDirectory(dir);

        try
        {
            File.WriteAllText(file, token);
        }
        catch (UnauthorizedAccessException)
        {
            // 旧文件权限过严（换用户或换部署方式）时删掉重建
            File.Delete(file);
            File.WriteAllText(file, token);
        }

        Protect(file);
    }

    /// <summary>收紧文件权限，仅属主（Windows 为 SYSTEM 与管理员）可读写</summary>
    private static void Protect(String file)
    {
        try
        {
            if (!Runtime.Windows)
            {
#if NET7_0_OR_GREATER
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
#else
                RunTool("chmod", $"600 \"{file}\"");
#endif
                return;
            }

            // Windows 下继承 ProgramData 的“Users 可读”，需要显式去掉继承。
            // 保留当前用户，避免代理换用户运行后读不到自己生成的令牌
            var user = Environment.UserDomainName + "\\" + Environment.UserName;
            var grant = $"\"SYSTEM:F\" \"*S-1-5-32-544:F\"";
            if (!user.IsNullOrEmpty()) grant += $" \"{user}:F\"";

            RunTool("icacls", $"\"{file}\" /inheritance:r /grant:r {grant}");
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Failed to restrict the local token file [{0}]: {1}", file, ex.Message);
        }
    }

    private static void RunTool(String fileName, String arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        process?.WaitForExit(3_000);
    }
}
