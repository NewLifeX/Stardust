using System;
using System.IO;
using Stardust.Managers;
using Xunit;

namespace ClientTest;

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
}
