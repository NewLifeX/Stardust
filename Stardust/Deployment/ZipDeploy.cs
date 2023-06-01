using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using NewLife;
using NewLife.Log;

namespace Stardust.Deployment;

/// <summary>Zip压缩包发布</summary>
public class ZipDeploy
{
    #region 属性
    /// <summary>应用名称</summary>
    public String Name { get; set; }

    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary>启动参数</summary>
    public String Arguments { get; set; }

    /// <summary>工作目录</summary>
    public String WorkingDirectory { get; set; }

    /// <summary>影子目录。应用将在其中执行</summary>
    /// <remarks>默认使用上一级的shadow目录，无权时使用临时目录</remarks>
    public String Shadow { get; set; }

    /// <summary>可执行文件路径</summary>
    public String ExecuteFile { get; set; }

    /// <summary>用户。以该用户执行应用</summary>
    public String UserName { get; set; }

    /// <summary>覆盖文件。需要拷贝覆盖已存在的文件，支持*模糊匹配，多文件分号隔开。如果目标文件不存在，配置文件等自动拷贝</summary>
    public String Overwrite { get; set; }

    /// <summary>进程</summary>
    public Process Process { get; private set; }

    /// <summary>是否调试模式。在调试模式下，重定向控制台输出到日志</summary>
    public Boolean Debug { get; set; }

    /// <summary>最后的错误信息</summary>
    public String LastError { get; set; }

    /// <summary>链路追踪</summary>
    public ITracer Tracer { get; set; }
    #endregion

