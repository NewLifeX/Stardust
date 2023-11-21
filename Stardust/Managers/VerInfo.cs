namespace Stardust.Managers;

/// <summary>版本信息</summary>
public class VerInfo
{
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>版本</summary>
    public String Version { get; set; } = null!;

    /// <summary>补丁</summary>
    public String? Sp { get; set; }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => String.IsNullOrEmpty(Sp) ? $"{Name} {Version}" : $"{Name} {Version} Sp{Sp}";
}