using System;
using System.Text;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;

namespace Test;

/// <summary>
/// 部署凭据相关测试（TDD：先写测试，再实现）
/// </summary>
class DeployCredentialsTests
{
    /// <summary>
    /// 测试 BuildCloneUrl：有 SSH 密钥 + 用户名 → 组装 SSH 格式 URL
    /// </summary>
    public static void TestBuildCloneUrl_WithSshKeyAndUserName()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：SSH 密钥 + 用户名 ===");

        var url = BuildCloneUrl("https://github.com/NewLifeX/Stardust.git", "git", "-----BEGIN RSA PRIVATE KEY-----\nkeydata\n-----END RSA PRIVATE KEY-----", null);
        XTrace.WriteLine("结果：{0}", url);

        // 应该返回 SSH 格式
        if (!url.StartsWith("git@github.com:NewLifeX/Stardust.git") && !url.Contains("git@github.com"))
        {
            throw new Exception($"期望 SSH 格式 URL，实际：{url}");
        }

        XTrace.WriteLine("✓ SSH 密钥 + 用户名 → SSH 格式 URL 正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：有 SSH 密钥但无用户名 → 原样返回（兼容已有行为）
    /// </summary>
    public static void TestBuildCloneUrl_WithSshKeyOnly()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：仅有 SSH 密钥 ===");

        var originalUrl = "git@github.com:NewLifeX/Stardust.git";
        var url = BuildCloneUrl(originalUrl, null, "-----BEGIN RSA PRIVATE KEY-----\nkeydata\n-----END RSA PRIVATE KEY-----", null);

        // 已有 SSH 格式 URL，不应改变
        if (url != originalUrl)
        {
            throw new Exception($"期望原样返回，实际：{url}");
        }

        XTrace.WriteLine("✓ 仅有 SSH 密钥 → 原样返回正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：有用户名 + 密码 → 拼接 HTTPS 凭据 URL
    /// </summary>
    public static void TestBuildCloneUrl_WithUserNameAndPassword()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：用户名 + 密码 ===");

        var url = BuildCloneUrl("https://github.com/NewLifeX/Stardust.git", "myuser", null, "mypassword");
        XTrace.WriteLine("结果：{0}", url);

        // 应该包含凭据
        if (!url.Contains("myuser:mypassword@"))
        {
            throw new Exception($"期望包含凭据的 URL，实际：{url}");
        }

        XTrace.WriteLine("✓ 用户名 + 密码 → HTTPS 凭据 URL 正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：有用户名 + 密码 + SSH 密钥 → SSH 优先
    /// </summary>
    public static void TestBuildCloneUrl_SshPreferredOverPassword()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：SSH 优先于密码 ===");

        var url = BuildCloneUrl("https://github.com/NewLifeX/Stardust.git", "git", "-----BEGIN RSA PRIVATE KEY-----\nkeydata\n-----END RSA PRIVATE KEY-----", "mypassword");

        // SSH 优先，应返回 SSH 格式
        if (!url.StartsWith("git@github.com:NewLifeX/Stardust.git") && !url.Contains("git@github.com"))
        {
            throw new Exception($"期望 SSH 格式 URL（SSH 优先），实际：{url}");
        }

        XTrace.WriteLine("✓ SSH 优先于密码正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：无凭据 → 原样返回
    /// </summary>
    public static void TestBuildCloneUrl_NoCredentials()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：无凭据 ===");

        var originalUrl = "https://github.com/NewLifeX/Stardust.git";
        var url = BuildCloneUrl(originalUrl, null, null, null);

        if (url != originalUrl)
        {
            throw new Exception($"期望原样返回，实际：{url}");
        }

        XTrace.WriteLine("✓ 无凭据 → 原样返回正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：空 URL → 返回空
    /// </summary>
    public static void TestBuildCloneUrl_EmptyUrl()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：空 URL ===");

        var url = BuildCloneUrl("", "user", null, "pass");
        if (url != "")
        {
            throw new Exception($"期望空字符串，实际：{url}");
        }

        XTrace.WriteLine("✓ 空 URL → 返回空正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：null URL → 返回 null
    /// </summary>
    public static void TestBuildCloneUrl_NullUrl()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：null URL ===");

        var url = BuildCloneUrl(null, "user", null, "pass");
        if (url != null)
        {
            throw new Exception($"期望 null，实际：{url}");
        }

        XTrace.WriteLine("✓ null URL → 返回 null 正确");
    }

    /// <summary>
    /// 测试 RedactForHistory：RepoUserName 和 RepoPassword 脱敏
    /// </summary>
    public static void TestRedactForHistory_Credentials()
    {
        XTrace.WriteLine("=== 测试 RedactForHistory：凭据脱敏 ===");

        var cmd = new CompileCommand
        {
            Repository = "https://github.com/NewLifeX/Stardust.git",
            DeployKey = "should-be-removed",
            RepoUserName = "myuser",
            RepoPassword = "aabbccddee",
            Branch = "main",
        };

        var safe = cmd.RedactForHistory();

        // RepoUserName 应脱敏
        if (safe.RepoUserName != "m***")
        {
            throw new Exception($"期望 RepoUserName 脱敏为 'm***'，实际：'{safe.RepoUserName}'");
        }

        // RepoPassword 应为 null
        if (safe.RepoPassword != null)
        {
            throw new Exception($"期望 RepoPassword 为 null，实际：'{safe.RepoPassword}'");
        }

        // DeployKey 应为 null
        if (safe.DeployKey != null)
        {
            throw new Exception("期望 DeployKey 为 null");
        }

        XTrace.WriteLine("✓ RedactForHistory 凭据脱敏正确");
    }

    /// <summary>
    /// 测试 RedactForHistory：空 RepoUserName 不脱敏
    /// </summary>
    public static void TestRedactForHistory_EmptyUserName()
    {
        XTrace.WriteLine("=== 测试 RedactForHistory：空 RepoUserName ===");

        var cmd = new CompileCommand
        {
            Repository = "https://github.com/NewLifeX/Stardust.git",
            RepoUserName = "",
            RepoPassword = "aabbccddee",
        };

        var safe = cmd.RedactForHistory();

        if (safe.RepoUserName != null)
        {
            throw new Exception($"期望空 RepoUserName 为 null，实际：'{safe.RepoUserName}'");
        }

        XTrace.WriteLine("✓ 空 RepoUserName 脱敏为 null 正确");
    }

    /// <summary>
    /// 测试 RedactForHistory：Repository URL 中的凭据脱敏
    /// </summary>
    public static void TestRedactForHistory_UrlCredentials()
    {
        XTrace.WriteLine("=== 测试 RedactForHistory：URL 凭据脱敏 ===");

        var cmd = new CompileCommand
        {
            Repository = "http://user:password@git.example.com/repo.git",
            DeployKey = "should-be-removed",
            RepoUserName = "myuser",
            RepoPassword = "aabbccddee",
        };

        var safe = cmd.RedactForHistory();
        var safeJson = safe.ToJson();
        XTrace.WriteLine("脱敏后结果：{0}", safeJson);

        // URL 中的凭据应被移除
        if (safeJson.Contains("user:password"))
        {
            throw new Exception("Repository URL 中的凭据未脱敏");
        }

        // 主机名应保留
        if (!safe.Repository!.Contains("git.example.com"))
        {
            throw new Exception("Repository 主机名丢失");
        }

        XTrace.WriteLine("✓ Repository URL 凭据脱敏正确");
    }

    /// <summary>
    /// 测试日志脱敏：密码不应出现在日志中
    /// </summary>
    public static void TestLogRedaction()
    {
        XTrace.WriteLine("=== 测试日志脱敏 ===");

        var url = BuildCloneUrl("https://github.com/NewLifeX/Stardust.git", "myuser", null, "secret123");
        XTrace.WriteLine("克隆 URL：{0}", url);

        // 直接断言 RedactUrlForLog 的返回值，不依赖 BuildCloneUrl 的行为
        var redactedUrl = RedactUrlForLog(url);
        XTrace.WriteLine("脱敏后克隆 URL：{0}", redactedUrl);

        if (redactedUrl.Contains("secret123"))
        {
            throw new Exception("日志中不应包含明文密码");
        }

        // 验证脱敏格式正确：user:***@
        if (!redactedUrl.Contains("myuser:***@"))
        {
            throw new Exception($"期望脱敏格式为 'myuser:***@'，实际：{redactedUrl}");
        }

        // 验证主机名保留
        if (!redactedUrl.Contains("github.com"))
        {
            throw new Exception("脱敏后主机名丢失");
        }

        XTrace.WriteLine("✓ 日志脱敏正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：密码含特殊字符（@、:、#）
    /// </summary>
    public static void TestBuildCloneUrl_PasswordWithSpecialChars()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：密码含特殊字符 ===");

        // 密码中包含 @ 和 : 字符
        var url = BuildCloneUrl("https://github.com/NewLifeX/Stardust.git", "myuser", null, "pass@word:123#");
        XTrace.WriteLine("结果：{0}", url);

        // 密码应原样拼接（Git 会处理 URL 编码）
        if (!url.Contains("myuser:pass@word:123#@"))
        {
            throw new Exception($"期望密码原样拼接，实际：{url}");
        }

        XTrace.WriteLine("✓ 密码含特殊字符 → 原样拼接正确");
    }

    /// <summary>
    /// 测试 BuildCloneUrl：SSH 格式 URL 传入时替换用户名
    /// </summary>
    public static void TestBuildCloneUrl_SshUrlWithUserName()
    {
        XTrace.WriteLine("=== 测试 BuildCloneUrl：SSH URL 替换用户名 ===");

        var url = BuildCloneUrl("git@github.com:NewLifeX/Stardust.git", "deploy", "-----BEGIN RSA PRIVATE KEY-----\nkeydata\n-----END RSA PRIVATE KEY-----", null);
        XTrace.WriteLine("结果：{0}", url);

        // 应替换为新的用户名
        if (!url.StartsWith("deploy@github.com:NewLifeX/Stardust.git"))
        {
            throw new Exception($"期望替换用户名为 deploy，实际：{url}");
        }

        XTrace.WriteLine("✓ SSH URL 替换用户名正确");
    }

    /// <summary>
    /// 测试 RedactUrlForLog：无凭据 URL 原样返回
    /// </summary>
    public static void TestRedactUrlForLog_NoCredentials()
    {
        XTrace.WriteLine("=== 测试 RedactUrlForLog：无凭据 ===");

        var url = "https://github.com/NewLifeX/Stardust.git";
        var redacted = RedactUrlForLog(url);

        if (redacted != url)
        {
            throw new Exception($"期望原样返回，实际：{redacted}");
        }

        XTrace.WriteLine("✓ 无凭据 URL 原样返回正确");
    }

    /// <summary>
    /// 测试 RedactUrlForLog：空 URL
    /// </summary>
    public static void TestRedactUrlForLog_EmptyUrl()
    {
        XTrace.WriteLine("=== 测试 RedactUrlForLog：空 URL ===");

        var redacted = RedactUrlForLog("");
        if (redacted != "")
        {
            throw new Exception($"期望空字符串，实际：{redacted}");
        }

        XTrace.WriteLine("✓ 空 URL 返回空正确");
    }

    /// <summary>
    /// 测试 RedactUrlForLog：null URL
    /// </summary>
    public static void TestRedactUrlForLog_NullUrl()
    {
        XTrace.WriteLine("=== 测试 RedactUrlForLog：null URL ===");

        var redacted = RedactUrlForLog(null!);
        if (redacted != null)
        {
            throw new Exception($"期望 null，实际：{redacted}");
        }

        XTrace.WriteLine("✓ null URL 返回 null 正确");
    }

    /// <summary>
    /// 测试 RedactUrlForLog：仅 token 无密码格式
    /// </summary>
    public static void TestRedactUrlForLog_TokenOnly()
    {
        XTrace.WriteLine("=== 测试 RedactUrlForLog：仅 token 无密码 ===");

        var url = "https://token@github.com/NewLifeX/Stardust.git";
        var redacted = RedactUrlForLog(url);

        // 没有 : 分隔符，不应脱敏
        if (redacted != url)
        {
            throw new Exception($"期望无 : 分隔符时原样返回，实际：{redacted}");
        }

        XTrace.WriteLine("✓ 仅 token 无密码原样返回正确");
    }

    /// <summary>
    /// 测试 AES 解密 + BuildCloneUrl 完整链路
    /// </summary>
    public static void TestDecryptAndBuildCloneUrl()
    {
        XTrace.WriteLine("=== 测试解密 + BuildCloneUrl 完整链路 ===");

        // 模拟 Agent 端收到加密密码后的处理流程
        var plainPassword = "my_secret_pass";
        var nodeSecret = "TestNodeSecret123";

        // 模拟服务端用 Node.Secret 加密
        var pass = Encoding.UTF8.GetBytes(nodeSecret);
        using var aes = System.Security.Cryptography.Aes.Create();
        var cipherBytes = aes.Encrypt(Encoding.UTF8.GetBytes(plainPassword), pass, System.Security.Cryptography.CipherMode.CBC, System.Security.Cryptography.PaddingMode.PKCS7);
        var cipherHex = cipherBytes.ToHex();

        // Agent 端解密
        using var aes2 = System.Security.Cryptography.Aes.Create();
        var decryptedBytes = aes2.Decrypt(cipherHex.ToHex(), pass, System.Security.Cryptography.CipherMode.CBC, System.Security.Cryptography.PaddingMode.PKCS7);
        var decryptedPassword = Encoding.UTF8.GetString(decryptedBytes);

        // 验证解密正确
        if (decryptedPassword != plainPassword)
        {
            throw new Exception($"解密失败：期望 '{plainPassword}'，实际 '{decryptedPassword}'");
        }

        // 用解密后的密码组装 URL
        var url = BuildCloneUrl("https://github.com/NewLifeX/Stardust.git", "gituser", null, decryptedPassword);
        XTrace.WriteLine("克隆 URL：{0}", url);

        if (!url.Contains("gituser:my_secret_pass@"))
        {
            throw new Exception($"期望 URL 包含凭据，实际：{url}");
        }

        XTrace.WriteLine("✓ 解密 + URL 组装完整链路正确");
    }

    /// <summary>
    /// 运行所有 DeployCredentials 测试
    /// </summary>
    public static void RunAll()
    {
        XTrace.WriteLine("");
        XTrace.WriteLine("========== DeployCredentials 测试开始 ==========");
        XTrace.WriteLine("");

        try
        {
            // BuildCloneUrl 测试
            TestBuildCloneUrl_WithSshKeyAndUserName();
            TestBuildCloneUrl_WithSshKeyOnly();
            TestBuildCloneUrl_WithUserNameAndPassword();
            TestBuildCloneUrl_SshPreferredOverPassword();
            TestBuildCloneUrl_NoCredentials();
            TestBuildCloneUrl_EmptyUrl();
            TestBuildCloneUrl_NullUrl();
            TestBuildCloneUrl_PasswordWithSpecialChars();
            TestBuildCloneUrl_SshUrlWithUserName();

            // RedactForHistory 脱敏测试
            TestRedactForHistory_Credentials();
            TestRedactForHistory_EmptyUserName();
            TestRedactForHistory_UrlCredentials();

            // 日志脱敏测试
            TestLogRedaction();

            // RedactUrlForLog 独立测试
            TestRedactUrlForLog_NoCredentials();
            TestRedactUrlForLog_EmptyUrl();
            TestRedactUrlForLog_NullUrl();
            TestRedactUrlForLog_TokenOnly();

            // 完整链路测试
            TestDecryptAndBuildCloneUrl();

            XTrace.WriteLine("");
            XTrace.WriteLine("========== 所有 DeployCredentials 测试通过 ✓ ==========");
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("");
            XTrace.WriteLine("========== 测试失败 ✗ ==========");
            XTrace.WriteLine("错误：{0}", ex.Message);
            throw;
        }
    }

    #region 委托到 CompileCommand 静态方法

    internal static String? BuildCloneUrl(String? repository, String? userName, String? deployKey, String? password)
        => CompileCommand.BuildCloneUrl(repository, userName, deployKey, password);

    internal static String RedactUrlForLog(String url)
        => CompileCommand.RedactUrlForLog(url);

    #endregion
}
