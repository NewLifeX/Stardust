using System;
using NewLife.Remoting;

namespace Stardust
{
    /// <summary>RPC服务端。支持星尘</summary>
    public class RpcServer : ApiServer
    {
        /// <summary>星尘客户端</summary>
        public StarClient Star { get; set; }

        /// <summary>启动</summary>
        public override void Start()
        {
            var star = Star;
            if (star == null) throw new ArgumentNullException(nameof(Star));

            // 上报

            base.Start();
        }
    }
}