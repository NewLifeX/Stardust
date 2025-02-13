using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Data.Monitors;
using Stardust.Monitors;

namespace Stardust.Server.Services;

public class UplinkService
{
    public String Server { get; set; }

    private ApiHttpClient _client;
    private String _server;
    private UplinkActor _actor;

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

    class UplinkActor : Actor
    {
        public ApiHttpClient Client { get; set; }

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