using System;
using NewLife.Remoting;

namespace Stardust
{
    /// <summary>RPC客户端。支持星尘</summary>
    public class RpcClient : ApiClient
    {
        /// <summary>星尘客户端</summary>
        public StarClient Star { get; set; }

        /// <summary>打开连接</summary>
        /// <returns></returns>
        public override Boolean Open()
        {
            if (Star == null) throw new ArgumentNullException(nameof(Star));

            return base.Open();
        }
    }
}