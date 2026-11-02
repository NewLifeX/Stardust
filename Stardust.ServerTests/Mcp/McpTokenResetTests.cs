using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Stardust.Data.Platform;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// MCP 令牌 Reset 回归：GET /api/Platform/McpToken/Reset?id= 不得被 Detail/{id} 误匹配；
/// 重置后应返回新 token（一次性展示）。
/// </summary>
public class McpTokenResetTests
{
    private static Type ApiControllerType =>
        Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpTokenController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi McpTokenController not found");

    [Fact(DisplayName = "Reset 动作注册独立 HttpGet 路由（避免与 Detail/{id} 冲突）")]
    public void Reset_HasDedicatedHttpGetRoute()
    {
        var method = ApiControllerType.GetMethod(McpTokenController_Reset, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        var routeAttrs = method!.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .Where(a => a.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(routeAttrs);
        Assert.Contains(routeAttrs, a => a.Template != null && a.Template.Contains("Reset", StringComparison.OrdinalIgnoreCase));
    }

    // 避免测试工程引用 StarWebApi 控制器类型名冲突，用字符串匹配实际方法名 Reset
    private const string McpTokenController_Reset = "Reset";

    [Fact(DisplayName = "Reset 执行后 Token 变更且 JsonRefresh 含新 token")]
    public void Reset_Invoked_ReturnsNewTokenInJson()
    {
        var token = new McpToken
        {
            Name = "reset-route-test-" + Guid.NewGuid().ToString("N")[..8],
            Enable = true,
            Token = McpToken.GenerateToken(),
            ProjectId = 0,
        };

        try
        {
            Assert.True(token.Insert() > 0);
            var old = token.Token;

            var controller = Activator.CreateInstance(ApiControllerType)!;
            var httpContext = new DefaultHttpContext();
            typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
                .SetValue(controller, new ControllerContext { HttpContext = httpContext });

            var reset = ApiControllerType.GetMethod(McpTokenController_Reset, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(reset);

            var actionResult = reset!.Invoke(controller, new object[] { token.Id }) as ActionResult;
            Assert.NotNull(actionResult);

            var json = Assert.IsType<JsonResult>(actionResult);
            var body = json.Value;
            Assert.NotNull(body);

            var codeProp = body!.GetType().GetProperty("Code") ?? body.GetType().GetProperty("code");
            Assert.NotNull(codeProp);
            Assert.Equal(0, Convert.ToInt32(codeProp!.GetValue(body)));

            var dataProp = body.GetType().GetProperty("Data") ?? body.GetType().GetProperty("data");
            Assert.NotNull(dataProp);
            var data = dataProp!.GetValue(body);
            Assert.NotNull(data);

            var tokenProp = data!.GetType().GetProperty("token") ?? data.GetType().GetProperty("Token");
            Assert.NotNull(tokenProp);
            var newToken = tokenProp!.GetValue(data) as string;
            Assert.False(string.IsNullOrEmpty(newToken));
            Assert.StartsWith(McpToken.TokenPrefix, newToken);
            Assert.NotEqual(old, newToken);

            var reloaded = McpToken.FindById(token.Id);
            Assert.NotNull(reloaded);
            Assert.Equal(newToken, reloaded!.Token);
        }
        finally
        {
            token.Delete();
        }
    }
}
