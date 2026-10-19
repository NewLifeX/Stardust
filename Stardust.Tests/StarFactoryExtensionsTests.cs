using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewLife.Log;
using Xunit;

namespace Stardust.Tests;

/// <summary>AddStardust 一行接入注册测试。</summary>
public class StarFactoryExtensionsTests
{
    /// <summary>AddStardust 自动注册追踪中间件与启动服务，无需再调用 UseStardust。</summary>
    [Fact]
    [DisplayName("AddStardust 一行注册追踪中间件与启动服务")]
    public void AddStardustRegistersAutoMiddlewareAndHostedService()
    {
        var services = new ServiceCollection();
        services.AddStardust(null, "Stardust.Tests.OneLine", null);

        Assert.Contains(services, e => e.ServiceType == typeof(StarFactory));
        Assert.Contains(services, e => e.ServiceType == typeof(ITracer));
        Assert.Contains(services, e => e.ServiceType == typeof(IHostedService) && e.ImplementationType?.Name == "StardustTracerHostedService");
        Assert.Contains(services, e => e.ServiceType == typeof(IStartupFilter) && e.ImplementationType?.Name == "StartupFilter");
    }
}
