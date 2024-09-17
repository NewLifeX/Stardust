using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NewLife;
using NewLife.Data;
using NewLife.Net;

namespace StarGateway.Proxy
{
    /// <summary>代理会话。客户端的一次转发请求（或者Tcp连接），就是一个会话。转发的全部操作都在会话中完成。</summary>
    /// <remarks>
    /// 一个会话应该包含两端，两个Socket，服务端和客户端
    /// 客户端<see cref="INetSession.Session"/>发来的数据，在这里经过处理后，转发给服务端<see cref="RemoteServer"/>；
    /// 服务端<see cref="RemoteServer"/>返回的数据，在这里经过处理后，转发给客户端<see cref="INetSession.Session"/>。
    /// </remarks>
    public class ProxySession : NetSession
    {
        #region 属性
        /// <summary>主机</summary>
        public ProxyServer Host { get; set; }

        /// <summary>远程服务端。跟目标服务端通讯的那个Socket，其实是客户端TcpSession/UdpServer</summary>
        public ISocketClient RemoteServer { get; set; }

        /// <summary>服务端地址</summary>
        public NetUri RemoteServerUri { get; set; } = new NetUri();

        /// <summary>是否中转空数据包。仅对Tcp有效，默认true</summary>
        public Boolean ExchangeEmptyData { get; set; } = true;
        #endregion

        #region 构造
        /// <summary>实例化一个代理会话</summary>
        public ProxySession() { }

        /// <summary>子类重载实现资源释放逻辑时必须首先调用基类方法</summary>
        /// <param name="disposing">从Dispose调用（释放所有资源）还是析构函数调用（释放非托管资源）</param>
        protected override void Dispose(Boolean disposing)
        {
            base.Dispose(disposing);

            var remote = RemoteServer;
            if (remote != null)
            {
                RemoteServer = null;
                remote.TryDispose();
            }
        }
        #endregion

        #region 数据交换
        /// <summary>开始会话处理。</summary>
        public override void Start()
        {
            // 如果未指定远程协议，则与来源协议一致
            if (RemoteServerUri.Type == 0) RemoteServerUri.Type = Session.Local.Type;

            // 如果是Tcp，收到空数据时不要断开。为了稳定可靠，默认设置
            //if (Session is TcpSession tcp) tcp.DisconnectWhenEmptyData = false;

            if (Host.ConnectRemoteOnStart) ConnectRemote(new ReceivedEventArgs());

            base.Start();
        }

        /// <summary>收到客户端发来的数据</summary>
        /// <param name="e"></param>
        protected override void OnReceive(ReceivedEventArgs e)
        {
            if (Disposed) return;

            //WriteLog("客户端[{0}] {1}", e.Length, e.ToHex(16));

            var len = e.Packet.Total;
            if (len > 0 || len == 0 && ExchangeEmptyData)
            {
                //if (len > 0) WriteDebugLog("客户端", e.Packet);

                // 如果未建立到远程服务器链接，则建立
                if (RemoteServer == null) ConnectRemote(e);

                // 如果已存在到远程服务器的链接，则把数据发向远程服务器
                if (RemoteServer != null) SendRemote(e.Packet);
            }
        }

        /// <summary>开始远程连接</summary>
        /// <param name="e"></param>
        protected virtual void ConnectRemote(ReceivedEventArgs e)
        {
            if (RemoteServer != null) return;
            lock (this)
            {
                if (RemoteServer != null) return;

                var sw = Stopwatch.StartNew();
                ISocketClient session = null;
                try
                {
                    //WriteDebugLog("连接远程服务器 {0} 解析 {1}", RemoteServerUri, RemoteServerUri.Address);

                    session = CreateRemote(e);
#if DEBUG
                    session.Log = Log;
                    session.LogSend = Host.LogSend;
                    session.LogReceive = Host.LogReceive;
#endif
                    session.Log = Session.Log;
                    session.OnDisposed += (s, e2) =>
                    {
                        // 这个是必须清空的，是否需要保持会话呢，由OnRemoteDispose决定
                        RemoteServer = null;
                        OnDisposeRemote(s as ISocketClient);
                    };
                    session.Received += Remote_Received;
                    session.Open();

                    //WriteDebugLog("连接远程服务器成功");

                    RemoteServer = session;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    WriteError("无法为{0}连接远程服务器{1}！耗时{2}！{3}", Remote, RemoteServerUri, sw.Elapsed, ex.Message);

                    if (session != null) session.Dispose();
                    Dispose();
                }
            }
        }

        /// <summary>为会话创建与远程服务器通讯的Socket。可以使用Socket池达到重用的目的。</summary>
        /// <param name="e"></param>
        /// <returns></returns>
        protected virtual ISocketClient CreateRemote(ReceivedEventArgs e)
        {
            var client = RemoteServerUri.CreateRemote();
            // 如果是Tcp，收到空数据时不要断开。为了稳定可靠，默认设置
            //if (client is TcpSession tcp) tcp.DisconnectWhenEmptyData = false;

            return client;
        }

        /// <summary>远程连接断开时触发。默认销毁整个会话，子类可根据业务情况决定客户端与代理的链接是否重用。</summary>
        /// <param name="client"></param>
        protected virtual void OnDisposeRemote(ISocketClient client) => Dispose();

        void Remote_Received(Object sender, ReceivedEventArgs e)
        {
            if (Disposed) return;

            try
            {
                OnReceiveRemote(e);
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
                Dispose();
            }
        }

        /// <summary>收到远程服务器返回的数据</summary>
        /// <param name="e"></param>
        protected virtual void OnReceiveRemote(ReceivedEventArgs e)
        {
            var len = e.Packet.Total;
            //if (len > 0) WriteDebugLog("服务端", e.Packet);

            if (len > 0 || len == 0 && ExchangeEmptyData)
            {
                var session = Session;
                if (session == null || session.Disposed)
                    Dispose();
                else
                {
                    try
                    {
                        Send(e.Packet);
                    }
                    catch (Exception ex)
                    {
                        WriteError("转发给客户端出错，{0}", ex.Message);

                        Dispose();
                        throw;
                    }
                }
            }
        }
        #endregion

        #region 发送
        /// <summary>发送数据</summary>
        /// <param name="pk">缓冲区</param>
        public virtual Int32 SendRemote(IPacket pk)
        {
            try
            {
                return RemoteServer.Send(pk);
            }
            catch
            {
                Dispose();
                throw;
            }
        }
        #endregion

        #region 辅助
        private String _LogPrefix;
        /// <summary>日志前缀</summary>
        public override String LogPrefix
        {
            get
            {
                if (_LogPrefix == null)
                {
                    var session = this as INetSession;
                    var name = session.Host == null ? "" : session.Host.Name.TrimEnd("Proxy");
                    _LogPrefix = $"{name}[{ID}] ";
                }
                return _LogPrefix;
            }
            set { _LogPrefix = value; }
        }

        /// <summary>写调试版日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        [Conditional("DEBUG")]
        protected void WriteDebugLog(String format, params Object[] args) => WriteLog(format, args);

        /// <summary>写调试版日志</summary>
        /// <param name="action"></param>
        /// <param name="stream"></param>
        [Conditional("DEBUG")]
        protected virtual void WriteDebugLog(String action, Stream stream) => WriteLog(action + "[{0}] {1}", stream.Length, stream.ReadBytes(16).ToHex());

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override String ToString() => base.ToString() + "=>" + RemoteServerUri;
        #endregion
    }
}