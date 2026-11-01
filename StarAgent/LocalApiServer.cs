using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Serialization;

namespace StarAgent;

/// <summary>本机 UDP API 服务端</summary>
/// <remarks>
/// 本机端口同时提供本地命令行 RPC 与局域网广播搜索（Api/Info），是码神工具等外部工具依赖的既有契约。
/// NewLife.Agent 的 Web 面板启动时会把全局 <see cref="JsonHelper.Default"/> 的命名策略改成 camelCase，
/// 因此这里为服务端指定独立的 JsonHost，固定使用 PascalCase 字段名，避免全局策略漂移导致外部工具读取不到 LocalIP/FileVersion。
/// </remarks>
public static class LocalApiServer
{
    /// <summary>创建 JSON 编码器。使用独立的 JsonHost 并固定 PascalCase</summary>
    /// <returns></returns>
    public static JsonEncoder CreateEncoder()
    {
        var json = new FastJson();
        json.Options.PropertyNaming = PropertyNaming.None;

        return new JsonEncoder { JsonHost = json };
    }

    /// <summary>创建本机 API 服务端</summary>
    /// <param name="uri">监听地址</param>
    /// <param name="tracer">追踪器</param>
    /// <param name="log">日志</param>
    /// <returns></returns>
    public static ApiServer CreateServer(NetUri uri, ITracer? tracer = null, ILog? log = null)
    {
        var svr = new ApiServer(uri)
        {
            Encoder = CreateEncoder(),
            ReuseAddress = true,
        };
        if (tracer != null) svr.Tracer = tracer;
        if (log != null)
        {
            svr.Log = log;
            svr.EncoderLog = log;
        }

        return svr;
    }
}
