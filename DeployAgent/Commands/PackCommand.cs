using System.ComponentModel;
using System.IO.Compression;
using NewLife;
using NewLife.Log;

namespace DeployAgent.Commands;

[Description("打包命令。abc.zip *.exe *.dll *.runtimeconfig.json ./Config/*.config")]
internal class PackCommand : ICommand
{
    public void Process(String[] args)
    {
        // *.zip aa.txt bb/*.cs
        XTrace.WriteLine("开始打包：{0}", args.Join(" "));

        // -r 递归压缩
        var recurse = args.Any(e => e == "-r");
        if (recurse) args = args.Where(e => e != "-r").ToArray();

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
            PackZip(target, patterns, recurse);
        }
        else
            throw new NotSupportedException("不支持的压缩格式！");
    }

    private void PackZip(String target, String[] patterns, Boolean recurse)
    {
        using var fs = new FileStream(target, FileMode.OpenOrCreate, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, true);
        var root = ".".GetCurrentPath().AsDirectory();
        var context = new PackContext();

        // 处理多个文件
        foreach (var item in patterns)
        {
            // 分为*匹配、单文件、单目录这几种情况
            if (item.Contains('*'))
            {
                var di = root;
                var pt = "";

                // 把pt分割为目录部分和匹配符部分，以最后一个斜杠或反斜杠分割
                var p = item.LastIndexOfAny(['/', '\\']);
                if (p > 0)
                {
                    di = item[..p].GetCurrentPath().AsDirectory();
                    pt = item[(p + 1)..];
                }
                else
                {
                    pt = item;
                    recurse = false;
                }

                // 遍历文件
                WriteLog("扫描：\e[31;1m{0}\e[0m 匹配：\u001b[32;1m{1}\u001b[0m", di.FullName, pt);
                var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var fi in di.GetFiles(pt, option))
                {
                    var name = recurse ? GetEntryName(di, fi) : fi.Name;
                    WriteLog("\t添加：{0}", name);
                    //zip.CreateEntryFromFile(fi.FullName, name);
                    CreateEntryFromFile(zip, fi, name, context);
                }
            }
            else
            {
                var fi = item.GetCurrentPath().AsFile();
                if (fi.Exists)
                {
                    var name = fi.Name;
                    WriteLog("添加：{0}", name);
                    //zip.CreateEntryFromFile(fi.FullName, name);
                    CreateEntryFromFile(zip, fi, name, context);
                }
                else
                {
                    var di = item.GetCurrentPath().AsDirectory();
                    if (!di.Exists) throw new FileNotFoundException("文件不存在", item);

                    WriteLog("扫描：\e[31;1m{0}\e[0m", di.FullName);
                    var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    foreach (var fi2 in di.GetFiles("", option))
                    {
                        var name = GetEntryName(di, fi2);
                        WriteLog("\t添加：{0}", name);
                        CreateEntryFromFile(zip, fi2, name, context);
                    }
                }
            }
        }

        zip.Dispose();
        fs.Flush();
        fs.SetLength(fs.Position);
        //fs.TryDispose();

        context.CompressSize = fs.Length;
        //context.CompressSize = target.AsFile().Length;
        var rate = context.Size == 0 ? 0 : context.CompressSize / (Double)context.Size;

        WriteLog("压缩完成，大小：\e[31;1m{0:n0}\e[0m，压缩后大小：\e[31;1m{1:n0}\e[0m，压缩比：\e[31;1m{2:p1}\e[0m", context.Size, context.CompressSize, rate);
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

    private void CreateEntryFromFile(ZipArchive zip, FileInfo fi, String name, PackContext context)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.SmallestSize);
        using var fs = fi.OpenRead();
        using var stream = entry.Open();
        fs.CopyTo(stream);

        context.Size += fi.Length;
    }

    public ILog Log { get; set; } = XTrace.Log;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
}

class PackContext
{
    public String Target { get; set; }

    public String[] Patterns { get; set; } = [];

    /// <summary>总大小</summary>
    public Int64 Size { get; set; }

    /// <summary>压缩后大小</summary>
    public Int64 CompressSize { get; set; }
}