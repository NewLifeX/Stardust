namespace Stardust.Deployment;

/// <summary>拷贝模式。发布时拷贝文件的行为模式</summary>
/// <remarks>
/// 对于配置文件，常用Skip，因为配置文件可能在部署时被修改，如果覆盖，可能会导致配置丢失。
/// 对于可执行文件，常用Overwrite，因为被发布的可执行文件一般被修改过。
/// </remarks>
public enum CopyModes
{
    /// <summary>无</summary>
    None = 0,

    /// <summary>跳过已存在</summary>
    SkipExists = 1,

    /// <summary>覆盖已存在</summary>
    Overwrite = 2,

    /// <summary>拷贝前清空</summary>
    ClearBeforeCopy = 3,
}
