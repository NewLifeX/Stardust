using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using Xunit;

namespace Stardust.WebApiTests.Mcp;

/// <summary>
/// G1 修复回归测试：McpTokenController.OnUpdate 在 JSON/API 请求（无表单内容类型）下
/// 不得触发 SyncByToken 清空该令牌的资源授权。
/// 背景：OnInsert 有 resources.Count>0 守卫而 OnUpdate 没有；前端 updateToken/setTokenEnable
/// 发 JSON PUT（不含 res_* 字段），ParseResourcesFromForm 返回空列表，SyncByToken 先删后插
/// 导致该令牌全部资源授权被清空（McpGuard 对零授权放行，反而扩大权限）。
/// </summary>
[Collection(WebApiCollection.Name)]
public class McpTokenUpdateGuardTests
{
    [Fact(DisplayName = "G1: OnUpdate 在 JSON 请求下不清空资源授权")]
    public void OnUpdate_JsonRequest_KeepsResources()
    {
        // 建令牌（不手动指定 Id，避免 XCode 自增忽略）
        var token = new McpToken
        {
            Name = "g1-guard-test",
            Enable = true,
            Token = "sdmcp_test_guard_" + Guid.NewGuid().ToString("N")[..8],
            ProjectId = 1,
        };

        var resource = default(McpTokenResource);
        try
        {
            Assert.True(token.Insert() > 0);
            var tokenId = token.Id;
            Assert.True(tokenId > 0);

            // 建一条资源授权（模拟已有白名单）
            resource = new McpTokenResource
            {
                TokenId = tokenId,
                ResourceType = McpResourceType.Project.ToStorageName(),
                ResourceId = 1,
                IsAll = false,
                Enable = true,
            };
            Assert.True(resource.Insert() > 0);

            // 反射构造 WebApi 宿主控制器（程序集 StarWebApi，与 Web 宿主同命名空间）
            var type = Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpTokenController, StarWebApi");
            Assert.NotNull(type);

            var controller = Activator.CreateInstance(type!)!;
            // 无表单内容类型的请求上下文（模拟前端 JSON PUT）
            var httpContext = new DefaultHttpContext();
            typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
                .SetValue(controller, new ControllerContext { HttpContext = httpContext });

            var method = type!.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var entity = McpToken.FindById(tokenId);
            Assert.NotNull(entity);
            entity!.Name = "g1-guard-test-renamed";

            // 执行 OnUpdate（JSON 请求：Request.HasFormContentType == false）
            method!.Invoke(controller, new[] { entity });

            // 资源授权必须仍在
            var kept = McpTokenResource.FindAllByTokenId(tokenId);
            Assert.NotNull(kept);
            Assert.Single(kept!);
            Assert.Equal(McpResourceType.Project.ToStorageName(), kept![0].ResourceType);
            Assert.Equal(1, kept[0].ResourceId);
        }
        finally
        {
            if (resource != null) resource.Delete();
            token.Delete();
        }
    }

    [Fact(DisplayName = "G1: ParseResourcesFromForm 在 JSON 请求下返回空列表（不抛异常）")]
    public void ParseResourcesFromForm_JsonRequest_ReturnsEmpty()
    {
        var type = Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpTokenController, StarWebApi");
        Assert.NotNull(type);

        var controller = Activator.CreateInstance(type!)!;
        var httpContext = new DefaultHttpContext();
        typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
            .SetValue(controller, new ControllerContext { HttpContext = httpContext });

        var method = type!.GetMethod("ParseResourcesFromForm", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(controller, null);
        Assert.NotNull(result);
        var list = Assert.IsAssignableFrom<IList<McpTokenResource>>(result);
        Assert.Empty(list);
    }
}
