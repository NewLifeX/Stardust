using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NewLife.Log;

namespace Stardust.Extensions;

/// <summary>星尘追踪器启动服务。主机启动时解析 <see cref="ITracer"/>，触发自动埋点全局挂载。</summary>
/// <remarks>
/// 解析 <see cref="ITracer"/> 会访问 <c>StarFactory.Tracer</c>，首次访问时创建 <c>StarTracer</c>
/// 并调用 <c>AttachGlobal</c>，从而启用 HttpClient、WebApi、数据库、Redis 等组件的自动埋点。
/// 本服务不执行任何业务逻辑，仅用于保证非 Web 主机也能在启动阶段完成挂载。
/// </remarks>
/// <param name="tracer">追踪器</param>
internal sealed class StardustTracerHostedService(ITracer tracer) : IHostedService
{
    /// <summary>追踪器。持有引用以确保其生命周期与主机一致。</summary>
    public ITracer Tracer { get; } = tracer;

    /// <summary>主机启动。追踪器已在构造阶段完成解析。</summary>
    /// <param name="cancellationToken">取消标记</param>
    /// <returns>已完成任务</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>主机停止。</summary>
    /// <param name="cancellationToken">取消标记</param>
    /// <returns>已完成任务</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
