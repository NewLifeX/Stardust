namespace Stardust.Models;

/// <summary>框架模型</summary>
public class FrameworkModel
{
    /// <summary>要安装的目标版本</summary>
    public String Version { get; set; }

    /// <summary>基准路径。将从该路径下载框架安装文件</summary>
    public String BaseUrl { get; set; }
}