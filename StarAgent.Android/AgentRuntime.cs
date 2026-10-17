using Android.Content;
using Android.OS;
using NewLife;
using NewLife.Log;
using StarAgent.Mobile;

namespace StarAgent.Droid;

/// <summary>Android代理运行时。持有全局唯一的代理实例，供界面与前台服务共用</summary>
public static class AgentRuntime
{
    private static readonly Object _lock = new();
    private static MobileAgent? _agent;
    private static Action<String>? _logSink;

    /// <summary>当前代理实例</summary>
    public static MobileAgent? Agent => _agent;

    /// <summary>平台适配</summary>
    public static AndroidPlatform? Platform { get; private set; }

    /// <summary>是否由冒烟测试启动</summary>
    public static Boolean SmokeRequested { get; set; }

    /// <summary>设置界面日志接收器</summary>
    /// <param name="sink">日志回调</param>
    public static void SetLogSink(Action<String>? sink) => _logSink = sink;

    /// <summary>写入界面与系统日志</summary>
    /// <param name="message">日志内容</param>
    public static void Log(String message)
    {
        XTrace.WriteLine(message);

        try { global::Android.Util.Log.Info("StarAgent", message); } catch { }
        try { _logSink?.Invoke(message); } catch { }
    }

    /// <summary>初始化代理。重复调用返回同一实例</summary>
    /// <param name="context">应用上下文</param>
    /// <param name="server">服务端地址。非空时覆盖本地配置</param>
    /// <returns>代理实例</returns>
    public static MobileAgent? Init(Context context, String? server = null)
    {
        lock (_lock)
        {
            if (_agent != null) return _agent;

            try
            {
                Platform = new AndroidPlatform(context.ApplicationContext ?? context);

                // 先确定配置与日志目录，再启用文件日志
                MobileAgent.Prepare(Platform);
                try { XTrace.UseConsole(false, true); } catch { }

                // 让 MachineInfo 也能拿到 Android 内存与电量
                MachineInfo.Provider ??= new AndroidMachineInfoProvider(Platform.Context);

                _agent = new MobileAgent(Platform, server);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                Log("初始化失败：" + ex.Message);
            }

            return _agent;
        }
    }

    /// <summary>启动前台服务</summary>
    /// <param name="context">上下文</param>
    public static void StartService(Context context)
    {
        var intent = new Intent(context, typeof(AgentService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    /// <summary>立即执行一次心跳</summary>
    /// <returns>结果说明</returns>
    public static async Task<String> PingOnceAsync()
    {
        var agent = _agent;
        if (agent == null) return "代理未初始化";

        var ok = await agent.PingOnceAsync().ConfigureAwait(false);
        var msg = ok ? $"心跳成功，服务端：{agent.Setting.Server}" : $"心跳失败：{agent.LastError}";

        Log(ok ? $"=== Ping ok === {msg}" : $"=== Ping fail === {msg}");

        return msg;
    }
}