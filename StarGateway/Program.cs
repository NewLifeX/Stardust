using System;
using NewLife;
using NewLife.Log;
using Stardust;

namespace StarGateway
{
    class Program
    {
        /// <summary>星尘客户端工厂。供 MyService 等组件获取已注册的 AppClient / ITracer</summary>
        public static StarFactory Star { get; private set; }

        public static void Main(String[] args)
        {
            XTrace.UseConsole();

#if DEBUG
            //DefaultTracer.Instance = new DefaultTracer { Log = XTrace.Log };
#endif

            // 初始化星尘客户端（自动读取 appsettings.json / Star.config / 环境变量 / 命令行）
            // 构造函数内部自动调用 Init()，无需显式调用
            Star = new StarFactory();

            // 注册网关到 StarServer（作为应用在线）
            var app = Star.App;
            if (app != null)
            {
                app.AppName = "StarGateway";
                app.ClientId = $"Gateway@{NetHelper.MyIP()}";
                XTrace.WriteLine("StarGateway 已连接 StarServer: {0}", Star.Server);
            }
            else
            {
                XTrace.WriteLine("StarGateway 未配置 StarServer，将以独立模式运行（仅数据库 + 本地配置）");
            }

            var host = new Host();
            host.Add<InitService>();
            host.Add<MyService>();

            host.Run();
        }
    }
}