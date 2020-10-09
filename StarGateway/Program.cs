using System;
using NewLife.Log;

namespace StarGateway
{
    class Program
    {
        public static void Main(String[] args)
        {
            XTrace.UseConsole();

#if DEBUG
            //DefaultTracer.Instance = new DefaultTracer { Log = XTrace.Log };
#endif

            var host = new Host();
            host.Add<InitService>();
            host.Add<MyService>();

            host.Run();
        }
    }
}