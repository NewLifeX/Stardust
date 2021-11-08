using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Security;
using Stardust.Models;
#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust.Web
{
    /// <summary>升级更新</summary>
    /// <remarks>
    /// 优先比较版本Version，再比较时间Time。
    /// 自动更新的难点在于覆盖正在使用的exe/dll文件，通过改名可以解决。
    /// </remarks>
    public class Upgrade
    {
        #region 属性
        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>更新目录</summary>
        public String UpdatePath { get; set; } = "Update";

        /// <summary>目标目录</summary>
        public String DestinationPath { get; set; } = ".";

        /// <summary>源文件下载地址</summary>
        public String Url { get; set; }

        /// <summary>更新源文件</summary>
        public String SourceFile { get; set; }
        #endregion

        #region 构造
        /// <summary>实例化一个升级对象实例，获取当前应用信息</summary>
        public Upgrade()
        {
            var asm = Assembly.GetEntryAssembly();
            Name = asm.GetName().Name;
        }
        #endregion

        #region 方法
        /// <summary>开始更新</summary>
        public virtual Boolean Download()
        {
            var url = Url;
            if (url.IsNullOrEmpty()) return false;

            var fileName = Path.GetFileName(url);

            // 即使更新包存在，也要下载
            var file = UpdatePath.CombinePath(fileName).GetBasePath();
            if (File.Exists(file)) File.Delete(file); ;

            WriteLog("准备下载 {0} 到 {1}", url, file);

            var sw = Stopwatch.StartNew();

            var web = CreateClient();
            TaskEx.Run(() => web.DownloadFileAsync(url, file)).Wait();

            sw.Stop();
            WriteLog("下载完成！大小{0:n0}字节，耗时{1:n0}ms", file.AsFile().Length, sw.ElapsedMilliseconds);

            SourceFile = file;

            return true;
        }

        /// <summary>检查并执行更新操作</summary>
        public virtual Boolean Update()
        {
            var dest = DestinationPath;

            // 删除备份文件
            DeleteBackup(dest);

            var file = SourceFile;
            if (!File.Exists(file)) return false;

            WriteLog("发现更新包 {0}", file);

            // 解压更新程序包
            if (!file.EndsWithIgnoreCase(".zip", ".7z")) return false;

            var tmp = Path.GetTempPath().CombinePath(Path.GetFileNameWithoutExtension(file));
            WriteLog("解压缩到临时目录 {0}", tmp);
            file.AsFile().Extract(tmp, true);

            //!!! 此处递归删除，导致也删掉了Update里面的文件
            // 更新覆盖之前，需要把exe/dll可执行文件移走，否则Linux下覆盖运行中文件会报段错误
            foreach (var item in dest.AsDirectory().GetAllFiles("*.exe;*.dll", false))
            {
                var del = item.FullName + ".del";
                WriteLog("MoveTo {0}", del);
                if (File.Exists(del)) File.Delete(del);
                item.MoveTo(del);
            }

            // 拷贝替换更新
            CopyAndReplace(tmp, dest);

            //// 删除备份文件
            //DeleteBackup(DestinationPath);
            //!!! 先别急着删除，在Linux上，删除正在使用的文件可能导致进程崩溃

            WriteLog("更新成功！");

            return true;
        }

        /// <summary>启动当前应用的新进程。当前进程退出</summary>
        public void Run(String name, String args)
        {
            var file = "";
            if (Runtime.Windows || Runtime.Mono)
                file = name + ".exe";
            else if (Runtime.Linux)
                file = name;
            else
                file = name + ".dll";

            file = file.GetFullPath();
            if (Runtime.Linux)
            {
                // 执行Shell命令，要求 UseShellExecute = true
                Process.Start(new ProcessStartInfo("chmod", "+x " + file) { UseShellExecute = true });
                // 授权文件可执行权限以后，需要等一会才能生效
                Thread.Sleep(1000);
            }

            WriteLog("拉起进程 {0} {1}", file, args);
            if (file.EndsWithIgnoreCase(".dll"))
                Process.Start("dotnet", $"{file} {args}");
            else
                Process.Start(file, args);
            Thread.Sleep(1000);
        }

        /// <summary>
        /// 自杀
        /// </summary>
        public virtual void KillSelf()
        {
            var p = Process.GetCurrentProcess();
            WriteLog("退出当前进程 {0}", p.Id);

            if (!Runtime.IsConsole) p.CloseMainWindow();
            Environment.Exit(0);
            p.Kill();
        }

        /// <summary>
        /// 执行命令，文件名与参数由空格隔开
        /// </summary>
        /// <param name="cmd"></param>
        public void Run(String cmd)
        {
            if (cmd.IsNullOrEmpty()) return;

            WriteLog("执行命令：{0}", cmd);

            var si = new ProcessStartInfo
            {
                UseShellExecute = true,
            };
            var p = cmd.IndexOf(' ');
            if (p < 0)
                si.FileName = cmd;
            else
            {
                si.FileName = cmd.Substring(0, p);
                si.Arguments = cmd.Substring(p + 1);
            }

            Process.Start(si);
        }

        /// <summary>
        /// 清理不属于当前平台的执行文件
        /// </summary>
        /// <param name="name"></param>
        public void Trim(String name)
        {
            var name2 = name.TrimEnd(".exe", ".dll");
            if (Runtime.Windows || Runtime.Mono)
            {
                var file = name2.GetFullPath();
                if (File.Exists(file)) File.Delete(file);
            }
            else if (Runtime.Linux)
            {
                var file = (name2 + ".exe").GetFullPath();
                if (File.Exists(file)) File.Delete(file);
            }
        }

        /// <summary>执行更新</summary>
        /// <param name="ur"></param>
        /// <returns></returns>
        public Boolean ProcessUpgrade(UpgradeInfo ur)
        {
            XTrace.WriteLine("执行更新：{0} {1}", ur.Version, ur.Source);

            var dest = ".";
            var url = ur.Source;

            try
            {
                // 需要下载更新包
                if (!url.IsNullOrEmpty())
                {
                    var fileName = Path.GetFileName(url);
                    if (!fileName.EndsWithIgnoreCase(".zip")) fileName = Rand.NextString(8) + ".zip";
                    fileName = "Update".CombinePath(fileName).EnsureDirectory(true);

                    // 清理
                    DeleteBackup(dest);

                    // 下载
                    var sw = Stopwatch.StartNew();
                    var client = new HttpClient();
                    client.DownloadFileAsync(url, fileName).Wait();

                    sw.Stop();
                    XTrace.WriteLine("下载 {0} 到 {1} 完成，耗时 {2} 。", url, fileName, sw.Elapsed);

                    // 解压
                    var source = fileName.TrimEnd(".zip");
                    if (Directory.Exists(source)) Directory.Delete(source, true);
                    source.EnsureDirectory(false);
                    fileName.AsFile().Extract(source, true);
                    //var dis = source.AsDirectory().GetAllFiles(null, true).ToArray();
                    //WriteLog("dis={0} {1}", dis.Length, source);

                    //!!! 此处递归删除，导致也删掉了Update里面的文件
                    // 更新覆盖之前，需要把exe/dll可执行文件移走，否则Linux下覆盖运行中文件会报段错误
                    var time = DateTime.Now.ToString("yyMMddHHmmss");
                    foreach (var item in dest.AsDirectory().GetAllFiles("*.exe;*.dll", false))
                    {
                        //WriteLog("Delete {0}", item);

                        var del = item.FullName + $".{time}.del";
                        WriteLog("MoveTo {0}", del);
                        if (File.Exists(del)) File.Delete(del);
                        item.MoveTo(del);
                    }

                    // 覆盖
                    CopyAndReplace(source, dest);
                    //if (Directory.Exists(source)) Directory.Delete(source, true);
                }

                // 升级处理命令，可选
                var cmd = ur.Executor?.Trim();
                if (!cmd.IsNullOrEmpty())
                {
                    XTrace.WriteLine("执行更新命令：{0}", cmd);

                    var si = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                    };
                    var p = cmd.IndexOf(' ');
                    if (p < 0)
                        si.FileName = cmd;
                    else
                    {
                        si.FileName = cmd.Substring(0, p);
                        si.Arguments = cmd.Substring(p + 1);
                    }

                    Process.Start(si);
                }

                return true;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine("更新失败！");
                XTrace.WriteException(ex);

                return false;
            }
        }
        #endregion

        #region 辅助
        private HttpClient _Client;
        private HttpClient CreateClient()
        {
            if (_Client != null) return _Client;

            return _Client = new HttpClient();
        }

        /// <summary>删除备份文件</summary>
        /// <param name="dest">目标目录</param>
        public void DeleteBackup(String dest)
        {
            // 删除备份
            var di = dest.AsDirectory();
            var fs = di.GetAllFiles("*.del", true);
            foreach (var item in fs)
            {
                WriteLog("Delete {0}", item);
                try
                {
                    item.Delete();
                }
                catch { }
            }
        }

        /// <summary>拷贝并替换。正在使用锁定的文件不可删除，但可以改名</summary>
        /// <param name="source">源目录</param>
        /// <param name="dest">目标目录</param>
        public void CopyAndReplace(String source, String dest)
        {
            WriteLog("CopyAndReplace {0} => {1}", source, dest);

            var di = source.AsDirectory();

            // 来源目录根，用于截断
            var root = di.FullName.EnsureEnd(Path.DirectorySeparatorChar.ToString());
            foreach (var item in di.GetAllFiles(null, true))
            {
                var name = item.FullName.TrimStart(root);
                var dst = dest.CombinePath(name).GetBasePath();

                // 如果是应用配置文件，不要更新
                if (dst.EndsWithIgnoreCase(".exe.config") ||
                    dst.EqualIgnoreCase("appsettings.json")) continue;

                // 拷贝覆盖
                WriteLog("Copy {0}", name);
                try
                {
                    item.CopyTo(dst.EnsureDirectory(true), true);
                }
                catch
                {
                    // 如果是exe/dll，则先改名，因为可能无法覆盖
                    if (/*dst.EndsWithIgnoreCase(".exe", ".dll") &&*/ File.Exists(dst))
                    {
                        //// 先尝试删除
                        //WriteLog("Delete {0}", item);
                        //try
                        //{
                        //    File.Delete(dst);
                        //}
                        //catch
                        //{
                        // 直接Move文件，不要删除，否则Linux上可能导致当前进程退出
                        WriteLog("Move {0}", item);
                        var del = dst + ".del";
                        if (File.Exists(del)) File.Delete(del);
                        File.Move(dst, del);
                        //}

                        item.CopyTo(dst, true);
                    }
                }
            }

            // 删除临时目录
            WriteLog("Delete {0}", di.FullName);
            di.Delete(true);
        }
        #endregion

        #region 日志
        /// <summary>日志对象</summary>
        public ILog Log { get; set; } = Logger.Null;

        /// <summary>输出日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info($"[{Name}]{format}", args);
        #endregion
    }
}