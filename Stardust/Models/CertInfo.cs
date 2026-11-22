namespace Stardust.Models;

/// <summary>证书下发信息。平台告知节点需要落地哪些证书文件</summary>
public class CertInfo
{
    /// <summary>证书编号</summary>
    public Int32 Id { get; set; }

    /// <summary>证书域名。支持通配符，如 *.newlifex.com</summary>
    public String? Domain { get; set; }

    /// <summary>证书名。节点上的文件前缀，如 newlifex.com 对应 newlifex.com.pem</summary>
    public String? Name { get; set; }

    /// <summary>目标目录。空则使用节点默认的 ../certs</summary>
    public String? TargetPath { get; set; }

    /// <summary>版本。证书指纹或更新时间，用于判断是否需要重新下发</summary>
    public String? Version { get; set; }

    /// <summary>证书链下载地址。Linux/Nginx 使用，落地为 {Name}.pem</summary>
    public String? CertFile { get; set; }

    /// <summary>证书链哈希。MD5，用于校验与跳过重复下载</summary>
    public String? CertHash { get; set; }

    /// <summary>私钥下载地址。Linux/Nginx 使用，落地为 {Name}.privatekey.pem</summary>
    public String? KeyFile { get; set; }

    /// <summary>私钥哈希。MD5</summary>
    public String? KeyHash { get; set; }

    /// <summary>PFX 下载地址。Windows/IIS 使用，落地为 {Name}.pfx</summary>
    public String? PfxFile { get; set; }

    /// <summary>PFX 哈希。MD5</summary>
    public String? PfxHash { get; set; }
}
