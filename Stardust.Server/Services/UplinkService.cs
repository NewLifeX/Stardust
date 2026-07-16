using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Data.Monitors;
using Stardust.Monitors;

namespace Stardust.Server.Services;

/// <summary>上行链路服务。将本地追踪数据上报到上级 StarServer，支持链路级联部署</summary>
public class UplinkService
{
    /// <summary>目标服务器地址</summary>
    public String Server { get; set; }

    private ApiHttpClient _client;
    private String _server;
    private UplinkActor _actor;

    /// <summary>获取或创建 HTTP 客户端。首次访问或地址变更时重新创建</summary>
    /// <returns>API 客户端，未配置时返回 null</returns>
    private ApiHttpClient GetClient()
    {
        var addr = Server;
        if (addr.IsNullOrEmpty())
        {
            var set = StarServerSetting.Current;
            addr = set.UplinkServer;
        }

        if (_client != null)
        {
            if (_server == addr) return _client;
        }

        if (addr.IsNullOrEmpty()) return null;

        _client = new ApiHttpClient(addr);

        _server = addr;

        return _client;
    }

    /// <summary>上报追踪数据到上级服务。使用 Actor 模式异步处理，避免阻塞调用方</summary>
    /// <param name="app">应用跟踪器</param>
    /// <param name="model">追踪数据模型</param>
    public void Report(AppTracer app, TraceModel model)
    {
        if (model == null) return;

        var client = GetClient();
        if (client == null) return;

        _actor ??= new UplinkActor { Client = client };
        _actor.Client = client;

        _actor.Tell(model);

        //Task.Run(() =>
        //{
        //    // 数据过大时，以压缩格式上传
        //    var body = model.ToJson();
        //    var rs = body.Length > 1024 ?
        //         client.PostAsync<TraceResponse>("Trace/ReportRaw", body.GetBytes()) :
        //         client.PostAsync<TraceResponse>("Trace/Report", model);
        //}).ConfigureAwait(false);
    }

    /// <summary>上行链路 Actor。异步串行处理上报请求，避免并发连接数过高</summary>
    class UplinkActor : Actor
    {
        /// <summary>API 客户端</summary>
        public ApiHttpClient Client { get; set; }

        /// <summary>处理上报消息。数据过大时自动使用压缩上传</summary>
        /// <param name="context">Actor 上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        protected override async Task ReceiveAsync(ActorContext context, CancellationToken cancellationToken)
        {
            var client = Client;
            if (client == null) return;

            if (context.Message is not TraceModel model) return;

            try
            {
                // 数据过大时，以压缩格式上传
                var body = model.ToJson();
                var rs = body.Length > 1024 ?
                    await client.PostAsync<TraceResponse>("Trace/ReportRaw", body.GetBytes()) :
                    await client.PostAsync<TraceResponse>("Trace/Report", model);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }
}