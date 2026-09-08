using NewLife;
using NewLife.Log;
using NewLife.Security;
using NewLife.Threading;
using Stardust.Data;

namespace Stardust.Server.Services;

/// <summary>应用密钥安全服务。定期为存量空密钥应用补密钥并开启重注册窗口（自动收敛），同时兜底关闭长期开启的重注册窗口，防止身份冒用</summary>
/// <remarks>实例化应用密钥安全服务</remarks>
/// <param name="tracer">跟踪器</param>
public class AppSecurityService(ITracer tracer) : IHostedService
{
    private TimerX _timer;

    /// <summary>启动服务，初始化定时器。首跑延迟10秒（避开宿主启动与数据库初始化），之后每小时执行</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(DoWork, null, 10_000, 3600 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>停止服务，销毁定时器</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    /// <summary>执行密钥安全检查。为空密钥应用补密钥并开窗，兜底关闭长期开启的重注册窗口</summary>
    /// <param name="state">定时器状态参数</param>
    private void DoWork(Object state)
    {
        using var span = tracer?.NewSpan("AppSecurity");
        try
        {
            FixEmptySecret();
            CloseReRegisterWindows();
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.WriteException(ex);
        }
    }

    /// <summary>为空密钥的应用生成密钥并开启重注册窗口，等待客户端自动收敛。供定时任务与测试调用</summary>
    public void FixEmptySecret()
    {
        var list = App.FindAllEmptySecret();
        if (list == null || list.Count == 0) return;

        foreach (var app in list)
        {
            app.Secret = Rand.NextString(16);
            app.AllowReRegister = true;
            app.Update();

            XTrace.WriteLine("已为[{0}]生成密钥并开启重注册窗口，等待客户端自动收敛", app);
        }
    }

    /// <summary>兜底关闭重注册窗口。客户端收敛成功后登录钩子会自动关闭，这里防止管理员开启后忘记关闭。仅关闭开启已超过宽限时长的窗口，给慢启动客户端留足收敛时间。供定时任务与测试调用</summary>
    /// <param name="graceMinutes">宽限时长（分钟）。开窗未满该时长的窗口保留不关，默认10分钟，等待客户端（如晚启动的StarWeb）自动收敛</param>
    public void CloseReRegisterWindows(Int32 graceMinutes = 10)
    {
        var list = App.FindAllAllowReRegister();
        if (list == null || list.Count == 0) return;

        var end = DateTime.Now.AddMinutes(-graceMinutes);
        foreach (var app in list)
        {
            // 空密钥应用尚未收敛，交给 FixEmptySecret 处理
            if (app.Secret.IsNullOrEmpty()) continue;

            // 开窗未满宽限时长，保留窗口等待客户端重试收敛（登录成功即自动关窗），避免同轮秒关导致客户端错过
            if (app.UpdateTime >= end) continue;

            app.AllowReRegister = false;
            app.Update();

            XTrace.WriteLine("已关闭[{0}]重注册窗口", app);
        }
    }
}
