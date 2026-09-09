using NewLife.Log;
using NewLife.Remoting;

namespace Stardust.Web.Services;

/// <summary>热门应用服务。通知StarServer缩短热门应用的统计计算周期</summary>
public class HotAppService(IServiceResolver resolver, ITracer tracer)
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
            _client ??= await resolver.GetClientAsync("StarServer");

            // 服务端SetHotApp是[HttpPost]+[FromBody]，只接受POST，因此这里不能用IApiClient的默认重载：
            // 它把Int32这类基础类型参数判为GET，拼到Url尾部（Trace/SetHotApp/1）而不带Body，必然打不中
            if (_client is not ApiHttpClient http)
            {
                XTrace.Log.Debug("设置热门应用失败：客户端[{0}]不是Http客户端，无法以POST调用Trace/SetHotApp", _client?.GetType().Name);
                return;
            }

            // 没有可用服务节点时，InvokeAsync会抛出无消息的InvalidOperationException，这里直接跳过，避免污染埋点错误数
            if (http.Services.Count == 0) return;

            await http.PostAsync<Int32>("Trace/SetHotApp", appId);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            XTrace.Log.Debug("设置热门应用失败：{0}", ex.Message);
        }
    }
}
