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
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

[Collection(StarTestServerCollection.Name)]
public class ApiTests : StarTestBase
{
    public ApiTests(StarTestServer fixture) : base(fixture) { }

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

    [Fact(DisplayName = "内网来源访问接口列表返回全部已注册接口")]
    public async Task AllTest()
    {
        var client = _server.CreateClient();

        // TestServer 默认来源为回环地址，属内网
        var rs = await client.GetAsync<IList<String>>("api/all");

        Assert.NotNull(rs);
        Assert.NotEmpty(rs);
        Assert.Contains(rs, e => e.StartsWith("GET Api/Get("));
        Assert.Contains(rs, e => e.StartsWith("GET Api/All("));
    }
}