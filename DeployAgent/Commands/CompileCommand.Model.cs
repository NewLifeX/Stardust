namespace DeployAgent;

/// <summary>编译命令参数</summary>
public class CompileCommand
{
    /// <summary>代码库。下载代码的位置</summary>
    public String? Repository { get; set; }

    /// <summary>分支</summary>
    public String? Branch { get; set; } = "main";

    /// <summary>源代码目录。本地已有源码的路径，优先使用</summary>
    public String? SourcePath { get; set; }

    /// <summary>项目路径。需要编译的项目路径，相对于代码库根目录</summary>
    public String? ProjectPath { get; set; }

    /// <summary>项目类型。默认dotnet</summary>
    public Int32 ProjectKind { get; set; }

    /// <summary>编译参数。编译项目时所需参数</summary>
    public String? BuildArgs { get; set; }

    /// <summary>编译输出目录。默认publish</summary>
    public String? OutputPath { get; set; } = "publish";

    /// <summary>打包过滤器。需要打包哪些文件，支持通配符，多项分号隔开</summary>
    public String? PackageFilters { get; set; }

    /// <summary>应用部署集名称。用于上传到星尘</summary>
    public String? DeployName { get; set; }

    /// <summary>拉取代码</summary>
    public Boolean PullCode { get; set; }

    /// <summary>编译项目</summary>
    public Boolean BuildProject { get; set; }

    /// <summary>打包输出</summary>
    public Boolean PackageOutput { get; set; }

    /// <summary>上传应用包</summary>
    public Boolean UploadPackage { get; set; }
}
