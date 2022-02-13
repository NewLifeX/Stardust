using System;
using System.IO.MemoryMappedFiles;
using NewLife;
using NewLife.Serialization;
using NewLife.Threading;
using Stardust.Models;

namespace StarAgent
{
    internal class InfoService
    {
        MemoryMappedFile _mmf;
        TimerX _timer2;
        public void Start()
        {
            var mapName = "MMF_Star";
            _mmf = MemoryMappedFile.CreateNew(mapName, 1024 * 1024);

            _timer2 = new TimerX(DoWork, null, 0, 5_000);
        }

        public void Stop()
        {
            _mmf.TryDispose();
            _timer2.TryDispose();
        }

        private void DoWork(Object state)
        {
            var inf = AgentInfo.GetLocal();
            var buf = Binary.FastWrite(inf).ReadBytes();

            var view = _mmf.CreateViewAccessor();
            view.WriteArray(0, buf, 0, buf.Length);
        }
    }
}