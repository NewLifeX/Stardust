using Foundation;
using UIKit;
using NewLife;

namespace StarAgent.Ios;

/// <summary>星尘代理控制台。展示运行状态与日志，并提供服务端地址配置</summary>
public class AgentViewController : UIViewController
{
    private UILabel? _title;
    private UILabel? _status;
    private UITextField? _server;
    private UIButton? _start;
    private UIButton? _ping;
    private UIButton? _copy;
    private UIButton? _clear;
    private UITextView? _console;
    private Boolean _busy;

    /// <summary>加载界面</summary>
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        View!.BackgroundColor = UIColor.SystemBackground;
        Title = "星尘代理";

        _title = new UILabel
        {
            Text = "星尘代理",
            Font = UIFont.BoldSystemFontOfSize(20),
            TextColor = UIColor.Label,
        };
        View.AddSubview(_title);

        _status = new UILabel
        {
            Text = "状态：未启动",
            Font = UIFont.SystemFontOfSize(13),
            TextColor = UIColor.SecondaryLabel,
            Lines = 2,
        };
        View.AddSubview(_status);

        _server = new UITextField
        {
            Placeholder = "http://star.newlifex.com:6600",
            BorderStyle = UITextBorderStyle.RoundedRect,
            AutocapitalizationType = UITextAutocapitalizationType.None,
            AutocorrectionType = UITextAutocorrectionType.No,
            KeyboardType = UIKeyboardType.Url,
            ReturnKeyType = UIReturnKeyType.Done,
        };
        View.AddSubview(_server);

        _start = MakeButton("保存并启动", StartAgent);
        _ping = MakeButton("立即心跳", () => _ = PingOnce());
        _copy = MakeButton("复制", CopyConsole);
        _clear = MakeButton("清空", () => { if (_console != null) _console.Text = ""; });
        View.AddSubview(_start);
        View.AddSubview(_ping);
        View.AddSubview(_copy);
        View.AddSubview(_clear);

        _console = new UITextView
        {
            Editable = false,
            Font = UIFont.GetMonospacedSystemFont(11, UIFontWeight.Regular),
            TextColor = UIColor.Label,
            BackgroundColor = UIColor.SystemBackground,
        };
        View.AddSubview(_console);

        AgentRuntime.SetLogSink(Append);
        AgentRuntime.Start();

        var agent = AgentRuntime.Agent;
        if (agent != null) _server.Text = agent.Setting.Server;

        Append("=== StarAgent 启动 ===");
        Append($"服务端：{agent?.Setting.Server}");

        UpdateStatus();
    }

    /// <summary>布局子视图</summary>
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();

        var bounds = View!.Bounds;
        var left = 16.0;
        var width = bounds.Width - left * 2;
        var top = View.SafeAreaInsets.Top + 8;

        _title!.Frame = new CGRect(left, top, width, 28);
        top += 32;

        _status!.Frame = new CGRect(left, top, width, 36);
        top += 42;

        _server!.Frame = new CGRect(left, top, width, 36);
        top += 44;

        var buttonWidth = (width - 3 * 6) / 4;
        _start!.Frame = new CGRect(left, top, buttonWidth, 34);
        _ping!.Frame = new CGRect(left + buttonWidth + 6, top, buttonWidth, 34);
        _copy!.Frame = new CGRect(left + (buttonWidth + 6) * 2, top, buttonWidth, 34);
        _clear!.Frame = new CGRect(left + (buttonWidth + 6) * 3, top, buttonWidth, 34);
        top += 42;

        _console!.Frame = new CGRect(0, top, bounds.Width, bounds.Height - top);
    }

    /// <summary>界面显示时刷新状态</summary>
    /// <param name="animated">是否动画</param>
    public override void ViewDidAppear(Boolean animated)
    {
        base.ViewDidAppear(animated);

        UpdateStatus();
    }

    private UIButton MakeButton(String title, Action action)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.SetTitle(title, UIControlState.Normal);
        button.TouchUpInside += (_, _) => action();

        return button;
    }

    private void StartAgent()
    {
        AgentRuntime.Start();

        var agent = AgentRuntime.Agent;
        if (agent == null)
        {
            Append("初始化失败");
            return;
        }

        var server = _server?.Text?.Trim();
        if (!server.IsNullOrEmpty()) agent.Setting.Server = server!;
        agent.Setting.Save();

        Append("=== Agent start ===");
        Append($"服务端：{agent.Setting.Server}");

        _ = AgentRuntime.PingOnceAsync();
        UpdateStatus();
    }

    private async Task PingOnce()
    {
        if (_busy) return;
        _busy = true;

        try
        {
            var ok = await AgentRuntime.PingOnceAsync();
            Append(ok ? "心跳成功" : "心跳失败：" + AgentRuntime.Agent?.LastError);
        }
        finally
        {
            _busy = false;
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        if (_status == null) return;

        var agent = AgentRuntime.Agent;
        if (agent == null)
        {
            _status.Text = "状态：未初始化";
            return;
        }

        var state = agent.Client.Logined ? "在线" : "离线";
        var last = agent.LastPingTime > DateTime.MinValue ? agent.LastPingTime.ToString("HH:mm:ss") : "-";
        _status.Text = $"状态：{state}  最后心跳：{last}\n设备：{agent.Platform.DeviceName}";
    }

    private void Append(String message)
    {
        InvokeOnMainThread(() =>
        {
            if (_console == null) return;

            _console.Text += message + "\n";

            var length = _console.Text?.Length ?? 0;
            if (length > 0)
                _console.ScrollRangeToVisible(new NSRange(length - 1, 1));
        });
    }

    private void CopyConsole()
    {
        UIPasteboard.General.String = _console?.Text ?? "";
        Append("已复制到剪贴板");
    }
}