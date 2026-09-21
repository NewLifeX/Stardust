using System.Net;
using NewLife;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>代理信息测试。覆盖DNS列表解析（穿透 systemd-resolved 本地存根）</summary>
public class AgentInfoTests
{
    [Fact(DisplayName = "解析resolv.conf的DNS列表")]
    public void ParseDnsServersTest()
    {
        var content = "# 由 systemd-resolved 管理\nnameserver 127.0.0.53\nnameserver 10.0.0.1\nnameserver 223.5.5.5  # 阿里\nnameserver 10.0.0.1\n";

        var list = AgentInfo.ParseDnsServers(content);

        // 剔除本地存根与重复项，保留真实上游DNS
        Assert.Equal(2, list.Length);
        Assert.Equal("10.0.0.1", list[0]);
        Assert.Equal("223.5.5.5", list[1]);
    }

    [Fact(DisplayName = "解析空的DNS内容")]
    public void ParseDnsServersEmptyTest()
    {
        Assert.Empty(AgentInfo.ParseDnsServers(""));
        Assert.Empty(AgentInfo.ParseDnsServers("nameserver 127.0.0.53\n"));
    }

    [Fact(DisplayName = "获取DNS列表")]
    public void GetDnsListTest()
    {
        // 列表内容因环境而异，只校验格式合法且不含本地存根
        var dns = AgentInfo.GetDnsList();
        if (!dns.IsNullOrEmpty())
        {
            foreach (var item in dns.Split(','))
            {
                Assert.True(IPAddress.TryParse(item, out _), $"不是合法IP：{item}");
                Assert.False(dns.Contains("127.0.0.53"), $"不应包含本地存根：{dns}");
            }
        }
    }
}
