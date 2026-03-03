using Stardust;
using Stardust.Models;
using Stardust.Monitors;
using Xunit;

namespace ClientTest.Monitors;

public class StarTracerTests
{
    [Fact]
    [Trait("Category", "StarTracer")]
    public void DefaultConstructorSetsAppIdFromAssembly()
    {
        var tracer = new StarTracer();

        Assert.NotNull(tracer.AppId);
        Assert.NotEmpty(tracer.AppId);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void DefaultConstructorSetsClientId()
    {
        var tracer = new StarTracer();

        Assert.NotNull(tracer.ClientId);
        Assert.Contains("@", tracer.ClientId);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void DefaultConstructorUsesSettingValues()
    {
        var set = StarSetting.Current;
        set.TracerPeriod = 45;
        set.MaxSamples = 3;
        set.MaxErrors = 15;

        var tracer = new StarTracer();

        Assert.Equal(45, tracer.Period);
        Assert.Equal(3, tracer.MaxSamples);
        Assert.Equal(15, tracer.MaxErrors);

        set.TracerPeriod = 60;
        set.MaxSamples = 1;
        set.MaxErrors = 10;
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void DefaultMaxFailsIs2880()
    {
        var tracer = new StarTracer();
        Assert.Equal(2 * 24 * 60, tracer.MaxFails);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void TrimSelfDefaultIsTrue()
    {
        var tracer = new StarTracer();
        Assert.True(tracer.TrimSelf);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void EnableMeterDefaultIsTrue()
    {
        var tracer = new StarTracer();
        Assert.True(tracer.EnableMeter);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void ConstructorWithServerThrowsOnEmpty()
    {
        Assert.Throws<ArgumentNullException>(() => new StarTracer(""));
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void ConstructorWithServerSetsClient()
    {
        var tracer = new StarTracer("http://127.0.0.1:6600");

        Assert.NotNull(tracer.Client);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void PropertiesAreSettable()
    {
        var tracer = new StarTracer
        {
            AppId = "TestApp",
            AppName = "Test Application",
            ClientId = "127.0.0.1@9999",
            MaxFails = 100,
            TrimSelf = false,
            EnableMeter = false,
            Excludes = ["health", "ping"],
        };

        Assert.Equal("TestApp", tracer.AppId);
        Assert.Equal("Test Application", tracer.AppName);
        Assert.Equal("127.0.0.1@9999", tracer.ClientId);
        Assert.Equal(100, tracer.MaxFails);
        Assert.False(tracer.TrimSelf);
        Assert.False(tracer.EnableMeter);
        Assert.Equal(["health", "ping"], tracer.Excludes);
    }

    [Fact]
    [Trait("Category", "StarTracer")]
    public void ResolverIsStarTracerResolver()
    {
        var tracer = new StarTracer();
        Assert.IsType<StarTracerResolver>(tracer.Resolver);
    }
}
