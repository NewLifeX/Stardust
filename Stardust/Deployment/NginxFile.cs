using System.Diagnostics.CodeAnalysis;
using System.Text;
using NewLife;
using NewLife.Collections;

namespace Stardust.Deployment;

/// <summary>
/// 用于解析和生成 Nginx 配置文件的类
/// </summary>
/// <remarks>
/// server {
/// 	listen 80;
/// 	listen 443 ssl;
/// 	listen [::]:80;
/// 	listen [::]:443 ssl;
/// 
/// 	server_name star.newlifex.com;
/// 
/// 	ssl_certificate /root/certs/newlifex.com.pem;
/// 	ssl_certificate_key /root/certs/newlifex.com.privatekey.pem;
/// 
/// 	location / {
/// 		proxy_pass		http://127.0.0.1:6680/;
/// 		proxy_http_version	1.1;
/// 		proxy_set_header	Upgrade $http_upgrade;
/// 		proxy_set_header	Connection "upgrade";
/// 		proxy_set_header	Host $host;
/// 		proxy_cache_bypass	$http_upgrade;
/// 		proxy_set_header	X-Real-IP $remote_addr;
/// 		proxy_set_header	X-Forwarded-For $proxy_add_x_forwarded_for;
/// 		proxy_set_header	X-Forwarded-Proto $scheme;
/// 		client_max_body_size	1024M;
/// 	}
/// }
/// </remarks>
public class NginxFile
{
    #region 属性
    /// <summary>服务名。一般是域名</summary>
    public String ServerName { get; set; } = "localhost";

    /// <summary>监听的端口列表</summary>
    public List<Int32> Ports { get; set; } = [80, 443];

    /// <summary>是否支持 IPv6</summary>
    public Boolean SupportIPv6 { get; set; } = true;

    /// <summary>SSL 证书路径</summary>
    public String? SslCertificate { get; set; }

    /// <summary>SSL 证书密钥路径</summary>
    public String? SslCertificateKey { get; set; }

    /// <summary>块内的指令</summary>
    public Dictionary<String, IList<String>> Directives { get; set; } = [];

    /// <summary>其它自定义块</summary>
    public List<NginxBlock> Blocks { get; set; } = [];
    #endregion

    #region 解析方法
    /// <summary>从文件加载并解析 Nginx 配置</summary>
    public static NginxFile? Load(String filePath)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        var nginxFile = new NginxFile();
        return nginxFile.Parse(text) ? nginxFile : null;
    }

    /// <summary>从文本解析 Nginx 配置</summary>
    public Boolean Parse(String content)
    {
        var result = new NginxFile();
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var index = 0;
        var root = new NginxBlock();
        if (!root.Parse(lines, ref index)) return false;

        var ds = Directives = root.Directives;
        Blocks = root.Childs;

        if (ds.TryGetValue("server_name", out var vs))
        {
            ds.Remove("server_name");
            ServerName = vs.Join(",");
        }
        if (ds.TryGetValue("listen", out var listens))
        {
            ds.Remove("listen");
            Ports = listens.Select(e => e.TrimEnd("ssl").Trim().ToInt()).Where(e => e > 0).Distinct().ToList();
            SupportIPv6 = listens.Any(e => e.Contains("[::]"));
        }
        if (ds.TryGetValue("ssl_certificate", out vs))
        {
            ds.Remove("ssl_certificate");
            SslCertificate = vs.FirstOrDefault();
        }
        if (ds.TryGetValue("ssl_certificate_key", out vs))
        {
            ds.Remove("ssl_certificate_key");
            SslCertificateKey = vs.FirstOrDefault();
        }

        return true;
    }

    /// <summary>将当前 Nginx 配置转换为字符串表示</summary>
    public NginxBlock Build()
    {
        var root = new NginxBlock
        {
            Name = "server",
            //Directives = Directives,
            Childs = Blocks,
        };

        var ds = new Dictionary<String, IList<String>>(Directives);
        root.Directives = ds;

        if (!ds.ContainsKey("listen") && Ports.Count > 0)
        {
            // 添加监听端口
            var listens = new List<String>();
            ds["listen"] = listens;
            foreach (var port in Ports)
            {
                listens.Add(port % 1000 == 443 ? $"{port} ssl" : $"{port}");
            }
            if (SupportIPv6)
            {
                foreach (var port in Ports)
                {
                    listens.Add(port % 1000 == 443 ? $"[::]:{port} ssl" : $"[::]:{port}");
                }
            }
        }
        if (!ds.ContainsKey("server_name") && !ServerName.IsNullOrEmpty())
        {
            ds["server_name"] = ServerName.Split(',').ToList();
        }
        if (!ds.ContainsKey("ssl_certificate") && !SslCertificate.IsNullOrEmpty())
        {
            ds["ssl_certificate"] = [SslCertificate!];
        }
        if (!ds.ContainsKey("ssl_certificate_key") && !SslCertificateKey.IsNullOrEmpty())
        {
            ds["ssl_certificate_key"] = [SslCertificateKey!];
        }

        return root;
    }

    /// <summary>生成 Nginx 配置文本</summary>
    public override String ToString()
    {
        var root = Build();

        var sb = new StringBuilder();
        root.Build(sb, 0);

        return sb.ToString();
    }
    #endregion

    #region 方法
    /// <summary>获取Location块，不存在时创建</summary>
    /// <returns></returns>
    public NginxBlock? GetLocation(Boolean createOnEmpty)
    {
        var block = Blocks.FirstOrDefault(e => e.Name.StartsWithIgnoreCase("location"));
        if (block != null || !createOnEmpty) return block;

        var location = new NginxBlock
        {
            Name = "location /"
        };
        location.Directives["proxy_pass"] = ["http://127.0.0.1:8080"];
        location.Directives["proxy_http_version"] = ["1.1"];
        location.Directives["proxy_set_header"] = ["Upgrade $http_upgrade", "Connection \"upgrade\"", "Host $host", "X-Real-IP $remote_addr", "X-Forwarded-For $proxy_add_x_forwarded_for", "X-Forwarded-Proto $scheme"];
        location.Directives["proxy_cache_bypass"] = ["$http_upgrade"];

        Blocks.Add(location);

        return location;
    }

    /// <summary>获取后端服务地址列表</summary>
    public String[] GetBackends()
    {
        var location = GetLocation(false);
        if (location == null) return [];

        if (location.Directives.TryGetValue("proxy_pass", out var vs) && vs.Count > 0)
        {
            return vs.Select(e => e.TrimEnd('/')).ToArray();
        }
        return [];
    }

    /// <summary>设置后端服务地址</summary>
    public void SetBackends(params String[] backends)
    {
        if (backends == null || backends.Length == 0) return;

        var location = GetLocation(true);
        location!.Directives["proxy_pass"] = backends.Select(e => e.TrimEnd('/')).ToList();
    }
    #endregion
}

