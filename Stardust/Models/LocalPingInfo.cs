namespace Stardust.Models;

/// <summary>本地心跳</summary>
public class LocalPingInfo
{
    /// <summary>进程标识</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>进程名称</summary>
    public String? ProcessName { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>看门狗超时时间。默认0秒</summary>
    /// <remarks>
    /// 设置看门狗超时时间，超过该时间未收到心跳，将会重启本应用进程。
    /// 0秒表示不启用看门狗。
    /// </remarks>
    public Int32 WatchdogTimeout { get; set; }
}
