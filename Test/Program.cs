using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Remoting;
using NewLife.Serialization;
using Renci.SshNet;
using Stardust;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Monitors;

namespace Test
{
    class Program
    {
        static ApiServer _Server;
        static void Main(String[] args)
        {
            XTrace.UseConsole();

            Test6();

            Console.WriteLine("OK!");
            Console.ReadKey();
        }

        static void Test1()
        {
            var ip = "10.39.21.36@10512";
            var rule = NodeResolver.Instance.Match(null, ip);
            XTrace.WriteLine(rule.ToJson(true));
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
            client.ProbeAndInstall(null, "1.6");

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

        static void Test5()
        {
            var rs = LocalStarClient.Scan();
            foreach (var item in rs)
            {
                XTrace.WriteLine(item.ToJson());
            }
        }

        static async void Test6()
        {
            XTrace.WriteLine("Test6");

            //var client = new LocalStarClient();
            ////client.GetInfo();

            //try
            //{
            //    //await client.GetInfoAsync();
            //    var rs = await client.Install("test666", "ping", "qq.com");
            //    XTrace.WriteLine("Install={0}", rs.ToJson(true));

            //    Thread.Sleep(3000);

            //    var rs2 = await client.Uninstall("test666");
            //    XTrace.WriteLine("Uninstall={0}", rs2);
            //}
            //catch (Exception ex)
            //{
            //    XTrace.Log.Error("星尘探测失败！{0}", ex.Message);
            //}
        }

        static void Test7()
        {
            var splits = new[] { "中心", "场地", "（", "转运", "10", "分拨", "76" };

            var dic = new SortedDictionary<Int32, NodeRule>();
            using var reader = new ExcelReader("IP划分.xlsx");
            foreach (var row in reader.ReadRows())
            {
                if (row == null || row.Length < 4) continue;
                //XTrace.WriteLine(row.Join());

                var name = (row[0] + "").Replace("\r", null).Replace("\n", null).Trim();
                var ip = (row[3] + "").Replace("\r", null).Replace("\n", null).Trim();
                if (name.IsNullOrEmpty() || ip.IsNullOrEmpty()) continue;

                foreach (var item in splits)
                {
                    var p = name.IndexOf(item);
                    if (p > 0) name = name[..p];
                }

                XTrace.WriteLine("{0}\t{1}", name, ip);

                var ss = ip.Split('.');
                if (ss.Length < 4) continue;

                var key = ss[0].ToInt() * 1000 + ss[1].ToInt();
                var rule = $"{ss[0]}.{ss[1]}.*";
                if (!dic.ContainsKey(key)) dic[key] = new NodeRule { Rule = rule, Category = name, Enable = true, NewNode = true };
            }

            var list = NodeRule.FindAll();
            foreach (var item in dic)
            {
                //XTrace.WriteLine("{0}\t{1}", item.Key, item.Value);

                var nr = item.Value;
                XTrace.WriteLine("{0}\t{1}", nr.Rule, nr.Category);

                if (!list.Any(e => e.Rule == nr.Rule)) nr.Insert();
            }
        }
    }
}