/// <summary>
/// Nginx 配置块（如 http、server、location 等）
/// </summary>
public class NginxBlock
{
    #region 属性
    /// <summary>块名</summary>
    public String Name { get; set; } = null!;

    /// <summary>块内的指令</summary>
    public Dictionary<String, IList<String>> Directives { get; set; } = [];

    /// <summary>子块列表</summary>
    public List<NginxBlock> Childs { get; set; } = [];
    #endregion

    /// <summary>解析 Nginx 块</summary>
    public virtual Boolean Parse(String[] lines, ref Int32 index)
    {
        if (index < 0 || index >= lines.Length) return false;

        // index所在行必须是块的开始行，左大括号结束，左边是块名称
        var firstLine = lines[index++].Trim();
        if (!firstLine.EndsWith("{")) return false;

        // 去掉末尾的左大括号
        Name = firstLine[..^1].Trim();

        // 从index开始逐行解析指令，直到遇到右大括号结束
        for (; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.IsNullOrEmpty() || line[0] == '#') continue;

            // 结束当前块
            if (line == "}") return true;

            // 如果是新的块开始，则递归解析
            if (line.EndsWith("{"))
            {
                var block = new NginxBlock();
                if (!block.Parse(lines, ref index)) return false;

                Childs.Add(block);
            }
            else
            {
                // 按第一个空格切分，获取指令名和参数
                var p = line.IndexOfAny([' ', '\t']);
                if (p > 0)
                {
                    var key = line[..p].Trim();
                    var value = line[(p + 1)..].Trim().TrimEnd(';');
                    if (!Directives.TryGetValue(key, out var vs)) Directives.Add(key, vs = []);
                    vs.Add(value);
                }
            }
        }

        return false;
    }

    /// <summary>构建 Nginx 块的字符串表示</summary>
    public void Build(StringBuilder builder, Int32 level)
    {
        var tab = new String('\t', level);
        var tab2 = new String('\t', level + 1);

        builder.AppendLine($"{tab}{Name} {{");
        foreach (var kv in Directives)
        {
            if (kv.Key.EqualIgnoreCase("server_name", "ssl_certificate")) builder.AppendLine();

            foreach (var value in kv.Value)
            {
                builder.AppendLine($"{tab2}{kv.Key}\t{value};");
            }
        }
        foreach (var child in Childs)
        {
            builder.AppendLine();
            child.Build(builder, level + 1);
        }
        builder.AppendLine($"{tab}}}");
    }

    /// <summary>将当前块转换为字符串表示</summary>
    public override String ToString()
    {
        var sb = Pool.StringBuilder.Get();
        Build(sb, 0);
        return sb.Return(true);
    }
}