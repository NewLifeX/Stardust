using BackgroundTasks;
using Foundation;
using UIKit;

namespace StarAgent.Ios;

/// <summary>iOS应用代理。负责后台任务注册、前后台心跳与场景配置</summary>
[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    /// <summary>启动完成</summary>
    /// <param name="application">应用</param>
    /// <param name="launchOptions">启动参数</param>
    /// <returns></returns>
    public override Boolean FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        RegisterBackgroundTasks();
        AgentRuntime.Start();

        return true;
    }

    /// <summary>获取场景配置</summary>
    /// <param name="application">应用</param>
    /// <param name="connectingSceneSession">场景会话</param>
    /// <param name="options">连接参数</param>
    /// <returns></returns>
    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        => new("Default Configuration", connectingSceneSession.Role);

    /// <summary>丢弃场景</summary>
    /// <param name="application">应用</param>
    /// <param name="sceneSessions">场景集合</param>
    public override void DidDiscardSceneSessions(UIApplication application, NSSet<UISceneSession> sceneSessions) { }

    /// <summary>进入后台。提交后台刷新与后台处理任务</summary>
    /// <param name="application">应用</param>
    public override void DidEnterBackground(UIApplication application)
    {
        ScheduleRefresh();
        ScheduleProcessing();
    }

    /// <summary>回到前台。补一次心跳并拉取待处理指令</summary>
    /// <param name="application">应用</param>
    public override void OnActivated(UIApplication application)
    {
        _ = AgentRuntime.PingOnceAsync();
    }

    private static void RegisterBackgroundTasks()
    {
        BGTaskScheduler.Shared.Register("com.newlife.staragent.refresh", null, task => HandleTask(task, true));
        BGTaskScheduler.Shared.Register("com.newlife.staragent.processing", null, task => HandleTask(task, false));
    }

    private static void HandleTask(BGTask task, Boolean refresh)
    {
        if (refresh)
            ScheduleRefresh();
        else
            ScheduleProcessing();

        task.ExpirationHandler = () => { };

        _ = Task.Run(async () =>
        {
            var ok = await AgentRuntime.PingOnceAsync().ConfigureAwait(false);
            task.SetTaskCompleted(ok);
        });
    }

    private static void ScheduleRefresh()
    {
        try
        {
            var request = new BGAppRefreshTaskRequest("com.newlife.staragent.refresh")
            {
                EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(15 * 60),
            };
            BGTaskScheduler.Shared.Submit(request, out _);
        }
        catch { }
    }

    private static void ScheduleProcessing()
    {
        try
        {
            var request = new BGProcessingTaskRequest("com.newlife.staragent.processing")
            {
                EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(60 * 60),
                RequiresNetworkConnectivity = true,
            };
            BGTaskScheduler.Shared.Submit(request, out _);
        }
        catch { }
    }
}