using System.Text;
using NewLife;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Managers;

/// <summary>部署应用日志文件读取（节点本地）。由 ServiceManager 注册 deploy/log/* 指令。</summary>
public static class DeployLogCommands
{
    public const Int32 MaxListFiles = 100;
    public const Int32 MaxTailBytes = 512 * 1024;

    public static Task<CommandReplyModel> ListAsync(CommandModel cmd)
    {
        try
        {
            var arg = cmd.Argument?.ToJsonEntity<DeployLogListArg>();
            if (arg == null) return Task.FromResult(Error(L.T("Invalid parameter.")));

            var root = ResolveLogRoot(arg.LogDirectory, arg.WorkingDirectory);
            if (root.IsNullOrEmpty()) return Task.FromResult(Error(L.T("Log directory is not configured.")));
            if (!Directory.Exists(root)) return Task.FromResult(Error(L.T("Log directory not found: {0}", root)));

            var pattern = arg.Pattern.IsNullOrEmpty() ? "*.log" : arg.Pattern!;
            var files = new List<DeployLogFileInfo>();
            foreach (var fi in new DirectoryInfo(root).EnumerateFiles(pattern, SearchOption.TopDirectoryOnly)
                         .OrderByDescending(e => e.LastWriteTimeUtc)
                         .Take(MaxListFiles))
            {
                files.Add(new DeployLogFileInfo
                {
                    Name = fi.Name,
                    Size = fi.Length,
                    LastWriteUtc = fi.LastWriteTimeUtc,
                });
            }

            return Task.FromResult(Ok(new DeployLogListResult { Files = files }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Error(ex.Message));
        }
    }

    public static Task<CommandReplyModel> TailAsync(CommandModel cmd)
    {
        try
        {
            var arg = cmd.Argument?.ToJsonEntity<DeployLogTailArg>();
            if (arg == null || arg.File.IsNullOrEmpty()) return Task.FromResult(Error(L.T("Invalid parameter.")));

            if (arg.File.Contains("..", StringComparison.Ordinal) ||
                arg.File.Contains('/') ||
                arg.File.Contains('\\'))
                return Task.FromResult(Error(L.T("Invalid log file name.")));

            var root = ResolveLogRoot(arg.LogDirectory, arg.WorkingDirectory);
            if (root.IsNullOrEmpty()) return Task.FromResult(Error(L.T("Log directory is not configured.")));

            var full = Path.GetFullPath(Path.Combine(root, arg.File!));
            var rootFull = Path.GetFullPath(root);
            if (!full.StartsWith(rootFull, Runtime.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return Task.FromResult(Error(L.T("Access denied.")));

            if (!File.Exists(full)) return Task.FromResult(Error(L.T("Log file not found: {0}", arg.File)));

            var maxBytes = arg.MaxBytes <= 0 ? 256 * 1024 : Math.Min(arg.MaxBytes, MaxTailBytes);
            var fi = new FileInfo(full);
            var readLen = (Int32)Math.Min(maxBytes, fi.Length);
            var offset = arg.Offset;
            if (offset < 0) offset = 0;
            if (offset >= fi.Length) offset = Math.Max(0, fi.Length - readLen);

            var start = fi.Length > readLen ? fi.Length - readLen : 0;
            if (offset > 0 && offset < fi.Length) start = offset;

            var toRead = (Int32)Math.Min(maxBytes, fi.Length - start);
            var buff = new Byte[toRead];
            using (var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(start, SeekOrigin.Begin);
                var read = 0;
                while (read < toRead)
                {
                    var n = fs.Read(buff, read, toRead - read);
                    if (n <= 0) break;
                    read += n;
                }
            }

            var content = Encoding.UTF8.GetString(buff);
            var truncated = start > 0 || fi.Length > start + toRead;

            return Task.FromResult(Ok(new DeployLogTailResult
            {
                Content = content,
                Offset = start,
                FileSize = fi.Length,
                Truncated = truncated,
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Error(ex.Message));
        }
    }

    /// <summary>解析日志根目录：绝对路径直接用；否则相对 WorkingDirectory。</summary>
    public static String? ResolveLogRoot(String? logDirectory, String? workingDirectory)
    {
        if (logDirectory.IsNullOrEmpty()) return null;

        var log = logDirectory!.Trim();
        if (Path.IsPathRooted(log)) return Path.GetFullPath(log);

        if (workingDirectory.IsNullOrEmpty()) return Path.GetFullPath(log);

        return Path.GetFullPath(Path.Combine(workingDirectory.Trim(), log));
    }

    static CommandReplyModel Ok(Object data) => new() { Status = CommandStatus.已完成, Data = data.ToJson() };

    static CommandReplyModel Error(String message) => new() { Status = CommandStatus.错误, Data = message };
}

public class DeployLogListArg
{
    public String? LogDirectory { get; set; }
    public String? WorkingDirectory { get; set; }
    public String? Pattern { get; set; }
}

public class DeployLogTailArg
{
    public String? LogDirectory { get; set; }
    public String? WorkingDirectory { get; set; }
    public String? File { get; set; }
    public Int64 Offset { get; set; }
    public Int32 MaxBytes { get; set; }
}

public class DeployLogListResult
{
    public IList<DeployLogFileInfo> Files { get; set; } = [];
}

public class DeployLogFileInfo
{
    public String Name { get; set; } = null!;
    public Int64 Size { get; set; }
    public DateTime LastWriteUtc { get; set; }
}

public class DeployLogTailResult
{
    public String Content { get; set; } = "";
    public Int64 Offset { get; set; }
    public Int64 FileSize { get; set; }
    public Boolean Truncated { get; set; }
}
