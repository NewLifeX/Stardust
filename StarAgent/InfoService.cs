using System;
using System.IO.MemoryMappedFiles;
using System.Linq;
using NewLife;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust;
using Stardust.Models;

namespace StarAgent
{
    internal class InfoService
    {
        /// <summary>本地应用服务管理</summary>
        public ServiceManager Manager { get; set; }

        MemoryMappedFile _mmf;
        TimerX _timer2;
        public void Start()
        {
            _mmf = MemoryMappedFile.CreateOrOpen("MMF_Star", 1024);

            _timer2 = new TimerX(DoWork, null, 0, 5_000);
        }

        public void Stop()
        {
            _mmf.TryDispose();
            _timer2.TryDispose();
        }

        private void DoWork(Object state)
        {
            var set = StarSetting.Current;
            var inf = AgentInfo.GetLocal();
            inf.Server = set.Server;
            inf.Services = Manager?.Services.Select(e => e.Name).ToArray();

            //var buf = Binary.FastWrite(inf).ReadBytes();

            //var view = _mmf.CreateViewAccessor();
            //view.Write(0, buf.Length);
            //view.WriteArray(4, buf, 0, buf.Length);

            var ms = _mmf.CreateViewStream(0, 1024, MemoryMappedFileAccess.Write);
            Binary.FastWrite(inf, ms);
        }
    }
}