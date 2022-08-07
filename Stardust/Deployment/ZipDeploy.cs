using System.Diagnostics;
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

    /// <summary>影子目录。应用将在其中执行</summary>
    public String Shadow { get; set; }
    #endregion

    #region 方法
    /// <summary>从启动参数中分析参数设置</summary>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public Boolean Parse(String[] args)
    {
        if (args == null || args.Length == 0) return false;

        // 在参数中找到zip文件
        var file = "";
        var name = "";
        var shadow = "";
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].EndsWithIgnoreCase(".zip"))
            {
                file = args[i];
                Arguments = args.Skip(i + 1).Join(" ");
            }
            else if (args[i].EqualIgnoreCase("-name") && i + 1 < args.Length)
            {
                name = args[i + 1];
            }
            else if (args[i].EqualIgnoreCase("-shadow") && i + 1 < args.Length)
            {
                shadow = args[i + 1];
            }
        }
        if (file.IsNullOrEmpty()) return false;

        var fi = file.AsFile();
        if (!fi.Exists) throw new FileNotFoundException("找不到zip文件", fi.FullName);

        if (name.IsNullOrEmpty()) name = Path.GetFileNameWithoutExtension(file);
        if (shadow.IsNullOrEmpty()) shadow = Path.GetTempPath().CombinePath(name);

        Name = name;
        FileName = file;
        Shadow = shadow;

        return true;
    }

    /// <summary>执行拉起应用</summary>
    public void Execute()
    {
        var fi = FileName?.AsFile();
        if (fi == null || !fi.Exists) throw new Exception("未指定Zip文件");

        var name = Name;
        if (name.IsNullOrEmpty()) name = Name = Path.GetFileNameWithoutExtension(FileName);

        var hash = fi.MD5().ToHex()[..8].ToLower();
        var rundir = fi.Directory;
        var shadow = Shadow;
        if (shadow.IsNullOrEmpty()) shadow = Shadow = Path.GetTempPath().CombinePath(name);

        WriteLog("ZipDeploy {0}", name);
        WriteLog("运行目录 {0}", rundir);

        // 影子目录，用于解压缩应用
        shadow = shadow.CombinePath(hash);
        if (!Path.IsPathRooted(shadow)) shadow = rundir.FullName.CombinePath(shadow).GetFullPath();
        WriteLog("影子目录 {0}", shadow);

        if (!Directory.Exists(shadow)) Extract(shadow);

        // 查找可执行文件
        var runfile = FindExeFile(shadow);

        // 执行
        if (runfile.IsNullOrEmpty())
        {
            WriteLog("无法找到名为[{0}]的可执行文件", name);
            return;
        }

        // 如果带有 NewLife.Core.dll ，重定向基础目录
        //Arguments = $"{Arguments} --BasePath={rundir}".Trim();
        Environment.SetEnvironmentVariable("BasePath", rundir.FullName);

        WriteLog("执行 {0}", runfile);

        ProcessStartInfo si = null;
        if (rundir.Extension.EqualIgnoreCase(".dll"))
        {
            si = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"{FileName} {Arguments}",
                WorkingDirectory = rundir.FullName,
                UseShellExecute = false,
            };
        }
        else
        {
            si = new ProcessStartInfo
            {
                FileName = runfile,
                Arguments = Arguments,
                WorkingDirectory = rundir.FullName,
                UseShellExecute = false,
            };
        }

        WriteLog("启动文件: {0}", si.FileName);
        WriteLog("启动参数: {0}", si.Arguments);

        var p = Process.Start(si);
        if (p.WaitForExit(3_000))
            WriteLog("启动失败！ExitCode={0}", p.ExitCode);
        else
            WriteLog("启动成功！");
    }

    /// <summary>解压缩</summary>
    /// <param name="shadow"></param>
    protected virtual void Extract(String shadow)
    {
        var fi = FileName.AsFile();
        var rundir = fi.Directory;
        WriteLog("解压缩 {0}", FileName);

        fi.Extract(shadow, true);

        // 复制配置文件和数据文件到运行目录
        foreach (var item in shadow.AsDirectory().GetFiles())
        {
            if (item.Extension.EndsWithIgnoreCase(".json", ".config", ".xml"))
            {
                item.CopyTo(rundir.FullName.CombinePath(item.Name), true);
            }
        }

        var di = shadow.CombinePath("Config").AsDirectory();
        if (di.Exists) di.CopyTo(rundir.FullName.CombinePath("Config"));

        di = shadow.CombinePath("Data").AsDirectory();
        if (di.Exists) di.CopyTo(rundir.FullName.CombinePath("Data"));
    }

    /// <summary>查找执行文件</summary>
    /// <param name="shadow"></param>
    /// <returns></returns>
    protected virtual String FindExeFile(String shadow)
    {
        var fis = shadow.AsDirectory().GetFiles();
        var runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(Name));
        if (runfile == null && Runtime.Windows)
        {
            var name = $"{Name}.exe";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));

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
                var name = $"{cfg.Name[..^ext.Length]}.dll";
                runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
            }
        }

        // 指定名称dll
        if (runfile == null)
        {
            var name = $"{Name}.dll";
            runfile = fis.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        }

        return runfile?.FullName;
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