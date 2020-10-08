using System;
using NewLife.Net;

namespace StarGateway.Proxy
{
    /// <summary>通用NAT代理。对固定目标服务器进行数据转发</summary>
    /// <remarks>
    /// 监听协议可以跟远程协议不同，也即是可以实现Tcp/Udp互相转发
    /// </remarks>
    public class NATProxy : ProxyServer
    {
        #region 属性
        /// <summary>远程服务器地址</summary>
        public NetUri RemoteServer { get; set; } = new NetUri();
        #endregion

        #region 方法
        /// <summary>开始</summary>
        protected override void OnStart()
        {
            var rs = RemoteServer;
            WriteLog("NAT代理 => {0}", rs);

            if (rs.Type == 0) rs.Type = ProtocolType;

            base.OnStart();
        }

        /// <summary>添加会话。子类可以在添加会话前对会话进行一些处理</summary>
        /// <param name="session"></param>
        protected override void AddSession(INetSession session)
        {
            var rs = RemoteServer;
            var ps = session as ProxySession;
            ps.RemoteServerUri = rs;

            // 如果不是Tcp/Udp，则使用本地协议
            if (!rs.IsTcp && !rs.IsUdp)
                ps.RemoteServerUri.Type = Local.Type;

            base.AddSession(session);
        }
        #endregion
    }
}