using Stardust.Deployment;
using Xunit;

namespace ClientTest;

public class NginxFileTests
{
    [Fact]
    public void Parse()
    {
        var txt = """
            server {
            	listen	80;
            	listen	443 ssl;
            	listen	[::]:80;
            	listen	[::]:443 ssl;

            	server_name	star.newlifex.com;

            	ssl_certificate	/root/certs/newlifex.com.pem;
            	ssl_certificate_key	/root/certs/newlifex.com.privatekey.pem;

            	location / {
            		proxy_pass	http://127.0.0.1:6680/;
            		proxy_http_version	1.1;
            		proxy_set_header	Upgrade $http_upgrade;
            		proxy_set_header	Connection "upgrade";
            		proxy_set_header	Host $host;
            		proxy_set_header	X-Real-IP $remote_addr;
            		proxy_set_header	X-Forwarded-For $proxy_add_x_forwarded_for;
            		proxy_set_header	X-Forwarded-Proto $scheme;
            		proxy_cache_bypass	$http_upgrade;
            		client_max_body_size	1024M;
            	}
            }

            """;

        var nginxFile = new NginxFile();
        var rs = nginxFile.Parse(txt);

        Assert.True(rs);
        Assert.Equal("star.newlifex.com", nginxFile.ServerName);
        Assert.Equal([80, 443], nginxFile.Ports);
        Assert.True(nginxFile.SupportIPv6);
        Assert.Equal("/root/certs/newlifex.com.pem", nginxFile.SslCertificate);
        Assert.Equal("/root/certs/newlifex.com.privatekey.pem", nginxFile.SslCertificateKey);
        Assert.Empty(nginxFile.Directives);
        Assert.Single(nginxFile.Blocks);

        var block = nginxFile.Blocks[0];
        Assert.Equal("location /", block.Name);

        var ds = block.Directives;
        Assert.Equal("http://127.0.0.1:6680/", ds["proxy_pass"][0]);
        Assert.Equal("1.1", ds["proxy_http_version"][0]);
        Assert.Equal("$http_upgrade", ds["proxy_cache_bypass"][0]);
        Assert.Equal("1024M", ds["client_max_body_size"][0]);
        //Assert.Equal("$http_upgrade", ds["proxy_set_header Upgrade"][0]);
        //Assert.Equal("upgrade", ds["proxy_set_header Connection"][0]);
        //Assert.Equal("$host", ds["proxy_set_header Host"][0]);
        //Assert.Equal("$http_upgrade", ds["proxy_cache_bypass"][0]);
        //Assert.Equal("$remote_addr", ds["proxy_set_header X-Real-IP"][0]);
        //Assert.Equal("$proxy_add_x_forwarded_for", ds["proxy_set_header X-Forwarded-For"][0]);
        //Assert.Equal("$scheme", ds["proxy_set_header X-Forwarded-Proto"][0]);

        var txt2 = nginxFile.ToString();
        Assert.Equal(txt, txt2);
    }
}
