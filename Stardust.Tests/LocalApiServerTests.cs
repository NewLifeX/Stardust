using System.ComponentModel;
using System.Net;
using NewLife;
using NewLife.Log;
using NewLife.Net;
using NewLife.Remoting;
using NewLife.Serialization;
using StarAgent;
using Xunit;

namespace Stardust.Tests;

/// <summary>本机 UDP API 字段名契约测试</summary>
/// <remarks>
/// StarAgent 的 Web 面板启动时会把全局 JsonHelper.Default 的命名策略改为 camelCase，
/// 本机 API 必须固定 PascalCase，否则码神工具 ApiServer 搜索等外部工具读不到 LocalIP/FileVersion。
/// </remarks>
public class LocalApiServerTests
{
    [Fact]
    [DisplayName("ApiInfo_全局驼峰策略下_仍返回PascalCase字段名")]
    public async Task ApiInfo_KeepsPascalCase_WhenGlobalJsonUsesCamelCase()
    {
        var naming = JsonHelper.Default.Options.PropertyNaming;
        try
        {
            // 模拟 StarAgent Web 面板启动时的全局设置
            JsonHelper.Default.Options.PropertyNaming = PropertyNaming.CamelCase;

            using var svr = LocalApiServer.CreateServer(new NetUri(NetType.Udp, IPAddress.Loopback.ToString(), 0), null, Logger.Null);
            svr.Start();
            Assert.True(svr.Port > 0);

            using var client = new ApiClient($"udp://127.0.0.1:{svr.Port}")
            {
                Timeout = 3_000,
            };
            var rs = await client.InvokeAsync<Object>("Api/Info");

            var dic = Assert.IsAssignableFrom<IDictionary<String, Object>>(rs);
            Assert.True(dic.ContainsKey("LocalIP"), "Api/Info 缺少 PascalCase 的 LocalIP 字段");
            Assert.True(dic.ContainsKey("FileVersion"), "Api/Info 缺少 PascalCase 的 FileVersion 字段");
            Assert.True(dic.ContainsKey("MachineName"));
        }
        finally
        {
            JsonHelper.Default.Options.PropertyNaming = naming;
        }
    }
}
