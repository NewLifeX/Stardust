namespace Stardust.Models;

/// <summary>编译命令参数</summary>
public class CompileCommand
{
    /// <summary>代码库。下载代码的位置</summary>
    public String? Repository { get; set; }

    /// <summary>分支</summary>
    public String? Branch { get; set; }

    /// <summary>项目路径。需要编译的项目路径</summary>
    public String? ProjectPath { get; set; }

    /// <summary>项目类型。默认dotnet</summary>
    public ProjectKinds ProjectKind { get; set; }

    /// <summary>打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开</summary>
    public String? PackageFilters { get; set; }
}
