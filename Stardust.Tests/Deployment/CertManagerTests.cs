using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Stardust.Deployment;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests.Deployment;

/// <summary>证书下发管理器测试。覆盖文件名过滤、清单匹配、证书同步与 Nginx 证书路径解析（PUB-24）</summary>
public class CertManagerTests
{
    private static String CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-cert-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static CertManager CreateManager(String root) => new()
    {
        CertPath = Path.Combine(root, "certs"),
        DataPath = Path.Combine(root, "data"),
    };

    [Theory(DisplayName = "证书名过滤非法字符，拒绝路径穿越")]
    [InlineData("newlifex.com", "newlifex.com")]
    [InlineData("../etc/passwd", "etcpasswd")]
    [InlineData("a b*c?.pem", "abc.pem")]
    [InlineData("..", null)]
    [InlineData("", null)]
    public void GetSafeName_FiltersIllegalChars(String input, String? expected)
    {
        Assert.Equal(expected, CertManager.GetSafeName(input));
    }

    [Fact(DisplayName = "证书同步写入约定文件名并生成清单")]
    public async Task SyncAsync_WritesFilesAndManifest()
    {
        var root = CreateTempDir();
        var manager = CreateManager(root);

        var certBody = "-----BEGIN CERTIFICATE-----\ncert\n-----END CERTIFICATE-----";
        var keyBody = "-----BEGIN PRIVATE KEY-----\nkey\n-----END PRIVATE KEY-----";

        var certUrl = StartHttpServer(certBody);
        var keyUrl = StartHttpServer(keyBody);

        try
        {
            var cert = new CertInfo
            {
                Id = 1,
                Domain = "*.newlifex.com",
                Name = "newlifex.com",
                CertFile = certUrl,
                CertHash = Md5(certBody),
                KeyFile = keyUrl,
                KeyHash = Md5(keyBody),
            };

            var rs = await manager.SyncAsync([cert]);

            Assert.True(rs.Changed);
            Assert.Single(rs.Certs);

            var certFile = Path.Combine(root, "certs", "newlifex.com.pem");
            var keyFile = Path.Combine(root, "certs", "newlifex.com.privatekey.pem");
            Assert.True(File.Exists(certFile));
            Assert.True(File.Exists(keyFile));
            Assert.Equal(certBody, File.ReadAllText(certFile));
            Assert.Equal(keyBody, File.ReadAllText(keyFile));

            // 清单可再次加载，并能按通配符匹配域名
            var manifest = manager.LoadManifest();
            Assert.Single(manifest);
            Assert.Equal("newlifex.com", manifest[0].Name);
            Assert.NotNull(manager.FindByHost("sso.newlifex.com"));
            Assert.Null(manager.FindByHost("sso.feifan.link"));

            // 文件未变化时再次同步不重复下载
            var rs2 = await manager.SyncAsync([cert]);
            Assert.False(rs2.Changed);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact(DisplayName = "Nginx 配置替换证书占位符并补全 ssl 指令")]
    public void ResolveConfig_ReplacesTokensAndInjectsSsl()
    {
        var root = CreateTempDir();
        var manager = CreateManager(root);

        try
        {
            manager.SaveManifest([
                new CertManifest { Id = 1, Domain = "*.newlifex.com", Name = "newlifex.com", UpdateTime = DateTime.Now }
            ]);

            var config = """
                server {
                	listen 80;
                	listen 443 ssl;

                	server_name sso.newlifex.com;

                	location / {
                		proxy_pass http://127.0.0.1:8080/;
                	}
                }
                """;

            var rs = manager.ResolveConfig(config);

            var certDir = Path.Combine(root, "certs");
            Assert.Contains($"ssl_certificate\t{Path.Combine(certDir, "newlifex.com.pem")};", rs);
            Assert.Contains($"ssl_certificate_key\t{Path.Combine(certDir, "newlifex.com.privatekey.pem")};", rs);
            Assert.Contains("server_name sso.newlifex.com;", rs);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact(DisplayName = "已有证书指令的站点配置只替换占位符，不重复注入")]
    public void ResolveConfig_KeepsExplicitCertificate()
    {
        var root = CreateTempDir();
        var manager = CreateManager(root);

        try
        {
            var config = """
                server {
                	listen 443 ssl;

                	server_name sso.newlifex.com;

                	ssl_certificate {CertDir}/{CertName}.pem;
                	ssl_certificate_key {CertDir}/{CertName}.privatekey.pem;
                }
                """;

            var rs = manager.ResolveConfig(config);

            Assert.DoesNotContain(NginxFile.CertDirToken, rs);
            Assert.DoesNotContain(NginxFile.CertNameToken, rs);
            Assert.Equal(1, rs.Split('\n').Count(e => e.Contains("ssl_certificate") && !e.Contains("ssl_certificate_key")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static String Md5(String value) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLower();

    /// <summary>启动只响应一次的最小 HTTP 服务，返回下载地址</summary>
    private static String StartHttpServer(String body)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _ = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();

                var buffer = new Byte[1024];
                await stream.ReadAsync(buffer, 0, buffer.Length);

                var payload = Encoding.UTF8.GetBytes(body);
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, 0, header.Length);
                await stream.WriteAsync(payload, 0, payload.Length);
            }
            finally
            {
                listener.Stop();
            }
        });

        return $"http://127.0.0.1:{port}/cert";
    }
}
