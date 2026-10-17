using Foundation;
using NewLife;
using NewLife.Log;
using StarAgent.Mobile;

namespace StarAgent.Ios;

/// <summary>iOS代理运行时</summary>
public static class AgentRuntime
{
    private static readonly Object _lock = new();
    private static MobileAgent? _agent;
    private static IosPlatform? _platform;
    private static Action<String>? _logSink;

    /// <summary>当前代理实例</summary>
    public static MobileAgent? Agent => _agent;

    /// <summary>是否由冒烟测试启动</summary>
    public static Boolean SmokeRequested { get; private set; }

    /// <summary>设置界面日志接收器</summary>
    /// <param name="sink">日志回调</param>
    public static void SetLogSink(Action<String>? sink) => _logSink = sink;

    /// <summary>写入界面与系统日志</summary>
    /// <param name="message">日志内容</param>
    public static void Log(String message)
    {
        XTrace.WriteLine(message);

        try { Console.WriteLine("[StarAgent] " + message); } catch { }
        try { _logSink?.Invoke(message); } catch { }
    }

    /// <summary>启动代理。重复调用会复用同一实例</summary>
    public static void Start()
    {
        lock (_lock)
        {
            try
            {
                SmokeRequested = NSProcessInfo.ProcessInfo.Arguments.Any(e => e.Equals("--smoke", StringComparison.OrdinalIgnoreCase));

                if (_agent == null)
                {
                    _platform ??= new IosPlatform();

                    // 先确定配置与日志目录，再启用文件日志
                    MobileAgent.Prepare(_platform);
                    try { XTrace.UseConsole(false, true); } catch { }

                    // 让 MachineInfo 也能拿到 iOS 内存与电量
                    MachineInfo.Provider ??= new IosMachineInfoProvider();

                    _agent = new MobileAgent(_platform);
                }

                _agent.Start();

                if (SmokeRequested) _ = RunSmokeAsync();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                Log("启动失败：" + ex.Message);
            }
        }
    }

    /// <summary>立即执行一次心跳</summary>
    /// <returns>是否成功</returns>
    public static async Task<Boolean> PingOnceAsync()
    {
        var agent = _agent;
        if (agent == null)
        {
            Start();
            agent = _agent;
        }
        if (agent == null) return false;

        var ok = await agent.PingOnceAsync().ConfigureAwait(false);
        Log(ok ? "=== Ping ok ===" : "=== Ping fail === " + agent.LastError);

        return ok;
    }

    private static async Task RunSmokeAsync()
    {
        // 等待登录完成，最多15秒
        for (var i = 0; i < 15; i++)
        {
            if (_agent?.Client.Logined == true) break;
            await Task.Delay(1_000).ConfigureAwait(false);
        }

        var ok = await PingOnceAsync().ConfigureAwait(false);
        var server = _agent?.Setting.Server ?? "";
        var error = _agent?.LastError ?? "";

        try
        {
            var dir = _platform?.GetDataPath();
            if (!dir.IsNullOrEmpty())
            {
                var json = $"{{\"ok\":{(ok ? "true" : "false")},\"server\":\"{Escape(server)}\",\"error\":\"{Escape(error)}\",\"time\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"}}";
                File.WriteAllText(Path.Combine(dir, "agent-smoke.json"), json);
            }
        }
        catch { }

        Log(ok ? "=== Agent ready ===" : "=== Agent fail ===");
    }

    private static String Escape(String value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}