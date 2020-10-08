using System;
using NewLife.Log;

namespace StarGateway
{
    class Program
    {
        public static void Main(String[] args)
        {
            XTrace.UseConsole();

            var host = new Host();
            host.Add<InitService>();
            host.Add<MyService>();

            host.Run();
        }
    }
}