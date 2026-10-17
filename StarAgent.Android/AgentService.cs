using Android.App;
using Android.Content;
using Android.OS;

namespace StarAgent.Droid;

/// <summary>星尘代理前台服务。保证应用在后台持续运行并维持心跳</summary>
[Service(Name = "com.newlife.staragent.AgentService", Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeSpecialUse)]
public class AgentService : Service
{
    private const String ChannelId = "staragent";
    private const Int32 NotificationId = 1001;

    /// <summary>创建服务</summary>
    public override void OnCreate()
    {
        base.OnCreate();

        CreateChannel();
        StartForeground(NotificationId, BuildNotification());

        AgentRuntime.Log("=== Agent service ===");
        AgentRuntime.Init(this)?.Start();
    }

    /// <summary>启动服务</summary>
    /// <param name="intent">意图</param>
    /// <param name="flags">标志</param>
    /// <param name="startId">启动编号</param>
    /// <returns></returns>
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, Int32 startId)
    {
        StartForeground(NotificationId, BuildNotification());
        AgentRuntime.Init(this)?.Start();

        return StartCommandResult.Sticky;
    }

    /// <summary>绑定服务</summary>
    /// <param name="intent">意图</param>
    /// <returns>始终返回空</returns>
    public override IBinder? OnBind(Intent? intent) => null;

    private void CreateChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        if (GetSystemService(NotificationService) is not NotificationManager manager) return;

        var channel = new NotificationChannel(ChannelId, "星尘代理", NotificationImportance.Low);
        manager.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        return builder
            .SetContentTitle("星尘代理")
            .SetContentText("正在守护当前设备")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .Build()!;
    }
}