using System.ComponentModel;
using Xunit;

namespace Stardust.Tests;

/// <summary>StarFactory 启动选项测试。</summary>
public class StarFactoryOptionsTests
{
    /// <summary>启动选项包含连接信息，并在工厂内复制一份。</summary>
    [Fact]
    [DisplayName("StarFactory 选项构造与属性转发")]
    public void OptionsConstructor()
    {
        var options = new StarOptions
        {
            Server = "http://127.0.0.1:1",
            AppId = "Stardust.Tests.Options",
            Secret = "secret",
            DelayStart = true,
            AcceptPlatformLanguage = false,
        };

        using var star = new StarFactory(options);

        Assert.Equal("http://127.0.0.1:1", star.Server);
        Assert.Equal("Stardust.Tests.Options", star.AppId);
        Assert.Equal("secret", star.Secret);
        Assert.True(star.DelayStart);
        Assert.False(star.Options.AcceptPlatformLanguage);
        Assert.NotSame(options, star.Options);
    }

    /// <summary>StarFactory 属性与 Options 双向同步。</summary>
    [Fact]
    [DisplayName("StarFactory 属性与选项双向同步")]
    public void PropertiesForwardToOptions()
    {
        using var star = new StarFactory(new StarOptions { DelayStart = true });

        star.Server = "http://127.0.0.1:2";
        Assert.Equal("http://127.0.0.1:2", star.Options.Server);

        star.Options.AppId = "Stardust.Tests.Forward";
        Assert.Equal("Stardust.Tests.Forward", star.AppId);

        star.DelayStart = false;
        Assert.False(star.Options.DelayStart);
    }
}
