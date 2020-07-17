using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife;
using NewLife.Remoting;

namespace Stardust
{
    /// <summary>RPC服务端。支持星尘</summary>
    public class RpcServer : ApiServer
    {
        /// <summary>星尘客户端</summary>
        public StarClient Star { get; set; }

        /// <summary>命名空间</summary>
        public String NameSpace { get; set; }

        /// <summary>排除在上报列表之外的服务名</summary>
        public ICollection<String> Excludes { get; } = new HashSet<String>();

        /// <summary>启动</summary>
        public override void Start()
        {
            if (NameSpace.IsNullOrEmpty()) throw new ArgumentNullException(nameof(NameSpace));

            var star = Star;
            if (star == null) throw new ArgumentNullException(nameof(Star));

            //// 上报
            //ReportAsync().Wait();

            base.Start();
        }

        ///// <summary>异步上报</summary>
        ///// <returns></returns>
        //public async Task ReportAsync()
        //{
        //    // 上报
        //    var ss = Manager.Services.Select(e => e.Value.Name).ToList();
        //    ss.RemoveAll(e => Excludes.Contains(e));

        //    await Star.ReportAsync(NameSpace, ss.ToArray());
        //}
    }
}