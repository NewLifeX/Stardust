using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.Remoting;
using Stardust.Data.Monitors;
using Stardust.Monitors;

namespace Stardust.Server.Services;

/// <summary>监控服务。通过 WebHook 将追踪数据推送到外部告警系统</summary>
public class MonitorService(ITracer tracer)
{
    private readonly ConcurrentDictionary<Int32, WebHookActor> _actors = new();

    /// <summary>推送追踪数据到应用的 WebHook 地址。使用 Actor 模式异步处理</summary>
    /// <param name="app">应用跟踪器</param>
    /// <param name="model">追踪数据模型</param>
    public void WebHook(AppTracer app, TraceModel model)
    {
        if (app == null || model == null) return;
        if (app.WebHook.IsNullOrEmpty()) return;

        // 创建Actor
        var actor = _actors.GetOrAdd(app.ID, k => new WebHookActor { App = app, Tracer = tracer });

        // 发送消息
        actor.App = app;
        actor.Tell(model);
    }

    public class WebHookActor : Actor
    {
        public AppTracer App { get; set; }

        private ApiHttpClient _client;
        private String _server;
        private String _action;
        //private String _token;

        public ApiHttpClient GetClient()
        {
            var addr = App.WebHook;
            if (addr.IsNullOrEmpty()) return null;

            if (_client != null && _server == addr) return _client;
            _server = addr;

            var uri = new Uri(addr);
            _action = uri.PathAndQuery;

            _client = new ApiHttpClient(addr);
            //_token = _client.Services.FirstOrDefault()?.Token;

            return _client;
        }

        protected override async Task ReceiveAsync(ActorContext context, CancellationToken cancellationToken)
        {
            if (context.Message is not TraceModel model) return;

            using var span = Tracer?.NewSpan("MonitorPush");

            var client = GetClient();
            if (client == null) return;

            span?.AppendTag(_action);

            try
            {
                await client.PostAsync<Object>(_action, model);
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
            }
        }
    }
}