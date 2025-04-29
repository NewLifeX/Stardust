using System.IO.Compression;
using NewLife;
using NewLife.Log;

namespace DeployAgent.Commands;

internal class PackCommand : ICommand
{
    public void Process(String[] args)
    {
        // *.zip aa.txt bb/*.cs
        XTrace.WriteLine("开始打包：{0}", args.Join(" "));

        var target = args[0].GetCurrentPath();
        var patterns = args.Length <= 1 ? ["./"] : args.Skip(1).ToArray();

        // 单一打包项，且没有通配符，直接打包，支持多种格式
        if (patterns.Length == 1 && !patterns[0].Contains('*'))
        {
            // 打包目录
            var di = patterns[0].GetCurrentPath().AsDirectory();
            if (di.Exists)
                di.Compress(target, true);
            else
            {
                // 打包文件
                var fi = patterns[0].GetCurrentPath().AsFile();
                if (fi.Exists)
                    fi.Compress(target);
                else
                    throw new FileNotFoundException("文件不存在", patterns[0]);
            }
        }
        else if (target.EndsWithIgnoreCase(".zip"))
        {
            using var fs = new FileStream(target, FileMode.OpenOrCreate, FileAccess.Write);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create, false);
            var root = ".".GetCurrentPath().AsDirectory();

            // 处理多个文件
            foreach (var item in patterns)
            {
                // 分为*匹配、单文件、单目录这几种情况
                if (item.Contains('*'))
                {
                    var parent = "";
                    var di = root;
                    var pt = "";

                    // 把pt分割为目录部分和匹配符部分，以最后一个斜杠或反斜杠分割
                    var p = item.LastIndexOfAny(['/', '\\']);
                    if (p > 0)
                    {
                        parent = item[..p];
                        di = parent.GetCurrentPath().AsDirectory();
                        pt = item[(p + 1)..];
                    }
                    else
                    {
                        pt = item;
                    }

                    // 遍历文件
                    WriteLog("扫描：{0}", di.FullName);
                    foreach (var fi in di.GetFiles("", SearchOption.AllDirectories))
                    {
                        var name = GetEntryName(di, fi);
                        WriteLog("\t添加：{0}", name);
                        zip.CreateEntryFromFile(fi.FullName, name);
                    }
                }
                else
                {
                    var fi = item.GetCurrentPath().AsFile();
                    if (fi.Exists)
                    {
                        var name = GetEntryName(fi.Directory, fi);
                        WriteLog("添加：{0}", name);
                        zip.CreateEntryFromFile(fi.FullName, name);
                    }
                    else
                    {
                        var di = item.GetCurrentPath().AsDirectory();
                        if (di.Exists)
                        {
                            WriteLog("扫描：{0}", di.FullName);
                            foreach (var fi2 in di.GetFiles("", SearchOption.AllDirectories))
                            {
                                var name = GetEntryName(di, fi2);
                                WriteLog("\t添加：{0}", name);
                                zip.CreateEntryFromFile(fi2.FullName, name);
                            }
                        }
                        else
                            throw new FileNotFoundException("文件不存在", item);
                    }
                }
            }
        }
        else
            throw new NotSupportedException("不支持的压缩格式！");
    }

    /// <summary>获取压缩条目相对路径</summary>
    /// <param name="parent"></param>
    /// <param name="fi"></param>
    /// <returns></returns>
    private String GetEntryName(DirectoryInfo parent, FileSystemInfo fi)
    {
        var name = fi.FullName;
        if (!name.StartsWith(parent.FullName)) throw new InvalidDataException();

        // 取得文件相对于目录的路径
        name = name[parent.FullName.Length..].TrimStart('/', '\\');

        // 加上目录
        return parent.Name.CombinePath(name);
    }

    public ILog Log { get; set; } = XTrace.Log;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
}