    #region 方法
    /// <summary>从启动参数中分析参数设置</summary>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public Boolean Parse(String[] args)
    {
        if (args == null || args.Length == 0) return false;

        using var span = Tracer?.NewSpan("ZipDeploy-Parse", args);

        var file = "";
        if (file.IsNullOrEmpty() && FileName.EndsWithIgnoreCase(".zip")) file = FileName;

        // 在参数中找到zip文件
        var name = "";
        var shadow = "";
        var gs = new String[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].EndsWithIgnoreCase(".zip"))
            {
                file = args[i];
            }
            else if (args[i].EqualIgnoreCase("-name") && i + 1 < args.Length)
            {
                name = args[i + 1];
                gs[i] = gs[i + 1] = null;
                i++;
            }
            else if (args[i].EqualIgnoreCase("-shadow") && i + 1 < args.Length)
            {
                shadow = args[i + 1];
                gs[i] = gs[i + 1] = null;
                i++;
            }
            else
            {
                // 其它参数全要，支持 urls=http://*:8000
                gs[i] = args[i];
            }
        }
        if (file.IsNullOrEmpty()) return false;

        Arguments = gs.Where(e => e != null).Join(" ");

        var fi = WorkingDirectory.CombinePath(file).AsFile();
        if (!fi.Exists)
        {
            //throw new FileNotFoundException("找不到zip文件", fi.FullName);
            WriteLog("找不到zip文件 {0}", fi.FullName);
            return false;
        }

        if (name.IsNullOrEmpty()) name = Path.GetFileNameWithoutExtension(file);
        //if (shadow.IsNullOrEmpty()) shadow = Path.GetTempPath().CombinePath(name);
        if (shadow.IsNullOrEmpty())
        {
            // 影子目录默认使用上一级的shadow目录，无权时使用临时目录
            try
            {
                shadow = WorkingDirectory.CombinePath("../shadow").GetFullPath();
                shadow.EnsureDirectory(false);
            }
            catch
            {
                shadow = Path.GetTempPath();
            }
            Shadow = shadow.CombinePath(name);
        }

        Name = name;
        FileName = file;
        Shadow = shadow;

        return true;
    }

    /// <summary>执行拉起应用</summary>
    public Boolean Execute(Int32 msWait = 3_000)
    {
        using var span = Tracer?.NewSpan("ZipDeploy-Execute", new { WorkingDirectory, FileName });

        var fi = WorkingDirectory.CombinePath(FileName)?.AsFile();
        if (fi == null || !fi.Exists)
        {
            //throw new Exception("未指定Zip文件");
            WriteLog("未发现Zip文件 {0}", fi.FullName);
            return false;
        }

        span?.AppendTag(fi.FullName);

        var name = Name;
        if (name.IsNullOrEmpty()) name = Name = Path.GetFileNameWithoutExtension(FileName);

        var shadow = Shadow;
        if (shadow.IsNullOrEmpty())
        {
            // 影子目录默认使用上一级的shadow目录，无权时使用临时目录
            try
            {
                shadow = WorkingDirectory.CombinePath("../shadow").GetFullPath();
                shadow.EnsureDirectory(false);
            }
            catch
            {
                shadow = Path.GetTempPath();
            }
            Shadow = shadow.CombinePath(name);
        }

        if (shadow.IsNullOrEmpty()) return false;

        var hash = fi.MD5().ToHex().Substring(0, 8).ToLower();
        var rundir = fi.Directory;

        WriteLog("ZipDeploy {0}", name);
        WriteLog("运行目录 {0}", rundir);

        // 影子目录，用于解压缩应用
        shadow = shadow.CombinePath($"{Name}-{hash}");
        if (!Path.IsPathRooted(shadow)) shadow = rundir.FullName.CombinePath(shadow).GetFullPath();
        WriteLog("影子目录 {0}", shadow);

        var hasExtracted = false;
        if (!Directory.Exists(shadow))
        {
            // 删除其它版本
            try
            {
                foreach (var di in shadow.AsDirectory().Parent.GetDirectories($"{Name}-"))
                {
                    span?.AppendTag($"删除旧版 {di.FullName}");

                    di.Delete(true);
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
            }

            Extract(shadow);
            hasExtracted = true;
        }

        // 查找可执行文件
        var runfile = FindExeFile(shadow);

        // 执行
        if (runfile == null)
        {
            WriteLog("无法找到名为[{0}]的可执行文件", name);
            return false;
        }

        // 如果带有 NewLife.Core.dll ，重定向基础目录
        //Arguments = $"{Arguments} --BasePath={rundir}".Trim();
        //Environment.SetEnvironmentVariable("BasePath", rundir.FullName);
        ExecuteFile = runfile.FullName;

        WriteLog("运行文件 {0}", runfile);

        var si = new ProcessStartInfo
        {
            FileName = runfile.FullName,
            Arguments = Arguments,
            WorkingDirectory = rundir.FullName,

            // false时目前控制台合并到当前控制台，一起退出；
            // true时目标控制台独立窗口，不会一起退出；
            UseShellExecute = false,
        };
        if (runfile.Extension.EqualIgnoreCase(".dll"))
        {
            si.FileName = "dotnet";
            si.Arguments = $"{runfile.FullName} {Arguments}";
        }
        else if (runfile.Extension.EqualIgnoreCase(".jar"))
        {
            si.FileName = "java";
            si.Arguments = $"{runfile.FullName} {Arguments}";
        }

        // 指定用户时，以特定用户启动进程
        if (!UserName.IsNullOrEmpty())
        {
            si.UserName = UserName;
            //si.UseShellExecute = false;

            // 在Linux系统中，改变目录所属用户
            if (Runtime.Linux)
            {
                var user = UserName;
                if (!user.Contains(':')) user = $"{user}:{user}";
                Process.Start("chown", $"-R {user} {si.WorkingDirectory}");
                Process.Start("chown", $"-R {user} {shadow}");
                Process.Start("chown", $"{user} {si.WorkingDirectory.CombinePath("../").GetBasePath()}");
            }
        }

        if (Debug)
        {
            // UseShellExecute 必须 false，以便于后续重定向输出流
            si.UseShellExecute = false;
            si.RedirectStandardError = true;
        }

        // 在环境变量中设置BasePath，不用担心影响当前进程，因为PathHelper仅读取一次
        Environment.SetEnvironmentVariable("BasePath", si.WorkingDirectory);
        // 在进程的环境变量中设置BasePath
        if (!si.UseShellExecute)
            si.EnvironmentVariables.Add("BasePath", si.WorkingDirectory);

        WriteLog("工作目录: {0}", si.WorkingDirectory);
        WriteLog("启动文件: {0}", si.FileName);
        WriteLog("启动参数: {0}", si.Arguments);
        if (!si.UserName.IsNullOrEmpty())
            WriteLog("启动用户：{0}", si.UserName);

        var p = Process.Start(si);
        Process = p;
        if (msWait > 0 && p.WaitForExit(msWait) && p.ExitCode != 0)
        {
            WriteLog("启动失败！PID={0} ExitCode={1}", p.Id, p.ExitCode);

            if (si.RedirectStandardError)
            {
                //var rs = p.StandardOutput.ReadToEnd();
                //WriteLog(rs);
                var rs = p.StandardError.ReadToEnd();
                LastError = rs;
                WriteLog(rs);
            }

            // 不是我解压缩的，这里需要删除，这样子会有间隔性保留影子目录的机会
            if (!hasExtracted)
            {
                // 启动失败时，删除影子目录，有可能上一次解压以后，该目录被篡改过。这次删除以后，下一次启动时会再次解压缩
                try
                {
                    WriteLog("删除影子目录：{0}", shadow);
                    Directory.Delete(shadow, true);
                }
                catch (Exception ex)
                {
                    Log?.Error(ex.ToString());
                }
            }

            return false;
        }

        WriteLog("Zip启动成功！PID={0}", p.Id);

        return true;
    }

    /// <summary>解压缩</summary>
    /// <param name="shadow"></param>
    public virtual void Extract(String shadow)
    {
        using var span = Tracer?.NewSpan("ZipDeploy-Extract", new { shadow });

        var fi = WorkingDirectory.CombinePath(FileName).AsFile();
        var rundir = fi.DirectoryName;
        WriteLog("解压缩 {0}", FileName);

        fi.Extract(shadow, true);

        var ovs = Overwrite?.Split(';');

        // 复制配置文件和数据文件到运行目录
        var sdi = shadow.AsDirectory();
        if (!sdi.FullName.EnsureEnd("\\").EqualIgnoreCase(rundir.EnsureEnd("\\")))
        {
            // 覆盖文件
            foreach (var item in sdi.GetFiles())
            {
                // 拷贝配置类文件
                if (item.Extension.EndsWithIgnoreCase(".json", ".config", ".xml"))
                {
                    var dst = rundir.CombinePath(item.Name);
                    // 当前文件在覆盖列表内时，强制覆盖
                    if (ovs != null && ovs.Any(e => e.IsMatch(item.Name)))
                    {
                        span?.AppendTag(item.Name);

                        // 注意，appsettings.json 也可能覆盖
                        item.CopyTo(dst, true);
                    }
                    else if (!File.Exists(dst))
                    {
                        item.CopyTo(dst, false);
                    }
                }
            }

            // 覆盖目录
            foreach (var item in sdi.GetDirectories())
            {
                var di = shadow.CombinePath(item.Name).AsDirectory();
                var dest = rundir.CombinePath(item.Name).AsDirectory();
                // 强制覆盖(包含子孙目录，否则会出现目标文件夹中子孙文件夹内容遗漏拷贝)
                if (ovs != null && ovs.Contains(item.Name))
                    di.CopyTo(dest.FullName, allSub: true);
                // 特殊目录且目标不存在时，覆盖
                else if (item.Name.EqualIgnoreCase("Data", "Config", "Plugins", "wwwroot") && !dest.Exists)
                    di.CopyTo(dest.FullName, allSub: true);
            }
        }
    }

    /// <summary>查找执行文件</summary>
    /// <param name="shadow"></param>
    /// <returns></returns>
    public virtual FileInfo FindExeFile(String shadow)
    {
        using var span = Tracer?.NewSpan("ZipDeploy-FindExeFile", new { shadow });

        var fis = shadow.AsDirectory().GetFiles();

        var runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(Name));
        if (runfile == null && Runtime.Windows)
        {
            // 包名的后缀改为exe，即为启动文件
            var name = $"{Name}.exe";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));

            // 第一个参数可能就是exe
            if (runfile == null)
            {
                var ss = Arguments?.Split(" ");
                if (ss != null && ss.Length > 0 && ss[0].EndsWithIgnoreCase(".exe"))
                {
                    runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(ss[0]));
                    if (runfile != null)
                    {
                        // 调整参数
                        Arguments = ss.Skip(1).Join(" ");
                    }
                }
            }

            // 如果当前目录有唯一exe文件，选择它作为启动文件
            if (runfile == null)
            {
                var exes = fis.Where(e => e.Extension.EqualIgnoreCase(".exe")).ToList();
                if (exes.Count == 1) runfile = exes[0];
            }
        }

        // 跟配置文件配套的dll
        if (runfile == null)
        {
            var ext = $".runtimeconfig.json";
            var cfg = fis.FirstOrDefault(e => e.Name.EndsWithIgnoreCase(ext));
            if (cfg != null)
            {
                var name = $"{cfg.Name.Substring(0, cfg.Name.Length - ext.Length)}.dll";
                runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
            }
        }

        // 指定名称dll
        if (runfile == null)
        {
            var name = $"{Name}.dll";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        }

        if (runfile != null) span?.AppendTag(runfile.FullName);

        return runfile;
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}