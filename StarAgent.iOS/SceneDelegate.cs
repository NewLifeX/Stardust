using Foundation;
using ObjCRuntime;
using UIKit;

namespace StarAgent.Ios;

/// <summary>场景代理。创建主窗口并挂载代理控制台</summary>
[Register("SceneDelegate")]
public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    /// <summary>主窗口</summary>
    [Export("window")]
    public UIWindow? Window { get; set; }

    /// <summary>场景连接</summary>
    /// <param name="scene">场景</param>
    /// <param name="session">会话</param>
    /// <param name="connectionOptions">连接参数</param>
    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene) return;

        Window ??= new UIWindow(windowScene);
        Window.RootViewController = new UINavigationController(new AgentViewController());
        Window.MakeKeyAndVisible();
    }
}