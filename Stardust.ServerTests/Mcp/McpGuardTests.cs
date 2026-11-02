using NewLife;
using Stardust.Data;
using Stardust.Data.Platform;
using Stardust.Mcp;
using Xunit;

namespace ServerTest.Mcp;

/// <summary>
/// McpGuard 特征测试。覆盖本轮修复：
/// <list type="bullet">
///   <item>D-2a: NeedProject 内 ProjectUser 成员校验（McpGuard.cs:19-24）</item>
///   <item>D-2b: CheckResource 资源白名单（McpGuard.cs:28-39，经 App/Node 入口间接测）</item>
///   <item>S2: Secret/Mask 敏感值判断（McpGuard.cs:105-116）</item>
/// </list>
/// 测试连真实 SQLite，所有自建数据用 sentinel Id（900000+）并在 finally 清理。
/// </summary>
public class McpGuardTests
{
    private const Int32 SentinelProjectId = 900001;
    private const Int32 SentinelUserId_NoMember = 900001;
    private const Int32 SentinelUserId_IsMember = 900002;
    private const Int32 SentinelTokenId = 999999;

    #region Secret / Mask 纯函数

    [Theory]
    [InlineData("Db_Password")]
    [InlineData("redis_connection_string")]
    [InlineData("ApiKey")]
    [InlineData("access_token")]
    [InlineData("Authorization")]
    [InlineData("Secret")]
    [InlineData("Conn")]
    public void Secret_SensitiveKey_ReturnsTrue(String key)
    {
        Assert.True(McpGuard.Secret(key));
    }

    [Theory]
    [InlineData("AppName")]
    [InlineData("Version")]
    [InlineData("Enable")]
    [InlineData("Port")]
    [InlineData("Description")]
    public void Secret_NormalKey_ReturnsFalse(String key)
    {
        Assert.False(McpGuard.Secret(key));
    }

    [Fact]
    public void Secret_EmptyOrNull_ReturnsFalse()
    {
        Assert.False(McpGuard.Secret(null));
        Assert.False(McpGuard.Secret(String.Empty));
    }

    [Fact]
    public void Mask_ReturnsAsterisks()
    {
        Assert.Equal("***", McpGuard.Mask("anything"));
        Assert.Equal("***", McpGuard.Mask("a"));
    }

