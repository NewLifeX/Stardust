using NewLife;
using NewLife.Log;
using NewLife.Security;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>应用密钥安全服务。定期为存量空密钥应用补密钥并开启重注册窗口（自动收敛），同时兜底关闭长期无活动的重注册窗口，防止身份冒用</summary>
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

            // 补密钥后若处于关闭态则开启收敛窗口等待客户端自动收敛；可信接入/开放模式的应用保持原模式（无需窗口）
            if (app.ReRegister == ReRegisterModes.Off) app.ReRegister = ReRegisterModes.Auto;

            app.Update();

            XTrace.WriteLine("已为[{0}]生成密钥，重注册模式={1}", app, app.ReRegister);
        }
    }

    /// <summary>兜底关闭长时间无活动的重注册窗口。客户端携带正确密钥收敛成功后会自动关窗，这里防止窗口开启后被遗忘；仅关闭长期无活动的窗口，避免误伤待收敛的存量客户端。供定时任务与测试调用</summary>
    /// <param name="idleDays">空闲天数。窗口连续无活动（无登录、无编辑）超过该天数才兜底关闭，默认7天；传0可强制关闭</param>
    public void CloseReRegisterWindows(Int32 idleDays = 7)
    {
        var list = App.FindAllReRegisterWindow();
        if (list == null || list.Count == 0) return;

        var end = DateTime.Now.AddDays(-idleDays);
        foreach (var app in list)
        {
            // 空密钥应用尚未收敛，交给 FixEmptySecret 处理
            if (app.Secret.IsNullOrEmpty()) continue;

            // 窗口仍有活动（如客户端持续登录刷新 UpdateTime），保留等待客户端收敛
            if (app.UpdateTime >= end) continue;

            app.ReRegister = ReRegisterModes.Off;
            app.Update();

            XTrace.WriteLine("已关闭[{0}]重注册窗口", app);
        }
    }
}
