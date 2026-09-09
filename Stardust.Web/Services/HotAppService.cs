using NewLife.Log;
using NewLife.Remoting;
using Stardust;

namespace Stardust.Web.Services;

/// <summary>热门应用服务。通知StarServer缩短热门应用的统计计算周期</summary>
public class HotAppService(StarFactory starFactory, ITracer tracer)
{
    /// <summary>设置热门应用。复用自身到StarServer的已登录客户端（自动携带应用令牌），避免匿名调用被滥用</summary>
    /// <param name="appId">应用编号</param>
    public async Task SetHotAppAsync(Int32 appId)
    {
        if (appId <= 0) return;

        using var span = tracer?.NewSpan("SetHotApp", appId);
        try
        {
            var client = starFactory.Client;
            if (client == null) return;

            await client.InvokeAsync<Int32>("Trace/SetHotApp", appId);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.Log.Debug("设置热门应用失败：{0}", ex.Message);
        }
    }
}
