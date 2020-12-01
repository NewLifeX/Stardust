using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;

namespace Stardust
{
    /// <summary>本地星尘客户端。连接本机星尘代理StarAgent</summary>
    public class LocalStarClient
    {
        #region 属性
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
        public IDictionary<String, Object> GetInfo()
        {
            Init();

            return _client.Invoke<Object>("Api/Info") as IDictionary<String, Object>;
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

            // 发起命令
            var rs = _client.Invoke<String>("KillAndStart", new
            {
                processId = p.Id,
                delay = 3,
                fileName = fileName,
                arguments = Environment.CommandLine,
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
            var ver = "";
            var pid = 0;
            try
            {
                var info = GetInfo();

                // 比目标版本高，不需要安装
                ver = info["Version"] + "";
                if (String.Compare(ver, version) >= 0) return true;

                if (info["Process"] is IDictionary<String, Object> dic) pid = dic["ProcessId"].ToInt();

                if (target.IsNullOrEmpty() && pid > 0)
                {
                    var p = Process.GetProcessById(pid);
                    if (p != null) target = Path.GetDirectoryName(p.MainModule.FileName);
                }

                XTrace.WriteLine("StarAgent在用版本 v{0}，低于目标版本 v{1}", ver, version);
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("没有探测到StarAgent，{0}", ex.GetTrue().Message);
            }

            // 准备安装，甭管是否能够成功重启，先覆盖了文件再说
            {
                if (target.IsNullOrEmpty())
                {
                    target = Runtime.Windows ? "C:\\StarAgent" : $"\\home\\{Environment.UserName}";
                }
                target = target.GetFullPath();
                target.EnsureDirectory(false);

                var ug = new Upgrade
                {
                    SourceFile = Path.GetFileName(url),
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
                var p = Process.GetProcesses().FirstOrDefault(e => e.ProcessName == "StarAgent");
                if (p == null && pid > 0) p = Process.GetProcessById(pid);

                // 重启目标
                if (p != null) p.Kill();

                var fileName = target.CombinePath("StarAgent.exe");
                if (File.Exists(fileName))
                {
                    if (Runtime.Linux) Process.Start("chmod", $"+x {fileName}");

                    var si = new ProcessStartInfo(fileName, "-run")
                    {
                        WorkingDirectory = Path.GetDirectoryName(fileName),
                        UseShellExecute = true
                    };
                    Process.Start(si);
                }
            }

            return true;
        }
        #endregion
    }
}