using NewLife;
using NewLife.Http;
using NewLife.Net;

namespace StarGateway.Proxy
{
    /// <summary>Http反向代理。把所有收到的Http请求转发到目标服务器。</summary>
    /// <remarks>
    /// 主要是修改Http请求头为正确的主机。
    /// 
    /// 经典用途：
    /// 1，缓存。代理缓存某些静态资源的请求结果，减少对服务器的请求压力
    /// 2，拦截。禁止访问某些资源，返回空白页或者连接重置
    /// 3，修改请求或响应。更多的可能是修改响应的页面内容
    /// 4，记录统计。记录并统计请求的网址。
    /// 
    /// 修改Http响应的一般做法：
    /// 1，反向映射888端口到目标abc.com
    /// 2，abc.com页面响应时，所有http://abc.com/的连接都修改为http://IP:888
    /// 3，注意在内网的反向代理需要使用公网IP，而不是本机IP
    /// 4，子域名也可以修改，比如http://pic.abc.com/修改为http://IP:888/http_pic.abc.com/
    /// </remarks>
    public class HttpReverseProxy : ProxyServer
    {
        #region 属性
        /// <summary>远程服务器地址</summary>
        public NetUri RemoteServer { get; set; } = new NetUri();
        #endregion

        /// <summary>实例化</summary>
        public HttpReverseProxy()
        {
            Name = "HttpRev";

            Port = 80;

            ProtocolType = NetType.Tcp;
        }

        //protected override void OnStart()
        //{
        //    Add(new HttpCodec { AllowParseHeader = true });

        //    base.OnStart();
        //}

        /// <summary>创建会话</summary>
        /// <param name="session"></param>
        /// <returns></returns>
        protected override INetSession CreateSession(ISocketSession session) => new HttpReverseSession { Host = this };
    }

    /// <summary>Http反向代理会话</summary>
    public class HttpReverseSession : ProxySession
    {
        /// <summary>原始主机</summary>
        public String RawHost { get; set; }

        /// <summary>请求地址</summary>
        public Uri LocalUri { get; set; }

        /// <summary>远程地址</summary>
        public Uri RemoteUri { get; set; }

        /// <summary>收到客户端发来的数据。子类可通过重载该方法来修改数据</summary>
        /// <param name="e"></param>
        protected override void OnReceive(ReceivedEventArgs e)
        {
            if (Disposed) return;

            // 请求头
            var request = new HttpRequest();
            if (request.Parse(e.Packet))
            {
                e.Message = request;

                //// 解码请求头，准备修改细节
                //request.DecodeHeaders();

                //if (OnRequest(request, e))
                //{
                //    // 重新生成Http请求头
                //    request.EncodeHeaders();
                //    e.Packet = request.ToPacket();
                //}

                //var uri = new NetUri(NetType.Http, RawHost, Session.Local.Port);
                WriteDebugLog(LocalUri + "");
            }

            base.OnReceive(e);
        }

        protected virtual Boolean OnRequest(HttpRequest request, ReceivedEventArgs e)
        {
            // 修改Host
            var host = request.Headers["Host"];

            LocalUri = new Uri($"http://{host}:{Session.Local.Port}{request.RequestUri}");

            host = GetHost(host);
            if (host.IsNullOrEmpty()) return false;

            RemoteUri = new Uri($"http://{host}:{RemoteServerUri.Port}{request.RequestUri}");

            request.Headers["Host"] = host;

            request.Headers["X-Real-IP"] = Remote.Host;
            request.Headers["X-Forwarded-For"] = Remote.Host;
            request.Headers["X-Request-Uri"] = LocalUri.ToString();

            return true;
        }

        protected virtual String GetHost(String rawHost)
        {
            if (Host is HttpReverseProxy http)
            {
                RemoteServerUri = http.RemoteServer;
                return http.RemoteServer.Host;
            }

            return null;
        }
    }
}