using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NewLife;
using NewLife.Data;
using NewLife.IO;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;
using Stardust.Windows;

namespace Test;

class Program
{
    static ApiServer _Server;
    static void Main(String[] args)
    {
        XTrace.UseConsole();

        XTrace.Log.Level = LogLevel.All; // 设置日志级别为所有

        Test8();

        Console.WriteLine("OK!");
        Console.ReadKey();
    }

    static async void Test1()
    {
        var ioc = ObjectContainer.Current;
        var provider = ioc.BuildServiceProvider();
        var factory = new StarFactory();
        for (var i = 0; i < 1000; i++)
        {
            var addr = factory.Config["$Registry:StarWeb"];
            //var addr = factory.Config.GetSection("$Registry:StarWeb")?.Value;
            XTrace.WriteLine(addr);

            Thread.Sleep(1000);
        }

        var io = new EasyClient(provider);
        io.SetValue("BaseAction", "/io/");
        //for (var i = 0; i < 1000; i++)
        //{
        var client = io.GetValue("_client") as ApiHttpClient;
        XTrace.WriteLine(client.Services.Join(",", e => e.Address));

        //    Thread.Sleep(1000);
        //}
        await io.Put("aa.txt", (ArrayPacket)"学无先后达者为师！".GetBytes());
        var rs = await io.Get("aa.txt");
        XTrace.WriteLine(rs.Data.ToStr());

        var ss = await io.Search();
        XTrace.WriteLine(ss.ToJson(true));

        var rs2 = await io.Delete("aa.txt");
    }

    static void Test2()
    {
        //Console.Write("请输入密码：");
        //var pass = Console.ReadLine().Trim();
        //Console.Clear();

        //using var client = new SshClient("192.168.13.214", "stone", pass);
        //client.Connect();

        //XTrace.WriteLine("连接成功");
        //{
        //    var rs = client.RunCommand("uname -a");
        //    Console.WriteLine(rs.Result);
        //}
        //{
        //    var rs = client.RunCommand("cat /proc/cpuinfo");
        //    Console.WriteLine(rs.Result);
        //}
        //{
        //    XTrace.WriteLine("Scp上传文件");
        //    using var scp = new ScpClient(client.ConnectionInfo);
        //    scp.Connect();
        //    XTrace.WriteLine("连接成功");

        //    scp.Upload("Test.exe".AsFile(), "./Test.exe");

        //    XTrace.WriteLine("Scp下载文件");
        //    scp.Download("./aspnetcore-runtime-3.1.5-linux-x64.tar.gz", "./".AsDirectory());
        //}
        //{
        //    XTrace.WriteLine("Ftp上传文件");
        //    using var ftp = new SftpClient(client.ConnectionInfo);
        //    ftp.Connect();
        //    XTrace.WriteLine("连接成功");

        //    ftp.UploadFile("Test.exe".AsFile().OpenRead(), "./Test.exe");

        //    XTrace.WriteLine("Ftp下载文件");
        //    ftp.DownloadFile("./aspnetcore-runtime-3.1.5-linux-x64.tar.gz", "asp.gz".AsFile().OpenWrite());
        //}
        //XTrace.WriteLine("完成");
    }

    static void Test3()
    {
        //var buf = "01005F0004496E666F560000007B2250726F636573734964223A323238342C22".ToHex();
        //var udp = new UdpClient();
        //udp.Send(buf, "127.0.0.1", 5500);
        //var rs = udp.ReceiveString();

        //foreach (Environment.SpecialFolder item in Enum.GetValues(typeof(Environment.SpecialFolder)))
        //{
        //    var v = Environment.GetFolderPath(item);
        //    Console.WriteLine("{0}:\t{1}", item, v);
        //}

        var client = new LocalStarClient { Log = XTrace.Log };
        //client.ProbeAndInstall(null, "1.6");
        var info = client.GetInfo();

        var appInfo = new AppInfo(Process.GetCurrentProcess());

        for (var i = 0; i < 5; i++)
        {
            _ = client.PingAsync(appInfo, 5);

            Thread.Sleep(2000);
        }

        Console.WriteLine("等待");
        Console.ReadLine();

        //var p = Process.GetCurrentProcess();
        //var name = p.MainModule.FileName;
        //var str = name + Environment.NewLine + name.ToJson();
        //str += Environment.NewLine + name.ToJson().ToJsonEntity<String>();

        //XTrace.WriteLine(str);
        //File.WriteAllText("aa.txt".GetFullPath(), str);
    }

    static void Test4()
    {
        //var buf = "hello".GetBytes(); 
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write("hello");
        writer.Write(0);

        var msg = new DefaultMessage();
        msg.Payload = (ArrayPacket)ms.ToArray();
        var buf = msg.ToPacket().ToArray();
        XTrace.WriteLine(buf.ToHex());

        var udp = new UdpClient();
        udp.Send(buf, buf.Length, new IPEndPoint(IPAddress.Broadcast, 5500));

        IPEndPoint ep = null;
        var rs = udp.Receive(ref ep);

        XTrace.WriteLine(ep + "");
        XTrace.WriteLine(rs.ToStr());
    }

    static void Test5()
    {
        var rs = LocalStarClient.Scan();
        foreach (var item in rs)
        {
            XTrace.WriteLine(item.ToJson());
        }
    }

    static void Test6()
    {
        //var nr = new NetRuntime();
        //var rs = nr.IsAlpine();
        //XTrace.WriteLine("IsAlpine: {0}", rs);

        var client = new StarClient();
        for (int i = 0; i < 100; i++)
        {
            XTrace.WriteLine("Name:{0}, MachinecName:{1}", client.GetNodeInfo().MachineName, Environment.MachineName);
            Thread.Sleep(1000);
        }
    }

    static void Test7()
    {
        XTrace.WriteLine("所有可用热点：");
        foreach (var item in NativeWifi.GetAvailableNetworkSsids())
        {
            XTrace.WriteLine("{0}:\t{2} ({3} + {4})", item.dot11Ssid, item.dot11BssType, item.wlanSignalQuality, item.dot11DefaultAuthAlgorithm, item.dot11DefaultCipherAlgorithm);
        }

        XTrace.WriteLine("正在连接的热点：");
        foreach (var item in NativeWifi.GetConnectedNetworkSsids())
        {
            XTrace.WriteLine("{0}:\t{2}", item.dot11Ssid, item.dot11BssType, item.wlanSignalQuality);
        }
    }

    static void Test8()
    {
        var arps = AgentInfo.GetArpTable();
        foreach (var item in arps)
        {
            XTrace.WriteLine("{0}:\t{1}", item.Key, item.Value);
        }
    }
}