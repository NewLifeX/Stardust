namespace Stardust.Server.Models;

#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
/// <summary>配置入参</summary>
public class ConfigInModel
{
    ///// <summary>令牌</summary>
    //public String Token { get; set; }

    /// <summary>应用</summary>
    public String AppId { get; set; }

    /// <summary>密钥</summary>
    public String? Secret { get; set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>作用域</summary>
    public String? Scope { get; set; }

    /// <summary>版本</summary>
    public Int32 Version { get; set; }

    /// <summary>已使用的键</summary>
    public String? UsedKeys { get; set; }

    /// <summary>缺失的键</summary>
    public String? MissedKeys { get; set; }
}
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
