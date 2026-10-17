using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using NewLife;

namespace StarAgent.Droid;

/// <summary>星尘代理控制台。展示运行状态与日志，并提供服务端地址配置</summary>
[Activity(Label = "StarAgent", MainLauncher = true, Exported = true,
    Theme = "@android:style/Theme.Material.NoActionBar",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public class MainActivity : Activity
{
    private const String Background = "#0B0F14";
    private const String Foreground = "#D7E2EA";

    private TextView? _console;
    private TextView? _status;
    private EditText? _server;
    private Boolean _autoSmoke;
    private Boolean _busy;

    /// <summary>创建界面</summary>
    /// <param name="savedInstanceState">状态</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _autoSmoke = Intent?.GetBooleanExtra("smoke", false) == true;

        BuildUi();

        AgentRuntime.SetLogSink(Append);
        AgentRuntime.Init(this);

        AgentRuntime.Log("=== StarAgent 启动 ===");
        Append($"平台：{AgentRuntime.Platform?.Name}  设备：{AgentRuntime.Platform?.DeviceName}");
        Append($"服务端：{AgentRuntime.Agent?.Setting.Server}");

        RequestNotificationPermission();

        if (_autoSmoke) RunSmoke();
    }

    /// <summary>回到前台时刷新状态</summary>
    protected override void OnResume()
    {
        base.OnResume();

        UpdateStatus();
    }

    private void BuildUi()
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(Color.ParseColor(Background));

        var bar = new LinearLayout(this) { Orientation = Orientation.Vertical };
        bar.SetPadding(Dp(12), Dp(12), Dp(12), Dp(4));

        _status = new TextView(this) { Text = "状态：未启动", TextSize = 13 };
        _status.SetTextColor(Color.ParseColor("#7FD1AE"));
        bar.AddView(_status);

        var serverRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        serverRow.SetPadding(0, Dp(8), 0, 0);

        _server = new EditText(this) { Hint = "http://star.newlifex.com:6600" };
        _server.SetSingleLine(true);
        serverRow.AddView(_server, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

        var save = new Button(this) { Text = "保存并启动" };
        save.Click += (_, _) => StartAgent();
        serverRow.AddView(save);

        bar.AddView(serverRow);

        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        actions.SetPadding(0, Dp(4), 0, 0);
        AddButton(actions, "立即心跳", () => _ = PingOnce());
        AddButton(actions, "复制", CopyConsole);
        AddButton(actions, "清空", () => { if (_console != null) _console.Text = ""; });
        bar.AddView(actions);

        _console = new TextView(this)
        {
            TextSize = 11,
            Typeface = Typeface.Monospace,
        };
        _console.SetTextColor(Color.ParseColor(Foreground));
        _console.SetBackgroundColor(Color.ParseColor(Background));
        _console.SetPadding(Dp(12), Dp(8), Dp(12), Dp(24));
        _console.SetTextIsSelectable(true);

        var scroll = new ScrollView(this);
        scroll.AddView(_console, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        root.AddView(bar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        SetContentView(root);
    }

    private void AddButton(LinearLayout row, String text, Action action)
    {
        var button = new Button(this) { Text = text };
        button.Click += (_, _) => action();
        row.AddView(button, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
    }

    private void StartAgent()
    {
        var agent = AgentRuntime.Init(this);
        if (agent == null)
        {
            Append("初始化失败");
            return;
        }

        var server = _server?.Text?.ToString()?.Trim();
        if (!server.IsNullOrEmpty()) agent.Setting.Server = server!;
        agent.Setting.Save();

        AgentRuntime.StartService(this);
        AgentRuntime.Log("=== Agent start ===");
        Append($"服务端：{agent.Setting.Server}");

        UpdateStatus();
    }

    private async Task PingOnce()
    {
        if (_busy) return;
        _busy = true;

        try
        {
            var msg = await AgentRuntime.PingOnceAsync();
            Append(msg);
        }
        finally
        {
            _busy = false;
            UpdateStatus();
        }
    }

    private async void RunSmoke()
    {
        StartAgent();

        // 等待登录完成，最多15秒
        for (var i = 0; i < 15; i++)
        {
            if (AgentRuntime.Agent?.Client.Logined == true) break;
            await Task.Delay(1000);
        }

        await PingOnce();

        var ok = AgentRuntime.Agent?.Client.Logined == true;
        AgentRuntime.Log(ok ? "=== Agent ready ===" : "=== Agent fail ===");
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
        _status.Text = $"状态：{state}  最后心跳：{last}  设备：{agent.Platform.DeviceName}";
    }

    private void Append(String message)
    {
        RunOnUiThread(() =>
        {
            if (_console == null) return;

            _console.Text += message + "\n";

            // 滚动到底部
            if (_console.Parent is ScrollView scroll)
                scroll.Post(() => scroll.FullScroll(FocusSearchDirection.Down));
        });
    }

    private void CopyConsole()
    {
        try
        {
            if (GetSystemService(ClipboardService) is ClipboardManager clipboard)
                clipboard.PrimaryClip = ClipData.NewPlainText("StarAgent", _console?.Text ?? "");
            Append("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            Append("复制失败：" + ex.Message);
        }
    }

    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;

        try { RequestPermissions(["android.permission.POST_NOTIFICATIONS"], 1); } catch { }
    }

    private Int32 Dp(Int32 value) => (Int32)(value * Resources?.DisplayMetrics?.Density ?? 1);
}