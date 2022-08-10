using System;
using System.Diagnostics;
using System.IO;
using NewLife;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Threading;
using Stardust;
using Stardust.Managers;
using Stardust.Models;

namespace StarAgent
{
    [Api(null)]
    public class StarService2
    {
        #region 属性
        /// <summary>服务对象</summary>
        public ServiceBase Service { get; set; }

        /// <summary>服务主机</summary>
        public IHost Host { get; set; }

        /// <summary>本地应用服务管理</summary>
        public ServiceManager Manager { get; set; }
        #endregion

        #region 业务
        /// <summary>信息</summary>
        /// <returns></returns>
        [Api(nameof(Info))]
        public AgentInfo Info(AgentInfo info)
        {
            var p = Process.GetCurrentProcess();
            var asmx = AssemblyX.Entry;
            var fileName = p.MainModule.FileName;
            var args = Environment.CommandLine.TrimStart(Path.ChangeExtension(fileName, ".dll")).Trim();

            var set = StarSetting.Current;
            // 使用对方送过来的星尘服务端地址
            if (set.Server.IsNullOrEmpty() && !info.Server.IsNullOrEmpty())
            {
                set.Server = info.Server;
                set.Save();

                XTrace.WriteLine("StarAgent使用应用[{0}]送过来的星尘服务端地址：{1}", info.ProcessName, info.Server);

                if (Service is MyService svc)
                {
                    ThreadPool.QueueUserWorkItem(s =>
                    {
                        svc.StartClient();
                        svc.StartFactory();
                    });
                }
            }

            return new AgentInfo
            {
                Version = asmx?.Version,
                ProcessId = p.Id,
                ProcessName = p.ProcessName,
                FileName = fileName,
                Arguments = args,
                Server = set.Server,
            };
        }
        #endregion
    }
}