using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife;
using NewLife.Data;
using NewLife.Remoting;
using Xunit;

namespace Stardust.Server.Controllers.Tests;

public class ApiTests
{
    private readonly TestServer _server;

    public ApiTests()
    {
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
    }

    [Theory(DisplayName = "基础测试")]
    [InlineData("abcdef")]
    [InlineData(null)]
    public async Task BasicTest(String state)
    {
        var client = _server.CreateClient();

        var response = await client.GetAsync<HttpResponseMessage>("api", new { state });
        response.EnsureSuccessStatusCode();
        Assert.StartsWith("application/json", response.Content.Headers.ContentType + "");

        var rs = await client.GetAsync<IDictionary<String, Object>>("api", new { state });
        if (state == null)
            Assert.Null(rs["state"]);
        else
            Assert.NotNull(rs["state"]);
    }

    [Theory(DisplayName = "信息测试")]
    [InlineData("abcdef")]
    [InlineData(null)]
    public async Task InfoTest(String state)
    {
        var client = _server.CreateClient();

        var rs = await client.GetAsync<IDictionary<String, Object>>("api", new { state });
        if (state.IsNullOrEmpty())
            Assert.Null(rs["state"]);
        else
            Assert.NotNull(rs["state"]);
        Assert.Equal(state + "", rs["state"] + "");
    }

    [Theory(DisplayName = "信息测试2")]
    [InlineData("abcdef")]
    [InlineData(null)]
    public async Task Info2Test(String state)
    {
        var client = _server.CreateClient();

        var rs = await client.PostAsync<HttpResponseMessage>("api/info2", state.ToHex());
        Assert.NotNull(rs);
    }
}