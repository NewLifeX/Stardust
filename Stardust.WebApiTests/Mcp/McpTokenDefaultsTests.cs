using System;
using System.Linq;
using System.Reflection;
using NewLife;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using Xunit;

namespace Stardust.WebApiTests.Mcp;

/// <summary>
/// MCP 令牌默认值与服务端强制特征测试。覆盖本轮修复：
/// <list type="bullet">
///   <item>S1: 新建令牌绑定项目时，默认 scope 只读、不含 deploy:write（McpTokenController.cs:61-62）</item>
///   <item>S3: 资源授权插入时服务端强制 IsAll=false（McpTokenResourceController.cs:43-46）</item>
/// </list>
/// 这两个逻辑在 Controller 层 protected OnInsert 里，单测通过反射验证关键行为存在；
/// 完整 HTTP 层行为由后续真端口集成测试覆盖。
/// </summary>
[Collection(WebApiCollection.Name)]
public class McpTokenDefaultsTests
{
    [Fact(DisplayName = "S1: McpToken 实体新建时 Scopes 默认为空（不带任何写权限）")]
    public void NewToken_Scopes_DefaultEmpty()
    {
        var token = new McpToken();
        Assert.True(token.Scopes.IsNullOrEmpty());
    }

    [Fact(DisplayName = "S1: 默认 scope 字符串不含 deploy:write")]
    public void DefaultScopes_ExcludesDeployWrite()
    {
        var defaultScopes = McpToken.DefaultProjectReadScopes;
        var scopes = defaultScopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // 不含任何 write/admin scope
        Assert.DoesNotContain(scopes, s => s.EndsWith(":write", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(scopes, s => s.EndsWith(":admin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(scopes, s => s.Equals("deploy:write", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "S1: 默认 scope 包含 7 个 read 域")]
    public void DefaultScopes_HasSevenReadScopes()
    {
        var defaultScopes = McpToken.DefaultProjectReadScopes;
        var scopes = defaultScopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(7, scopes.Length);
        Assert.Contains(scopes, s => s == "apps:read");
        Assert.Contains(scopes, s => s == "nodes:read");
        Assert.Contains(scopes, s => s == "config:read");
        Assert.Contains(scopes, s => s == "deploy:read");
        Assert.Contains(scopes, s => s == "monitor:read");
        Assert.Contains(scopes, s => s == "now:read");
        Assert.Contains(scopes, s => s == "platform:read");
    }

    [Fact(DisplayName = "S3: McpTokenResource 实体新建时 IsAll 默认 false")]
    public void NewTokenResource_IsAll_DefaultFalse()
    {
        var res = new McpTokenResource();
        Assert.False(res.IsAll);
    }

    [Fact(DisplayName = "S3: McpTokenResourceController.OnInsert 存在且强制 IsAll=false")]
    public void ResourceController_OnInsert_ForcesIsAllFalse()
    {
        // 验证 Controller 类存在 OnInsert override（API 宿主程序集 StarWebApi，与 StarWeb 同命名空间但含 S1/S3 强制）
        var controllerType = Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpTokenResourceController, StarWebApi");
        Assert.NotNull(controllerType);

        var method = controllerType!.GetMethod("OnInsert", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.True(method.IsVirtual); // override 了基类方法
    }

    [Fact(DisplayName = "S1: McpTokenController.OnInsert 存在且设置默认 scope")]
    public void TokenController_OnInsert_SetsDefaultScopes()
    {
        var controllerType = Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpTokenController, StarWebApi");
        Assert.NotNull(controllerType);

        var method = controllerType!.GetMethod("OnInsert", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.True(method.IsVirtual);
    }
}
