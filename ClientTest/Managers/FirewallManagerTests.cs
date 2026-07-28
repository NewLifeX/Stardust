using System;
using System.IO;
using Stardust.Managers;
using Xunit;

namespace ClientTest.Managers;

public class FirewallManagerTests
{
    [Fact(DisplayName = "防火墙检测")]
    public void DetectFirewall()
    {
        var firewall = new FirewallManager();
        
        // 至少应该能检测出防火墙类型（即使不可用）
        Assert.True(Enum.IsDefined(typeof(FirewallType), firewall.Type));
        
        // 输出检测结果
        Console.WriteLine($"防火墙类型: {firewall.Type}");
        Console.WriteLine($"是否可用: {firewall.Available}");
    }

    [Fact(DisplayName = "从Nginx配置提取端口")]
    public void ExtractPortsFromNginx()
    {
        // 创建临时目录和测试文件
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 创建测试的Nginx配置文件
            var nginxConfig = @"
server {
    listen 80;
    listen 443 ssl;
    server_name example.com;
    
    location / {
        proxy_pass http://localhost:5000;
    }
}";
            var configFile = Path.Combine(tempDir, "site.conf");
            File.WriteAllText(configFile, nginxConfig);

            // 提取端口
            var ports = FirewallManager.DetectPorts(tempDir);
            var portList = new System.Collections.Generic.List<Int32>(ports);

            // 验证提取的端口
            Assert.Contains(80, portList);
            Assert.Contains(443, portList);
            Console.WriteLine($"提取的端口: {String.Join(", ", portList)}");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "从appsettings.json提取端口")]
    public void ExtractPortsFromAppSettings()
    {
        // 创建临时目录和测试文件
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 创建测试的appsettings.json
            var appSettings = @"{
  ""Kestrel"": {
    ""Endpoints"": {
      ""Http"": {
        ""Url"": ""http://localhost:5000""
      },
      ""Https"": {
        ""Url"": ""https://localhost:5001""
      }
    }
  },
  ""urls"": ""http://localhost:8080;https://localhost:8443""
}";
            var configFile = Path.Combine(tempDir, "appsettings.json");
            File.WriteAllText(configFile, appSettings);

            // 提取端口
            var ports = FirewallManager.DetectPorts(tempDir);
            var portList = new System.Collections.Generic.List<Int32>(ports);

            // 验证提取的端口
            Assert.Contains(5000, portList);
            Assert.Contains(5001, portList);
            Assert.Contains(8080, portList);
            Assert.Contains(8443, portList);
            Console.WriteLine($"提取的端口: {String.Join(", ", portList)}");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "从web.config提取端口")]
    public void ExtractPortsFromWebConfig()
    {
        // 创建临时目录和测试文件
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 创建测试的web.config
            var webConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <system.applicationHost>
    <sites>
      <site name=""Default Web Site"" id=""1"">
        <bindings>
          <binding protocol=""http"" bindingInformation=""*:8080:"" />
          <binding protocol=""https"" bindingInformation=""*:8443:"" />
        </bindings>
      </site>
    </sites>
  </system.applicationHost>
</configuration>";
            var configFile = Path.Combine(tempDir, "web.config");
            File.WriteAllText(configFile, webConfig);

            // 提取端口
            var ports = FirewallManager.DetectPorts(tempDir);
            var portList = new System.Collections.Generic.List<Int32>(ports);

            // 验证提取的端口
            Assert.Contains(8080, portList);
            Assert.Contains(8443, portList);
            Console.WriteLine($"提取的端口: {String.Join(", ", portList)}");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "综合端口检测")]
    public void DetectPortsCombined()
    {
        // 创建临时目录和测试文件
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 创建Nginx配置
            var nginxConfig = @"server { listen 80; }";
            File.WriteAllText(Path.Combine(tempDir, "site.conf"), nginxConfig);

            // 创建appsettings.json
            var appSettings = @"{""urls"": ""http://localhost:5000""}";
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), appSettings);

            // 提取端口
            var ports = FirewallManager.DetectPorts(tempDir);
            var portList = new System.Collections.Generic.List<Int32>(ports);

            // 验证提取的端口（去重后应包含两个）
            Assert.Contains(80, portList);
            Assert.Contains(5000, portList);
            Console.WriteLine($"提取的端口: {String.Join(", ", portList)}");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "从命令行参数提取端口")]
    public void ExtractPortsFromArguments()
    {
        // 测试直接从命令行参数提取端口（传空目录，仅验证参数提取）
        var arguments = "urls=http://*:3380";
        var ports = FirewallManager.DetectPorts(String.Empty, arguments);
        var portList = new System.Collections.Generic.List<Int32>(ports);

        Assert.Contains(3380, portList);
        Console.WriteLine($"从命令行参数提取的端口: {String.Join(", ", portList)}");
    }

    [Fact(DisplayName = "命令行参数覆盖配置文件端口")]
    public void ArgumentsOverrideConfigUrls()
    {
        // 创建临时目录和测试文件（appsettings.json 中有 urls=8080）
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 创建 appsettings.json，配置 urls=8080
            var appSettings = @"{""urls"": ""http://*:8080""}";
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), appSettings);

            // 传入命令行参数 urls=3380，应只识别 3380，不识别 8080
            var ports = FirewallManager.DetectPorts(tempDir, "urls=http://*:3380");
            var portList = new System.Collections.Generic.List<Int32>(ports);

            // 命令行参数覆盖配置文件，应只包含 3380，不包含 8080
            Assert.Contains(3380, portList);
            Assert.DoesNotContain(8080, portList);
            Console.WriteLine($"命令行覆盖后的端口: {String.Join(", ", portList)}");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "命令行参数多种格式提取端口")]
    public void ExtractPortsFromArgumentsMultipleFormats()
    {
        // 测试 --urls 带引号格式
        var ports1 = FirewallManager.DetectPorts(String.Empty, "--urls \"http://*:443\"");
        Assert.Contains(443, ports1);

        // 测试 urls= 多地址格式
        var ports2 = FirewallManager.DetectPorts(String.Empty, "urls=http://*:5000;https://*:5001");
        Assert.Contains(5000, ports2);
        Assert.Contains(5001, ports2);

        Console.WriteLine("多种格式参数提取端口测试通过");
    }
}
