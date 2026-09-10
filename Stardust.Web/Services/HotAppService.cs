using NewLife.Log;
using NewLife.Messaging;
using Stardust;

namespace Stardust.Web.Services;

/// <summary>热门应用服务。通知StarServer缩短热门应用的统计计算周期</summary>
public class HotAppService(StarFactory starFactory, ITracer tracer)
{
    private readonly Object _lock = new();
    private IEventBus<Int32>? _bus;

    /// <summary>设置热门应用。通过应用间事件总线广播（星尘平台内部，直接使用AppClient工厂），避免暴露公开接口</summary>
    /// <param name="appId">应用编号</param>
    public async Task SetHotAppAsync(Int32 appId)
    {
        if (appId <= 0) return;

        using var span = tracer?.NewSpan("SetHotApp", appId);
        try
        {
            var bus = _bus;
            if (bus == null)
            {
                lock (_lock) _bus ??= (starFactory.Service as AppClient)?.CreateEventBus<Int32>("Star-HotApp");
                bus = _bus;
            }
            if (bus == null) return;

            await bus.PublishAsync(appId);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.Log.Debug("设置热门应用失败：{0}", ex.Message);
        }
    }
}
