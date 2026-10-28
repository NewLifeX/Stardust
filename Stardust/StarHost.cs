using System.IO;
using System.Text;
using NewLife;

namespace Stardust;

/// <summary>宿主启动辅助。用于在日志初始化之前纠正星尘约定</summary>
/// <remarks>
/// 各类宿主在 <c>Program</c> 顶层调用，必须在 <c>XTrace</c> 之前完成。
/// </remarks>
public static class StarHost
{
    /// <summary>首次启动时按星尘约定初始化日志、数据与备份目录</summary>
    /// <param name="logPath">日志目录，如 ../LogServer</param>
    /// <remarks>
    /// 配置文件缺失时，Core 在读取配置的过程中就会建档并初始化文件日志，此时用的是默认目录（如 ../ServerLog）。
    /// 日志器一旦创建就把目录固定下来，之后再改配置也不会搬移文件，于是首轮日志与后续日志分落两个目录，排查时容易漏看。
    /// 这里在配置被读取之前先落一个最小配置文件，把目录固定为星尘约定值；已有配置时不做任何改动。
    /// </remarks>
    public static void InitLogPath(String logPath)
    {
        if (logPath.IsNullOrEmpty()) return;

        var file = "Config/Core.config".GetFullPath();
        if (File.Exists(file)) return;

        var dir = Path.GetDirectoryName(file);
        if (!dir.IsNullOrEmpty() && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var xml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}<Core>{Environment.NewLine}" +
            $"  <LogPath>{logPath}</LogPath>{Environment.NewLine}" +
            $"  <DataPath>../Data</DataPath>{Environment.NewLine}" +
            $"  <BackupPath>../Backup</BackupPath>{Environment.NewLine}" +
            $"</Core>";
        File.WriteAllText(file, xml, new UTF8Encoding(false));
    }
}