    [Fact]
    public void Mask_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Null(McpGuard.Mask(null));
        Assert.Equal(String.Empty, McpGuard.Mask(String.Empty));
    }

    #endregion

    #region NeedProject 成员校验（D-2a）

    [Fact(DisplayName = "NeedProject-未绑定项目抛32003")]
    public void NeedProject_NoProject_Throws()
    {
        var ctx = new McpContext { ProjectId = 0, UserId = 0 };
        var ex = Assert.Throws<McpException>(() => McpGuard.NeedProject(ctx));
        Assert.Equal(-32003, ex.Code);
    }

    [Fact(DisplayName = "NeedProject-老令牌UserId=0跳过成员校验")]
    public void NeedProject_OldToken_SkipsMemberCheck()
    {
        // 老令牌 UserId==0，即使项目里没有任何成员也应放行
        var ctx = new McpContext { ProjectId = SentinelProjectId, UserId = 0 };
        // 不应抛异常
        McpGuard.NeedProject(ctx);
    }

    [Fact(DisplayName = "NeedProject-用户非项目成员抛32003")]
    public void NeedProject_UserNotMember_Throws()
    {
        // 清理可能残留的同号行
        foreach (var e in ProjectUser.FindAll(ProjectUser._.UserId == SentinelUserId_NoMember & ProjectUser._.ProjectId == SentinelProjectId))
            e.Delete();

        var ctx = new McpContext { ProjectId = SentinelProjectId, UserId = SentinelUserId_NoMember };
        var ex = Assert.Throws<McpException>(() => McpGuard.NeedProject(ctx));
        Assert.Equal(-32003, ex.Code);
        // 实际文案：用户 {uid} 不是项目 {pid} 的成员，无权访问。
        Assert.Contains("无权访问", ex.Message);
    }

    [Fact(DisplayName = "NeedProject-用户是项目成员放行")]
    public void NeedProject_UserIsMember_Passes()
    {
        // 清理残留
        foreach (var e in ProjectUser.FindAll(ProjectUser._.UserId == SentinelUserId_IsMember & ProjectUser._.ProjectId == SentinelProjectId))
            e.Delete();

        var pu = new ProjectUser { ProjectId = SentinelProjectId, UserId = SentinelUserId_IsMember, Enable = true };
        try
        {
            pu.Insert();
            var ctx = new McpContext { ProjectId = SentinelProjectId, UserId = SentinelUserId_IsMember };
            // 不应抛异常
            McpGuard.NeedProject(ctx);
        }
        finally
        {
            pu.Delete();
        }
    }

    [Fact(DisplayName = "NeedProject-成员Enable=false视为非成员")]
    public void NeedProject_MemberDisabled_Throws()
    {
        var pu = new ProjectUser { ProjectId = SentinelProjectId, UserId = SentinelUserId_IsMember, Enable = false };
        try
        {
            pu.Insert();
            var ctx = new McpContext { ProjectId = SentinelProjectId, UserId = SentinelUserId_IsMember };
            var ex = Assert.Throws<McpException>(() => McpGuard.NeedProject(ctx));
            Assert.Equal(-32003, ex.Code);
        }
        finally
        {
            pu.Delete();
        }
    }

    #endregion

    #region CheckResource 白名单（D-2b，经 App 入口间接测）

    [Fact(DisplayName = "CheckResource-无资源记录时向后兼容放行")]
    public void App_NoResourceRecords_PassesThrough()
    {
        // 找一个真实存在的应用
        var app = Stardust.Data.App.FindAll().FirstOrDefault();
        Assert.NotNull(app);

        // TokenId=SentinelTokenId 不对应任何 McpTokenResource 记录
        var ctx = new McpContext
        {
            TokenId = SentinelTokenId,
            ProjectId = app.ProjectId,
            UserId = 0,
        };

        // 无资源记录 → 不应因 CheckResource 抛异常
        // 可能因 ProjectId 不匹配抛，但我们用 app.ProjectId
        var result = McpGuard.App(ctx, app.Id);
        Assert.Equal(app.Id, result.Id);
    }

    [Fact(DisplayName = "CheckResource-资源记录存在但不匹配抛32003")]
    public void App_ResourceWhitelistMismatch_Throws()
    {
        var app = Stardust.Data.App.FindAll().FirstOrDefault();
        Assert.NotNull(app);

        // 清理残留
        foreach (var e in McpTokenResource.FindAllByTokenId(SentinelTokenId)) e.Delete();

        // 建一条白名单：只授权 app.Id + 1（肯定不匹配当前 app）
        var res = new McpTokenResource
        {
            TokenId = SentinelTokenId,
            ResourceType = "App",
            ResourceId = app.Id + 1000,
            IsAll = false,
            Enable = true,
        };

        try
        {
            res.Insert();
            var ctx = new McpContext
            {
                TokenId = SentinelTokenId,
                ProjectId = app.ProjectId,
                UserId = 0,
            };

            var ex = Assert.Throws<McpException>(() => McpGuard.App(ctx, app.Id));
            Assert.Equal(-32003, ex.Code);
            Assert.Contains("未被授权访问", ex.Message);
        }
        finally
        {
            res.Delete();
        }
    }

    [Fact(DisplayName = "CheckResource-ResourceType大小写不敏感匹配")]
    public void App_ResourceTypeCaseInsensitive_Passes()
    {
        var app = Stardust.Data.App.FindAll().FirstOrDefault();
        Assert.NotNull(app);

        foreach (var e in McpTokenResource.FindAllByTokenId(SentinelTokenId)) e.Delete();

        // 用小写 "app" 存，CheckResource 传 "App"
        var res = new McpTokenResource
        {
            TokenId = SentinelTokenId,
            ResourceType = "app",
            ResourceId = app.Id,
            IsAll = false,
            Enable = true,
        };

        try
        {
            res.Insert();
            var ctx = new McpContext
            {
                TokenId = SentinelTokenId,
                ProjectId = app.ProjectId,
                UserId = 0,
            };

            // 应通过（大小写不敏感）
            var result = McpGuard.App(ctx, app.Id);
            Assert.Equal(app.Id, result.Id);
        }
        finally
        {
            res.Delete();
        }
    }

    [Fact(DisplayName = "CheckResource-IsAll=true时类型匹配即放行")]
    public void App_IsAllTypeMatch_Passes()
    {
        var app = Stardust.Data.App.FindAll().FirstOrDefault();
        Assert.NotNull(app);

        foreach (var e in McpTokenResource.FindAllByTokenId(SentinelTokenId)) e.Delete();

        var res = new McpTokenResource
        {
            TokenId = SentinelTokenId,
            ResourceType = "App",
            ResourceId = 0, // IsAll=true 时忽略
            IsAll = true,
            Enable = true,
        };

        try
        {
            res.Insert();
            var ctx = new McpContext
            {
                TokenId = SentinelTokenId,
                ProjectId = app.ProjectId,
                UserId = 0,
            };

            var result = McpGuard.App(ctx, app.Id);
            Assert.Equal(app.Id, result.Id);
        }
        finally
        {
            res.Delete();
        }
    }

    [Fact(DisplayName = "CheckResource-跨项目应用拒绝")]
    public void App_WrongProject_Throws()
    {
        var app = Stardust.Data.App.FindAll().FirstOrDefault();
        Assert.NotNull(app);

        var ctx = new McpContext
        {
            TokenId = SentinelTokenId,
            ProjectId = app.ProjectId + 9999, // 故意不匹配
            UserId = 0,
        };

        var ex = Assert.Throws<McpException>(() => McpGuard.App(ctx, app.Id));
        Assert.Equal(-32003, ex.Code);
        Assert.Contains("不属于当前令牌绑定的项目", ex.Message);
    }

    #endregion
}
