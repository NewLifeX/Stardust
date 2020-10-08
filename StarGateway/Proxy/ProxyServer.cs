using System;
using NewLife.Net;

namespace StarGateway.Proxy
{
    /// <summary>代理服务器</summary>
    /// <remarks>
    /// 负责监听并转发客户端和远程服务器之间的所有数据。
    /// </remarks>
    public abstract class ProxyServer : NetServer
    {
        #region 属性
        /// <summary>开始会话时连接远程会话。默认false，将在首次收到数据包时连接远程会话</summary>
        public Boolean ConnectRemoteOnStart { get; set; }
        #endregion

        #region 构造函数
        /// <summary></summary>
        public ProxyServer() { }
        #endregion

        #region 业务
        ///// <summary>创建会话</summary>
        ///// <param name="session"></param>
        ///// <returns></returns>
        //protected override INetSession CreateSession(ISocketSession session) => new ProxySession { Host = this };
        #endregion
    }
}