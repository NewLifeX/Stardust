using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NewLife;
using Stardust.Data.Deployment;
using Xunit;

namespace ServerTest.Deployment;

/// <summary>webhook 多平台解析单元测试。纯内存解析，不依赖数据库</summary>
public class WebhookParserTests
{
    #region 辅助
    private static WebhookRequest Build(String body, params (String Name, String Value)[] headers)
    {
        var map = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in headers) map[item.Name] = item.Value;
        return WebhookRequest.Create(body, name => map.TryGetValue(name, out var value) ? value : null);
    }

    private const String GithubSnakeBody = @"{
        ""ref"": ""refs/heads/main"",
        ""before"": ""1111111111111111111111111111111111111111"",
        ""after"": ""2222222222222222222222222222222222222222"",
        ""head_commit"": {
            ""id"": ""2222222222222222222222222222222222222222"",
            ""message"": ""fix: snake case head commit"",
            ""timestamp"": ""2026-10-20T10:00:00Z"",
            ""author"": { ""name"": ""Alice"", ""username"": ""alice"" }
        },
        ""commits"": [
            { ""id"": ""1111111111111111111111111111111111111111"", ""message"": ""older"", ""author"": { ""name"": ""Bob"" } },
            { ""id"": ""2222222222222222222222222222222222222222"", ""message"": ""newer"", ""author"": { ""name"": ""Alice"" } }
        ],
        ""repository"": { ""name"": ""stardust"", ""full_name"": ""newlifex/stardust"" },
        ""pusher"": { ""name"": ""Alice"", ""username"": ""alice"" }
    }";

    private const String GithubCamelBody = @"{
        ""ref"": ""refs/heads/release/1.0"",
        ""before"": ""1111111111111111111111111111111111111111"",
        ""after"": ""3333333333333333333333333333333333333333"",
        ""headCommit"": {
            ""id"": ""3333333333333333333333333333333333333333"",
            ""message"": ""feat: camel case head commit"",
            ""timestamp"": ""2026-10-21T08:30:00Z"",
            ""author"": { ""name"": ""Carol"" }
        },
        ""commits"": [],
        ""repository"": { ""name"": ""stardust"", ""fullName"": ""newlifex/stardust"" },
        ""pusher"": { ""name"": ""Carol"" }
    }";

    private const String GitlabBody = @"{
        ""object_kind"": ""push"",
        ""event_name"": ""push"",
        ""ref"": ""refs/heads/master"",
        ""before"": ""1111111111111111111111111111111111111111"",
        ""after"": ""4444444444444444444444444444444444444444"",
        ""checkout_sha"": ""4444444444444444444444444444444444444444"",
        ""message"": ""push summary"",
        ""user_name"": ""David"",
        ""user_username"": ""david"",
        ""project"": { ""name"": ""stardust"", ""path_with_namespace"": ""newlifex/stardust"" },
        ""commits"": [
            { ""id"": ""4444444444444444444444444444444444444444"", ""message"": ""feat: gitlab commit"", ""timestamp"": ""2026-10-22T09:00:00Z"", ""author"": { ""name"": ""David"" } }
        ]
    }";

    private const String GiteeBody = @"{
        ""ref"": ""refs/heads/main"",
        ""before"": ""1111111111111111111111111111111111111111"",
        ""after"": ""5555555555555555555555555555555555555555"",
        ""head_commit"": {
            ""id"": ""5555555555555555555555555555555555555555"",
            ""message"": ""feat: gitee commit"",
            ""timestamp"": ""2026-10-23T09:00:00Z"",
            ""author"": { ""name"": ""Eric"" }
        },
        ""hook_name"": ""push_hooks"",
        ""user_name"": ""Eric"",
        ""repository"": { ""name"": ""stardust"", ""full_name"": ""newlifex/stardust"" },
        ""pusher"": { ""name"": ""Eric"" }
    }";

    /// <summary>MindHub post-push 钩子发送的最小负载：仅 ref/after/head_commit，无 commits/repository/pusher</summary>
    private const String MinimalHookBody = @"{
        ""ref"": ""refs/heads/main"",
        ""after"": ""6666666666666666666666666666666666666666"",
        ""head_commit"": {
            ""id"": ""6666666666666666666666666666666666666666"",
            ""message"": ""feat: minimal payload"",
            ""author"": { ""name"": ""Frank"" },
            ""timestamp"": ""2026-10-26T00:19:21Z""
        }
    }";
    #endregion

    [Fact]
    public void Parse_Github_SnakeCase_HeadCommitBound()
    {
        var request = Build(GithubSnakeBody, ("X-GitHub-Event", "push"));

        Assert.Equal("github", request.Platform);
        Assert.Equal("push", request.Event);

        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("github", evt.Platform);
        Assert.Equal("main", evt.Branch);
        Assert.Equal("2222222222222222222222222222222222222222", evt.CommitId);
        Assert.Equal("fix: snake case head commit", evt.CommitMessage);
        Assert.Equal("Alice", evt.CommitAuthor);
        Assert.Equal("newlifex/stardust", evt.Repository);
        Assert.False(evt.IsTag);
        Assert.False(evt.Deleted);
        Assert.NotNull(evt.Commits);
        Assert.Equal(2, evt.Commits.Length);
    }

    [Fact]
    public void Parse_Github_CamelCase_HeadCommitBound()
    {
        var request = Build(GithubCamelBody, ("X-GitHub-Event", "push"));

        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("release/1.0", evt.Branch);
        Assert.Equal("3333333333333333333333333333333333333333", evt.CommitId);
        Assert.Equal("feat: camel case head commit", evt.CommitMessage);
        Assert.Equal("Carol", evt.CommitAuthor);
    }

    [Fact]
    public void Parse_Gitlab_UsesCheckoutShaAndTopLevelMessage()
    {
        var request = Build(GitlabBody, ("X-Gitlab-Event", "Push Hook"));

        Assert.Equal("gitlab", request.Platform);
        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("gitlab", evt.Platform);
        Assert.Equal("master", evt.Branch);
        Assert.Equal("4444444444444444444444444444444444444444", evt.CommitId);
        Assert.Equal("feat: gitlab commit", evt.CommitMessage);
        Assert.Equal("David", evt.CommitAuthor);
        Assert.Equal("newlifex/stardust", evt.Repository);
    }

    [Fact]
    public void Parse_Gitee_PlatformFromHeader()
    {
        var request = Build(GiteeBody, ("X-Gitee-Event", "Push Hook"));

        Assert.Equal("gitee", request.Platform);
        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("gitee", evt.Platform);
        Assert.Equal("main", evt.Branch);
        Assert.Equal("feat: gitee commit", evt.CommitMessage);
        Assert.Equal("Eric", evt.CommitAuthor);
    }

    [Fact]
    public void Parse_Gitea_PlatformFromHeader()
    {
        var request = Build(GithubCamelBody, ("X-Gitea-Event", "push"));

        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("gitea", evt.Platform);
        Assert.Equal("release/1.0", evt.Branch);
    }

    [Fact]
    public void Parse_NoEventHeader_FallsBackToCustomParser()
    {
        var request = Build(GithubCamelBody);

        Assert.True(request.Platform.IsNullOrEmpty());
        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("custom", evt.Platform);
        Assert.Equal("feat: camel case head commit", evt.CommitMessage);
    }

    [Fact]
    public void Parse_MinimalHookPayload_AllConsumedFieldsPresent()
    {
        var request = Build(MinimalHookBody, ("X-Gitea-Event", "push"));

        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.Equal("gitea", evt.Platform);
        Assert.Equal("main", evt.Branch);
        Assert.Equal("6666666666666666666666666666666666666666", evt.CommitId);
        Assert.Equal("feat: minimal payload", evt.CommitMessage);
        Assert.Equal("Frank", evt.CommitAuthor);
        Assert.Equal(2026, evt.CommitTime.Year);
        Assert.False(evt.IsTag);
        Assert.False(evt.Deleted);
        Assert.Null(evt.Commits);
    }

    [Fact]
    public void Parse_TagPush_IsTag()
    {
        var body = GithubCamelBody.Replace("refs/heads/release/1.0", "refs/tags/v1.0.0");
        var request = Build(body, ("X-GitHub-Event", "push"));

        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.True(evt.IsTag);
        Assert.Equal("refs/tags/v1.0.0", evt.Branch);
    }

    [Fact]
    public void Parse_DeletedBranch_AfterAllZero()
    {
        var body = GithubCamelBody.Replace("3333333333333333333333333333333333333333", new String('0', 40));
        var request = Build(body, ("X-GitHub-Event", "push"));

        var evt = WebhookParserRegistry.Parse(request);
        Assert.NotNull(evt);
        Assert.True(evt.Deleted);
    }

    [Fact]
    public void Signature_HexDigest_Passed()
    {
        const String secret = "s3cret";
        var request = Build(GithubSnakeBody, ("X-Hub-Signature-256", "sha256=" + HmacHex(secret, GithubSnakeBody)));

        Assert.True(WebhookSignature.Verify(secret, request));
    }

    [Fact]
    public void Signature_Base64Digest_Passed()
    {
        const String secret = "s3cret";
        var request = Build(GithubSnakeBody, ("X-Hub-Signature-256", HmacBase64(secret, GithubSnakeBody)));

        Assert.True(WebhookSignature.Verify(secret, request));
    }

    [Fact]
    public void Signature_PlainToken_Passed()
    {
        var request = Build(GitlabBody, ("X-Gitlab-Token", "s3cret"));

        Assert.True(WebhookSignature.Verify("s3cret", request));
    }

    [Fact]
    public void Signature_WrongSecret_Rejected()
    {
        const String secret = "s3cret";
        var request = Build(GithubSnakeBody, ("X-Hub-Signature-256", "sha256=" + HmacHex("other", GithubSnakeBody)));

        Assert.False(WebhookSignature.Verify(secret, request));
        Assert.False(WebhookSignature.Verify("s3cret", Build(GithubSnakeBody)));
    }

    private static String HmacHex(String secret, String body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-", "");
    }

    private static String HmacBase64(String secret, String body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }
}
