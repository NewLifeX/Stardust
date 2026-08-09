using System.Text;
using NewLife;
using NewLife.Log;
using Xunit.Abstractions;

namespace Stardust.McpClientTests;

/// <summary>测试项目专用日志捕获器。继承自 NewLife.Log.Logger，接管 XTrace 输出并缓冲所有服务端日志条目，
/// 供测试核对「日志输出是否正确」——不仅看断言通过，还要确认服务端日志无 [McpService] 异常。
/// 构造时通过 Install 把转发目标设为原 XTrace.Log，保留控制台/文件输出。</summary>
public sealed class McpTestLog : Logger
{
    private static readonly McpTestLog _instance = new();
    public static McpTestLog Instance => _instance;

    private readonly List<Entry> _entries = new();
    private readonly Object _lock = new();
    private ILog? _next;

    /// <summary>安装捕获器：将当前 XTrace.Log 设为捕获器，并转发给原日志，避免丢失控制台输出</summary>
    public static void Install(ILog? next)
    {
        _instance._next = next;
        XTrace.Log = _instance;
    }

    /// <summary>清空缓冲，便于按测试用例隔离日志</summary>
    public void Reset() { lock (_lock) _entries.Clear(); }

    /// <summary>当前缓冲的所有日志条目（快照）</summary>
    public IReadOnlyList<Entry> Snapshot()
    {
        lock (_lock) return _entries.ToArray();
    }

    /// <summary>错误级别（Error/Fatal）条目数</summary>
    public Int32 CountErrors() => Snapshot().Count(e => e.Level >= LogLevel.Error);

    /// <summary>是否存在包含指定文本的日志（按最低级别过滤）。用于核对关键标记是否出现/不出现</summary>
    public Boolean Contains(String text, LogLevel minLevel = LogLevel.Info)
    {
        if (text.IsNullOrEmpty()) return false;
        return Snapshot().Any(e => e.Level >= minLevel && e.Message.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>将缓冲日志打印到测试输出（ITestOutputHelper），供人工核对</summary>
    public void WriteTo(ITestOutputHelper output)
    {
        output.WriteLine("────── MCP 服务端日志 ──────");
        foreach (var e in Snapshot())
            output.WriteLine($"[{e.Level}] {e.Message}");
        output.WriteLine($"──────────────────────────（共 {_entries.Count} 条）");
    }

    /// <summary>核心写日志：缓冲条目并转发原日志</summary>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        var msg = FormatSafe(format, args);
        lock (_lock) _entries.Add(new Entry(level, msg));
        _next?.Write(level, format, args);
    }

    private static String FormatSafe(String format, Object[]? args)
    {
        if (args == null || args.Length == 0) return format;
        try { return String.Format(format, args); }
        catch { return format; }
    }

    /// <summary>单条日志条目</summary>
    public sealed record Entry(LogLevel Level, String Message);
}
