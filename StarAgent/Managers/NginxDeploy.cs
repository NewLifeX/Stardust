using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;

namespace StarAgent.Managers;

/// <summary>发布站点到nginx</summary>
/// <remarks>
/// 
/// </remarks>
internal class NginxDeploy
{
    #region 属性
    /// <summary>nginx配置文件</summary>
    public String SiteFile { get; set; } = String.Empty;

    /// <summary>nginx配置路径</summary>
    public String ConfigPath { get; set; } = String.Empty;

    /// <summary>配置文件扩展名</summary>
    public String Extension { get; set; } = String.Empty;

    private static String _nginxConfig = String.Empty;
    private static String _nginxExtension = ".conf";
    #endregion

    #region 构造
    static NginxDeploy()
    {
        Init();
    }

    public NginxDeploy()
    {
        // 初始化时，nginx配置目录和扩展名已经确定
        ConfigPath = _nginxConfig;
        Extension = _nginxExtension;
    }

    private static void Init()
    {
        // 初始化nginx配置
        var output = "nginx".Execute("-t", 5_000);
        if (!output.IsNullOrEmpty())
        {
            // 匹配nginx.conf路径
            var match = Regex.Match(output, @"nginx: configuration file (.+nginx\.conf) ");
            if (match.Success)
            {
                var nginxConfPath = match.Groups[1].Value.Trim();

                // 2. 读取nginx.conf，查找include行
                if (!String.IsNullOrEmpty(nginxConfPath) && File.Exists(nginxConfPath))
                {
                    var lines = File.ReadAllLines(nginxConfPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!trimmed.StartsWith("#") && trimmed.StartsWith("include"))
                        {
                            // 解析include路径
                            var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                var includePath = parts[1].TrimEnd(';');
                                // 例如 include /etc/nginx/conf.d/*.conf
                                var dir = Path.GetDirectoryName(includePath);
                                var ext = Path.GetExtension(includePath);
                                if (!String.IsNullOrEmpty(dir) && Directory.Exists(dir))
                                {
                                    _nginxConfig = dir;
                                    _nginxExtension = String.IsNullOrEmpty(ext) ? ".conf" : ext;
                                    break;
                                }
                            }
                        }
                    }

                    if (!_nginxConfig.IsNullOrEmpty()) return;
                }
            }
        }

        // 这里可以加载默认的nginx配置模板

        // 常见nginx配置文件路径
        var commonPaths = new[]
        {
            "/etc/nginx/conf.d",
            "/etc/nginx/sites-enabled",
            "/usr/local/nginx/conf/vhost",
            "/usr/local/nginx/conf.d",
            "C:\\nginx\\conf\\vhost",
            "C:\\nginx\\conf\\sites-enabled",
            "C:\\nginx\\conf\\conf.d",
            "D:\\nginx\\conf\\vhost",
            "D:\\nginx\\conf\\sites-enabled",
            "D:\\nginx\\conf\\conf.d"
        };


        String? bestDir = null;
        String? bestExt = null;
        var maxCount = 0;

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
            {
                // 查找所有配置文件
                var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWithIgnoreCase(".conf", ".nginx"))
                    .ToList();

                if (files.Count > 0)
                {
                    // 统计后缀名出现次数
                    var extGroup = files.GroupBy(f => Path.GetExtension(f).ToLower())
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();

                    if (extGroup != null && extGroup.Count() > maxCount)
                    {
                        bestDir = path;
                        bestExt = extGroup.Key;
                        maxCount = extGroup.Count();
                    }
                }
            }
        }

        if (!String.IsNullOrEmpty(bestDir))
        {
            _nginxConfig = bestDir;
            _nginxExtension = bestExt ?? ".conf";
        }
        else
        {
            // 没有找到站点配置文件，使用默认
            _nginxConfig = commonPaths.FirstOrDefault(Directory.Exists) ?? "";
            _nginxExtension = ".conf";
        }
    }
    #endregion

    #region 方法
    /// <summary>检测nginx配置文件</summary>
    /// <remarks>
    /// 检测nginx配置文件是否存在nginx配置文件，以.nginx或者.conf结尾，且内部内容包含nginx特征
    /// 目录下可能有多个nginx配置文件，返回所有符合条件的配置文件
    /// </remarks>
    public static IEnumerable<NginxDeploy> DetectNginxConfig(String path)
    {
        //todo: 检测nginx配置文件是否存在nginx配置文件，以.nginx或者.conf结尾，且内部内容包含nginx特征
        // 目录下可能有多个nginx配置文件，返回所有符合条件的配置文件
        if (String.IsNullOrEmpty(path) || !Directory.Exists(path)) yield break;

        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWithIgnoreCase(".nginx", ".conf"));

        foreach (var file in files)
        {
            // 简单判断是否为nginx配置文件
            var content = File.ReadAllText(file);
            if (content.Contains("server") && content.Contains("listen") && content.Contains("location"))
            {
                yield return new NginxDeploy { SiteFile = file, };
            }
        }
    }

    /// <summary>发布站点到nginx</summary>
    public Boolean Publish()
    {
        // 判断配置文件是否存在，然后拷贝到nginx配置目录，并重新加载nginx配置
        if (String.IsNullOrEmpty(SiteFile) || String.IsNullOrEmpty(ConfigPath))
            throw new InvalidOperationException("配置文件或nginx路径未设置");

        if (!File.Exists(SiteFile))
            throw new FileNotFoundException("配置文件不存在", SiteFile);

        // 目标nginx配置目录
        var targetDir = ConfigPath;
        if (String.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            throw new DirectoryNotFoundException("nginx配置目录不存在");

        // 拷贝配置文件
        var targetFile = targetDir.CombinePath(Path.GetFileName(SiteFile));
        if (!Extension.IsNullOrEmpty()) targetFile = Path.ChangeExtension(targetFile, Extension);

        // 目标文件不存在，或者两者MD5不一致时，才进行拷贝
        if (File.Exists(targetFile) && File.ReadAllText(SiteFile).MD5() == File.ReadAllText(targetFile).MD5())
            return false;

        WriteLog("正在发布站点配置到nginx：{0} => {1}", SiteFile, targetFile);
        File.Copy(SiteFile, targetFile, true);

        // 重新加载nginx配置
        WriteLog("正在重新加载nginx配置...");
        if (Runtime.Windows)
            targetDir.CombinePath("../nginx.exe").Run("-s reload", 5_000);
        else
            "nginx".Run("-s reload", 5_000);
        WriteLog("nginx配置重新加载完成。");

        return true;
    }

    /// <summary>
    /// 重新加载nginx配置
    /// </summary>
    private void ReloadNginx(String nginxDir)
    {
        // 尝试查找nginx可执行文件
        var exeNames = new[] { "nginx.exe", "nginx" };
        String? exePath = null;
        foreach (var exe in exeNames)
        {
            var path = Path.Combine(nginxDir, exe);
            if (File.Exists(path))
            {
                exePath = path;
                break;
            }
        }

        if (exePath == null)
        {
            // 可能nginx在PATH中，尝试直接调用
            exePath = exeNames.First();
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-s reload",
                WorkingDirectory = nginxDir,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                proc?.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("重载nginx配置失败: " + ex.Message, ex);
        }
    }
    #endregion

    #region 日志
    public ILog Log { get; set; } = XTrace.Log;

    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}
