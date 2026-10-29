using System;
using System.IO;

namespace ServerTest;

/// <summary>测试源码位置辅助</summary>
/// <remarks>
/// 部分测试直接读取产物源码做断言。构建输出目录可被重定向（例如进程占用 Bin 时改用 -p:OutputPath），
/// 按固定的相对层级回溯会算错根目录，因此改为向上查找解决方案文件定位仓库根。
/// </remarks>
public static class SourceRoot
{
    private const String SolutionFile = "星尘.slnx";

    /// <summary>获取仓库根目录</summary>
    /// <returns>仓库根目录，未找到时返回构建输出目录</returns>
    public static String Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFile))) return dir.FullName;
            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }

    /// <summary>读取仓库内源码文本</summary>
    /// <param name="relativePath">相对仓库根的路径</param>
    /// <returns>源码文本</returns>
    public static String ReadSource(String relativePath) => File.ReadAllText(FindPath(relativePath));

    /// <summary>获取仓库内源码的完整路径</summary>
    /// <param name="relativePath">相对仓库根的路径</param>
    /// <returns>完整路径</returns>
    public static String FindPath(String relativePath) => Path.Combine(Find(), relativePath);
}
