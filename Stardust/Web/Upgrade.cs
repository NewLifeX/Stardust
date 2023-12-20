using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using NewLife;
using NewLife.Log;

namespace Stardust.Web;

/// <summary>升级更新</summary>
/// <remarks>
/// 自动更新的难点在于覆盖正在使用的exe/dll文件，通过改名可以解决。
/// </remarks>
public class Upgrade
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>更新目录。默认./Update</summary>
    public String? UpdatePath { get; set; } = "Update";

    /// <summary>目标目录</summary>
    public String? DestinationPath { get; set; } = ".";

    /// <summary>源文件下载地址</summary>
    public String? Url { get; set; }

    /// <summary>更新源文件</summary>
    public String? SourceFile { get; set; }

    /// <summary>解压缩的临时目录</summary>
    public String? TempPath { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化一个升级对象实例，获取当前应用信息</summary>
    public Upgrade()
    {
        var asm = Assembly.GetEntryAssembly();
        Name = asm?.GetName().Name ?? nameof(Upgrade);
    }
    #endregion

    #region 方法
    /// <summary>开始下载更新</summary>
    public virtual async Task<Boolean> Download()
    {
        var url = Url;
        if (url.IsNullOrEmpty()) return false;

        var fileName = Path.GetFileName(url);
        if (fileName.IsNullOrEmpty() || fileName.Contains('?')) fileName = "a.zip";

        // 即使更新包存在，也要下载
        var file = UpdatePath.CombinePath(fileName).GetBasePath();
        if (File.Exists(file)) File.Delete(file); ;

        WriteLog("准备下载 {0} 到 {1}", url, file);

        var sw = Stopwatch.StartNew();

        var web = CreateClient();
        //await web.DownloadFileAsync(url, file);
        file = await DownloadFileAsync(web, url, file);

        sw.Stop();
        WriteLog("下载完成！{2} 大小{0:n0}字节，耗时{1:n0}ms", file.AsFile().Length, sw.ElapsedMilliseconds, file);

        var md5 = file.AsFile().MD5().ToHex();
        WriteLog("MD5: {0}", md5);

        SourceFile = file;

        return true;
    }

    /// <summary>
    /// 检查文件散列，避免文件损坏
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public Boolean CheckFileHash(String hash)
    {
        if (hash.IsNullOrEmpty()) return false;

        var fi = SourceFile?.AsFile();
        if (fi == null || !fi.Exists) return false;

        var md5 = fi.MD5().ToHex();
        return md5.EqualIgnoreCase(hash);
    }

    /// <summary>解压缩</summary>
    /// <returns></returns>
    public virtual Boolean Extract()
    {
        var file = SourceFile;
        if (file.IsNullOrEmpty() || !File.Exists(file)) return false;

        WriteLog("发现更新包 {0}", file);

        // 解压更新程序包
        if (!file.EndsWithIgnoreCase(".zip")) return false;

        var tmp = TempPath;
        if (tmp.IsNullOrEmpty()) tmp = TempPath = Path.GetTempPath().CombinePath(Path.GetFileNameWithoutExtension(file));
        WriteLog("解压缩到临时目录 {0}", tmp);
        file.AsFile().Extract(tmp, true);

        return true;
    }

    /// <summary>执行更新，拷贝文件</summary>
    public virtual Boolean Update()
    {
        var dest = DestinationPath;
        if (dest.IsNullOrEmpty()) return false;

        // 删除备份文件
        DeleteBackup(dest);

        var tmp = TempPath;
        if (tmp.IsNullOrEmpty() || !Directory.Exists(tmp)) return false;

        WriteLog("发现更新源目录 {0}", tmp);

        // 记录移动文件，更新失败时恢复
        var dic = new Dictionary<String, String>();
        try
        {
            //!!! 此处递归删除，导致也删掉了Update里面的文件
            // 更新覆盖之前，需要把exe/dll可执行文件移走，否则Linux下覆盖运行中文件会报段错误
            foreach (var item in dest.AsDirectory().GetAllFiles("*.exe;*.dll", false))
            {
                var ori = item.FullName;
                var del = item.FullName + ".del";
                WriteLog("MoveTo {0}", del);
                try
                {
                    if (File.Exists(del)) File.Delete(del);
                    item.MoveTo(del);

                    dic[ori] = del;
                }
                catch (Exception ex)
                {
                    WriteLog(ex.Message);

                    try
                    {
                        // 删除失败时，移动到临时目录随机文件
                        var target = Path.GetTempFileName();
                        item.MoveTo(target);

                        dic[ori] = target;
                    }
                    catch (Exception ex2)
                    {
                        WriteLog(ex2.Message);
                    }
                }
            }

            // 拷贝替换更新
            CopyAndReplace(tmp, dest);

            //// 删除备份文件
            //DeleteBackup(DestinationPath);
            //!!! 先别急着删除，在Linux上，删除正在使用的文件可能导致进程崩溃

            WriteLog("更新成功！");
        }
        catch
        {
            WriteLog("更新失败，恢复文件");
            Restore(dic);

            throw;
        }

        return true;
    }

    void Restore(IDictionary<String, String> dic)
    {
        foreach (var item in dic)
        {
            WriteLog("Restore {0}", item.Value);
            if (File.Exists(item.Value))
            {
                if (File.Exists(item.Key))
                {
                    WriteLog("Delete {0}", item.Key);

                    try
                    {
                        File.Delete(item.Key);
                    }
                    catch (Exception ex)
                    {
                        WriteLog(ex.Message);
                    }
                }

                try
                {
                    File.Move(item.Value, item.Key);
                }
                catch (Exception ex)
                {
                    WriteLog(ex.Message);
                }
            }
        }
    }

    /// <summary>启动当前应用的新进程。当前进程退出</summary>
    public Boolean Run(String name, String args)
    {
        var file = "";
        if (Runtime.Windows || Runtime.Mono)
            file = name + ".exe";
        else if (Runtime.Linux)
            file = name;
        else
            file = name + ".dll";

        file = file.GetFullPath();

        // 如果入口文件不存在，则直接使用dll启动
        if (!File.Exists(file))
            file = (name + ".dll").GetFullPath();
        else if (Runtime.Linux)
        {
            // 执行Shell命令，要求 UseShellExecute = true
            RunShell("chmod", "+x " + file);
            // 授权文件可执行权限以后，需要等一会才能生效
            Thread.Sleep(1000);
        }

        WriteLog("拉起进程 {0} {1}", file, args);
        var p = file.EndsWithIgnoreCase(".dll") ?
            RunShell("dotnet", $"{file} {args}") :
            RunShell(file, args);

        // 如果进程在指定时间退出，说明启动失败
        return p != null && (!p.WaitForExit(1000) || p.ExitCode == 0);
    }

    static Process? RunShell(String fileName, String args) => Process.Start(new ProcessStartInfo(fileName, args) { UseShellExecute = true });

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

        var args = "";
        var p = cmd.IndexOf(' ');
        if (p > 0)
        {
            args = cmd.Substring(p + 1);
            cmd = cmd.Substring(0, p);
        }

        RunShell(cmd, args);
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
    #endregion

    #region 辅助
    private HttpClient? _Client;
    private HttpClient CreateClient()
    {
        if (_Client != null) return _Client;

        return _Client = new HttpClient();
    }

    /// <summary>下载文件</summary>
    /// <param name="client"></param>
    /// <param name="address"></param>
    /// <param name="fileName"></param>
    public static async Task<String> DownloadFileAsync(HttpClient client, String address, String fileName)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, address);
        var rs = await client.SendAsync(request);
        rs.EnsureSuccessStatusCode();

        // 从Http响应头中获取文件名
        var file2 = rs.Content.Headers?.ContentDisposition?.FileName;
        if (!file2.IsNullOrEmpty()) fileName = Path.GetDirectoryName(fileName).CombinePath(file2);
        fileName.EnsureDirectory(true);

        // 删除已存在文件，否则新文件比旧文件小时，写入的文件后面有冗余数据，导致解压缩失败
        if (File.Exists(fileName))
        {
            try
            {
                File.Delete(fileName);
            }
            catch { }
        }

        var ms = rs.Content;
        using var fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        await ms.CopyToAsync(fs);

        // 截断文件，如果前面删除失败，这里就可能使用旧文件，需要把多余部分截断
        fs.SetLength(fs.Position);

        return fileName;
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
            catch (Exception ex)
            {
                WriteLog(ex.Message);
            }
        }
    }

    /// <summary>拷贝并替换。正在使用锁定的文件不可删除，但可以改名</summary>
    /// <param name="source">源目录</param>
    /// <param name="dest">目标目录</param>
    public void CopyAndReplace(String source, String dest)
    {
        WriteLog("CopyAndReplace {0} => {1}", source, dest);

        var src = source.AsDirectory();

        // 来源目录根，用于截断
        var root = src.FullName.EnsureEnd(Path.DirectorySeparatorChar.ToString());
        foreach (var item in src.GetAllFiles(null, true))
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
            catch (Exception ex)
            {
                WriteLog(ex.Message);

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
        WriteLog("Delete {0}", src.FullName);
        src.Delete(true);
    }
    #endregion

    #region 日志
    /// <summary>日志对象</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>输出日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}