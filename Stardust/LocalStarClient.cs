using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using Stardust.Models;

namespace Stardust
{
    /// <summary>本地星尘客户端。连接本机星尘代理StarAgent</summary>
    public class LocalStarClient
    {
        #region 属性
        /// <summary>代理信息</summary>
        public AgentInfo Info { get; private set; }

        private ApiClient _client;
        #endregion

        #region 构造
        #endregion

        #region 方法
        private void Init()
        {
            if (_client != null) return;

            _client = new ApiClient("udp://127.0.0.1:5500")
            {
                Timeout = 3_000,
                Log = XTrace.Log,
            };

            var set = Setting.Current;
            if (set.Debug) _client.EncoderLog = XTrace.Log;
        }

        /// <summary>获取信息</summary>
        /// <returns></returns>
        public AgentInfo GetInfo()
        {
            Init();

            return Info = _client.Invoke<AgentInfo>("Info");
        }
        #endregion

        #region 进程控制
        /// <summary>自杀并重启</summary>
        /// <returns></returns>
        public Boolean KillAndRestartMySelf()
        {
            Init();

            var p = Process.GetCurrentProcess();
            var fileName = p.MainModule.FileName;
            var args = Environment.CommandLine.TrimStart(Path.ChangeExtension(fileName, ".dll")).Trim();

            // 发起命令
            var rs = _client.Invoke<String>("KillAndStart", new
            {
                processId = p.Id,
                delay = 3,
                fileName,
                arguments = args,
                workingDirectory = Environment.CurrentDirectory,
            });

            // 本进程退出
            //p.Kill();

            return !rs.IsNullOrEmpty();
        }
        #endregion

        #region 安装星尘代理
        /// <summary>探测并安装星尘代理</summary>
        /// <param name="url">zip包下载源</param>
        /// <param name="version">版本号</param>
        /// <param name="target">目标目录</param>
        public Boolean ProbeAndInstall(String url = null, String version = null, String target = null)
        {
            //if (url.IsNullOrEmpty()) throw new ArgumentNullException(nameof(url));
            if (url.IsNullOrEmpty())
            {
                var set = NewLife.Setting.Current;
                if (Environment.Version.Major >= 5)
                    url = set.PluginServer.CombinePath("staragent50.zip");
                else if (Environment.Version.Major >= 4)
                    url = set.PluginServer.CombinePath("staragent45.zip");
                else
                    url = set.PluginServer.CombinePath("staragent31.zip");
            }

            // 尝试连接，获取版本
            try
            {
                var info = GetInfo();

                // 比目标版本高，不需要安装
                if (String.Compare(info.Version, version) >= 0) return true;

                if (!info.FileName.IsNullOrEmpty()) info.FileName = info.FileName.TrimEnd(" (deleted)");
                if (target.IsNullOrEmpty()) target = Path.GetDirectoryName(info.FileName);

                XTrace.WriteLine("StarAgent在用版本 v{0}，低于目标版本 v{1}", info.Version, version);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("没有探测到StarAgent，{0}", ex.GetTrue().Message);
            }

            if (target.IsNullOrEmpty())
            {
                // 在进程中查找
                var p = Process.GetProcesses().FirstOrDefault(e => e.ProcessName == "StarAgent");
                if (p != null)
                {
                    target = Path.GetDirectoryName(p.MainWindowTitle);
                }
            }

            // 准备安装，甭管是否能够成功重启，先覆盖了文件再说
            {
                if (target.IsNullOrEmpty()) target = "..\\staragent";
                target = target.GetFullPath();
                target.EnsureDirectory(false);

                XTrace.WriteLine("目标：{0}", target);

                var ug = new Upgrade
                {
                    SourceFile = Path.GetFileName(url).GetFullPath(),
                    DestinationPath = target,

                    Log = XTrace.Log,
                };

                XTrace.WriteLine("下载：{0}", url);

                var client = new HttpClient();
                client.DownloadFileAsync(url, ug.SourceFile).Wait();

                ug.Update();

                File.Delete(ug.SourceFile);
            }

            {
                // 在进程中查找
                var info = Info;
                var p = info != null && info.ProcessId > 0 ?
                    Process.GetProcessById(info.ProcessId) :
                    Process.GetProcesses().FirstOrDefault(e => e.ProcessName == "StarAgent");

                // 在Linux中设置执行权限
                var fileName = info?.FileName ?? target.CombinePath(Runtime.Linux ? "StarAgent" : "StarAgent.exe");
                if (File.Exists(fileName) && Runtime.Linux) Process.Start("chmod", $"+x {fileName}");

                // 让对方自己退出
                if (info != null)
                {
                    _client.Invoke<String>("KillAndStart", new
                    {
                        processId = p.Id,
                        fileName = info.FileName,
                    });
                    Thread.Sleep(1000);

                    return true;
                }

                // 重启目标
                if (p != null)
                {
                    try
                    {
                        if (!p.HasExited) p.Kill();
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);
                    }
                }

                if (File.Exists(fileName))
                {
                    if (info?.Arguments == "-s")
                    {
                        Process.Start(fileName, "-restart");
                    }
                    else
                    {
                        var si = new ProcessStartInfo(fileName, "-run")
                        {
                            WorkingDirectory = Path.GetDirectoryName(fileName),
                            UseShellExecute = true
                        };
                        Process.Start(si);
                    }
                }
            }

            return true;
        }
        #endregion
    }
}