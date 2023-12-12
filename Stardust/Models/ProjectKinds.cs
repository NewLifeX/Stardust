namespace Stardust.Models;

/// <summary>项目类型</summary>
public enum ProjectKinds
{
    /// <summary>dotnet项目，SDK编译</summary>
    DotNet = 1,

    /// <summary>dotnet项目，MSBuild编译</summary>
    MSBuild = 2,

    /// <summary>自定义项目，执行自定义脚本编译</summary>
    Custom = 99,
}
