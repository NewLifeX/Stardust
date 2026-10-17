using Android.App;
using Android.Content;

namespace StarAgent.Droid;

/// <summary>开机自启接收器。设备重启后拉起前台服务</summary>
[BroadcastReceiver(Name = "com.newlife.staragent.BootReceiver", Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootReceiver : BroadcastReceiver
{
    /// <summary>接收开机广播</summary>
    /// <param name="context">上下文</param>
    /// <param name="intent">意图</param>
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        AgentRuntime.StartService(context);
    }
}