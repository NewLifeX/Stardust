using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting;
using Renci.SshNet;
using Stardust;
using Stardust.Monitors;

namespace Test
{
    class Program
    {
        static ApiServer _Server;
        static void Main(String[] args)
        {
            XTrace.UseConsole();

            Test4();

            Console.WriteLine("OK!");
            Console.ReadKey();
        }

        static void Test1()
        {
            var sc = new RpcServer()
            {
                Port = 1234,
                Log = XTrace.Log,
                EncoderLog = XTrace.Log,

                NameSpace = "NewLife.Test",
            };

            var star = new StarClient("tcp://127.0.0.1:6666")
            {
                Code = "test",
                Secret = "pass"
            };

            sc.Star = star;

            sc.Start();

            _Server = sc;
        }

        static void Test2()
        {
            Console.Write("请输入密码：");
            var pass = Console.ReadLine().Trim();
            Console.Clear();

            using var client = new SshClient("192.168.13.214", "stone", pass);
            client.Connect();

            XTrace.WriteLine("连接成功");
            {
                var rs = client.RunCommand("uname -a");
                Console.WriteLine(rs.Result);
            }
            {
                var rs = client.RunCommand("cat /proc/cpuinfo");
                Console.WriteLine(rs.Result);
            }
            {
                XTrace.WriteLine("Scp上传文件");
                using var scp = new ScpClient(client.ConnectionInfo);
                scp.Connect();
                XTrace.WriteLine("连接成功");

                scp.Upload("Test.exe".AsFile(), "./Test.exe");

                XTrace.WriteLine("Scp下载文件");
                scp.Download("./aspnetcore-runtime-3.1.5-linux-x64.tar.gz", "./".AsDirectory());
            }
            {
                XTrace.WriteLine("Ftp上传文件");
                using var ftp = new SftpClient(client.ConnectionInfo);
                ftp.Connect();
                XTrace.WriteLine("连接成功");

                ftp.UploadFile("Test.exe".AsFile().OpenRead(), "./Test.exe");

                XTrace.WriteLine("Ftp下载文件");
                ftp.DownloadFile("./aspnetcore-runtime-3.1.5-linux-x64.tar.gz", "asp.gz".AsFile().OpenWrite());
            }
            XTrace.WriteLine("完成");
        }

        static void Test3()
        {
            //foreach (Environment.SpecialFolder item in Enum.GetValues(typeof(Environment.SpecialFolder)))
            //{
            //    var v = Environment.GetFolderPath(item);
            //    Console.WriteLine("{0}:\t{1}", item, v);
            //}

            var client = new LocalStarClient();
            client.ProbeAndInstall(null, "1.1");
        }

        static void Test4()
        {
            //var buf = "hello".GetBytes(); 
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write("hello");
            writer.Write(0);

            var msg = new DefaultMessage();
            msg.Payload = ms.ToArray();
            var buf = msg.ToPacket().ToArray();
            XTrace.WriteLine(buf.ToHex());

            var udp = new UdpClient();
            udp.Send(buf, buf.Length, new IPEndPoint(IPAddress.Broadcast, 5500));

            IPEndPoint ep = null;
            var rs = udp.Receive(ref ep);

            XTrace.WriteLine(ep + "");
            XTrace.WriteLine(rs.ToStr());
        }
    }
}