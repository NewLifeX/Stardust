using NewLife.Remoting.Models;

namespace Stardust.Models;

/// <summary>应用模型</summary>
public class AppModel : LoginRequest
{
    /// <summary>应用标识</summary>
    public String? AppId { get => Code; set => Code = value; }

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>节点编码</summary>
    public String? NodeCode { get; set; }

    /// <summary>项目名。新应用默认所需要加入的项目</summary>
    public String? Project { get; set; }
}