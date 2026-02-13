using NewLife;
using NewLife.Log;
using NewLife.Remoting;

namespace Stardust.Web.Services;

/// <summary>热门应用服务。通知StarServer缩短热门应用的统计计算周期</summary>
public class HotAppService(StarFactory starFactory, ITracer tracer)
{
    private IApiClient _client;

    /// <summary>设置热门应用。调用StarServer的API通知缩短统计计算周期</summary>
    /// <param name="appId">应用编号</param>
    public async Task SetHotAppAsync(Int32 appId)
    {
        if (appId <= 0) return;

        using var span = tracer?.NewSpan("SetHotApp", appId);
        try
        {
            _client ??= await starFactory.CreateForServiceAsync("StarServer");

            await _client.InvokeAsync<Int32>("Trace/SetHotApp", appId);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.Log.Debug("设置热门应用失败：{0}", ex.Message);
        }
    }
}
