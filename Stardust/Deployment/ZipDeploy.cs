using System;
using System.Collections.Generic;
using System.Text;
using NewLife;
using NewLife.Log;

namespace Stardust.Deployment;

/// <summary>Zip压缩包发布</summary>
public class ZipDeploy
{
    #region 属性
    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary></summary>
    public String Shadow { get; set; }
    #endregion

    #region 方法
    public bool Parse(String[] args)
    {
        if (args == null || args.Length == 0) return false;

        // 在参数中找到zip文件
        var file = "";
        var ps = "";
        var name = "";
        var shadow = "";
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].EndsWithIgnoreCase(".zip"))
            {
                file = args[i];
                ps = args.Skip(i + 1).Join(" ");
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
        var hash = fi.MD5().ToHex().ToLower();
        var rundir = fi.Directory;

        return true;
    }

    public void Execute()
    {
        WriteLog("ZipDeploy {0}", name);
        WriteLog("执行目录 {0}", rundir);

        // 影子目录，用于解压缩应用
        if (shadow.IsNullOrEmpty()) shadow = Path.GetTempPath().CombinePath(name);
        shadow = shadow.CombinePath(hash);
        if (!Path.IsPathRooted(shadow)) shadow = rundir.FullName.CombinePath(shadow).GetFullPath();
        WriteLog("影子目录 {0}", shadow);

        if (!Directory.Exists(shadow))
        {
            WriteLog("解压到 {0}", shadow);

            fi.Extract(shadow, true);

            // 复制配置文件和数据文件
            foreach (var item in shadow.AsDirectory().GetFiles())
            {
                if (item.Extension.EndsWithIgnoreCase(".json", ".config", ".xml"))
                {
                    item.CopyTo(rundir.FullName.CombinePath(item.Name));
                }
            }

            var di = shadow.CombinePath("Config").AsDirectory();
            if (di.Exists) di.CopyTo(rundir.FullName.CombinePath("Config"));

            di = shadow.CombinePath("Data").AsDirectory();
            if (di.Exists) di.CopyTo(rundir.FullName.CombinePath("Data"));
        }

        // 执行
        var runfile = rundir.GetFiles().FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
        if (runfile != null)
        {
            WriteLog("执行 {0}", runfile);
        }
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
