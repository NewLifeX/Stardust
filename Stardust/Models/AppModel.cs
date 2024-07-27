using NewLife.Remoting.Models;

namespace Stardust.Models;

/// <summary>应用模型</summary>
public class AppModel : LoginRequest
{
    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>节点编码</summary>
    public String? NodeCode { get; set; }
}