using System;
using System.Diagnostics;
using System.IO;
using NewLife;
using NewLife.Log;

namespace StarAgent;

/// <summary>star 命令别名。Windows 创建 star.cmd，Linux/macOS 创建 star 脚本</summary>
public static class StarLauncher
{
    /// <summary>确保 star 命令别名存在</summary>
    /// <returns>是否成功</returns>
    public static Boolean EnsureAlias()
    {
        try
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            if (dir.IsNullOrEmpty()) return false;
            dir = dir.TrimEnd('/', '\\');

            if (Runtime.Windows)
            {
                var exe = Path.Combine(dir, "StarAgent.exe");
                var content = File.Exists(exe)
                    ? $"@echo off\r\n\"{dir}\\StarAgent.exe\" %*\r\n"
                    : $"@echo off\r\ndotnet \"{dir}\\StarAgent.dll\" %*\r\n";
                var cmd = Path.Combine(dir, "star.cmd");

                if (!File.Exists(cmd) || File.ReadAllText(cmd) != content)
                {
                    File.WriteAllText(cmd, content);
                    XTrace.WriteLine("Created star command: {0}", cmd);
                }
            }
            else
            {
                var script = Path.Combine(dir, "star");
                var content = "#!/bin/sh\n" +
                    $"DIR=\"{dir}\"\n" +
                    "if [ -x \"$DIR/StarAgent\" ]; then exec \"$DIR/StarAgent\" \"$@\"; fi\n" +
                    "exec dotnet \"$DIR/StarAgent.dll\" \"$@\"\n";

                if (!File.Exists(script) || File.ReadAllText(script) != content)
                {
                    File.WriteAllText(script, content);
                    Process.Start("chmod", $"+x \"{script}\"")?.WaitForExit(5_000);
                    XTrace.WriteLine("Created star command: {0}", script);
                }

                // 尽量建立 /usr/local/bin/star，便于直接调用；无权限时忽略
                try
                {
                    if (Directory.Exists("/usr/local/bin"))
                    {
                        Process.Start("ln", $"-sf \"{script}\" \"/usr/local/bin/star\"")?.WaitForExit(5_000);
                    }
                }
                catch
                {
                    // 忽略权限不足
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Failed to create star command: {0}", ex.Message);
            return false;
        }
    }
}
