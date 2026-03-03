using Stardust;
using Xunit;

namespace ClientTest.Configs;

public class StarSettingTests
{
    [Fact]
    [Trait("Category", "StarSetting")]
    public void DefaultValues()
    {
        var setting = new StarSetting();

        Assert.False(setting.Debug);
        Assert.Equal("", setting.Server);
        Assert.Equal("", setting.ServiceAddress);
        Assert.Equal("", setting.AllowedHosts);
        Assert.Equal(60, setting.TracerPeriod);
        Assert.Equal(1, setting.MaxSamples);
        Assert.Equal(10, setting.MaxErrors);
    }

    [Fact]
    [Trait("Category", "StarSetting")]
    public void SetPropertiesRetainValues()
    {
        var setting = new StarSetting
        {
            Debug = true,
            Server = "http://star.newlifex.com:6600",
            AppKey = "MyApp",
            Secret = "mysecret",
            ServiceAddress = "https://myapp.com",
            AllowedHosts = "*.myapp.com",
            TracerPeriod = 30,
            MaxSamples = 5,
            MaxErrors = 20,
        };

        Assert.True(setting.Debug);
        Assert.Equal("http://star.newlifex.com:6600", setting.Server);
        Assert.Equal("MyApp", setting.AppKey);
        Assert.Equal("mysecret", setting.Secret);
        Assert.Equal("https://myapp.com", setting.ServiceAddress);
        Assert.Equal("*.myapp.com", setting.AllowedHosts);
        Assert.Equal(30, setting.TracerPeriod);
        Assert.Equal(5, setting.MaxSamples);
        Assert.Equal(20, setting.MaxErrors);
    }

    [Fact]
    [Trait("Category", "StarSetting")]
    public void IClientSettingCode_MapsToAppKey()
    {
        var setting = new StarSetting { AppKey = "StarApp" };

        NewLife.Remoting.Clients.IClientSetting clientSetting = setting;
        Assert.Equal("StarApp", clientSetting.Code);

        clientSetting.Code = "NewApp";
        Assert.Equal("NewApp", setting.AppKey);
    }

    [Fact]
    [Trait("Category", "StarSetting")]
    public void TracerPeriodDefault()
    {
        var setting = new StarSetting();
        Assert.Equal(60, setting.TracerPeriod);
    }

    [Fact]
    [Trait("Category", "StarSetting")]
    public void MaxSamplesDefault()
    {
        var setting = new StarSetting();
        Assert.Equal(1, setting.MaxSamples);
    }

    [Fact]
    [Trait("Category", "StarSetting")]
    public void MaxErrorsDefault()
    {
        var setting = new StarSetting();
        Assert.Equal(10, setting.MaxErrors);
    }
}
