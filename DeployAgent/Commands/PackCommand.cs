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
        var files = args.Length <= 1 ? ["./"] : args.Skip(1).ToArray();

        if (files.Length == 1)
        {
            var di = files[0].GetCurrentPath().AsDirectory();
            if (di.Exists)
                di.Compress(target, true);
            else
            {
                var fi = files[0].GetCurrentPath().AsFile();
                if (fi.Exists)
                    fi.Compress(target);
                else
                    throw new FileNotFoundException("文件不存在", files[0]);
            }
        }
        else
        {
            // 处理多个文件
            foreach (var file in files)
            {
            }
        }
    }
}
