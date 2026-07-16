using System;
using System.IO;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust;
using Stardust.Models;

namespace Test;

/// <summary>
/// DeployKey 部署密钥相关测试
/// </summary>
class DeployKeyTests
{
    /// <summary>
    /// 测试 CompileCommand 序列化时 DeployKey 会被正确包含
    /// </summary>
    public static void TestDeployKeySerialization()
    {
        XTrace.WriteLine("=== 测试 DeployKey 序列化 ===");

        var cmd = new CompileCommand
        {
            Repository = "git@github.com:test/repo.git",
            DeployKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA0Z3VS5JJcds3xfn/ygWyf8Qj6rRj9XAMPLEkeydata\n-----END RSA PRIVATE KEY-----",
            Branch = "main",
            ProjectPath = "src/Project",
            BuildArgs = "--configuration Release",
            OutputPath = "publish",
            PullCode = true,
            BuildProject = true,
            PackageOutput = true,
            UploadPackage = true
        };

        var json = cmd.ToJson();
        XTrace.WriteLine("序列化结果：{0}", json);

        // 验证 DeployKey 存在于序列化结果中（用于下发到 agent）
        if (!json.Contains("DeployKey"))
        {
            throw new Exception("DeployKey 未包含在序列化结果中，无法下发到 agent");
        }
        if (!json.Contains("-----BEGIN RSA PRIVATE KEY-----"))
        {
            throw new Exception("DeployKey 私钥内容未包含在序列化结果中");
        }

        XTrace.WriteLine("✓ DeployKey 正确包含在序列化结果中，可下发到 agent");
    }

    /// <summary>
    /// 测试脱敏后的 CompileCommand 不包含 DeployKey
    /// </summary>
    public static void TestDeployKeySanitization()
    {
        XTrace.WriteLine("=== 测试 DeployKey 脱敏 ===");

        var cmd = new CompileCommand
        {
            Repository = "git@github.com:test/repo.git",
            DeployKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA0Z3VS5JJcds3xfn/ygWyf8Qj6rRj9XAMPLEkeydata\n-----END RSA PRIVATE KEY-----",
            Branch = "main",
            ProjectPath = "src/Project",
            BuildArgs = "--configuration Release",
            OutputPath = "publish",
            PullCode = true,
            BuildProject = true,
            PackageOutput = true,
            UploadPackage = true
        };

        // 使用统一的 RedactForHistory 方法脱敏
        var safeCmd = cmd.RedactForHistory();
        var safeJson = safeCmd.ToJson();
        XTrace.WriteLine("脱敏后结果：{0}", safeJson);

        // 验证私钥内容不泄露（用于记录历史）
        // 注意：ToJson() 会保留 null 值的键，所以检查私钥内容是否泄露
        if (safeJson.Contains("-----BEGIN RSA PRIVATE KEY-----"))
        {
            throw new Exception("脱敏后私钥内容仍然存在于序列化结果中");
        }

        // 验证 DeployKey 值为 null
        if (safeCmd.DeployKey != null)
        {
            throw new Exception("脱敏后 DeployKey 不为 null");
        }

        // 验证其他字段仍然存在
        if (!safeJson.Contains("Repository"))
        {
            throw new Exception("脱敏后 Repository 字段丢失");
        }
        if (!safeJson.Contains("Branch"))
        {
            throw new Exception("脱敏后 Branch 字段丢失");
        }

        XTrace.WriteLine("✓ DeployKey 已正确脱敏，不会泄露到历史记录");
    }

    /// <summary>
    /// 测试 SSH 密钥格式验证（TrimStart 兼容性）
    /// </summary>
    public static void TestSshKeyFormatValidation()
    {
        XTrace.WriteLine("=== 测试 SSH 密钥格式验证 ===");

        // 测试带前导空白/换行的密钥
        var keyWithLeadingWhitespace = "\n  \r\n  -----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA0Z3VS5JJcds3xfn/ygWyf8Qj6rRj9XAMPLEkeydata\n-----END RSA PRIVATE KEY-----";
        var trimmedKey = keyWithLeadingWhitespace.TrimStart();

        if (!trimmedKey.StartsWith("-----BEGIN"))
        {
            throw new Exception("TrimStart 后密钥格式验证失败");
        }

        XTrace.WriteLine("✓ TrimStart 后密钥格式验证通过");

        // 测试无效密钥
        var invalidKey = "not-a-valid-key";
        var trimmedInvalidKey = invalidKey.TrimStart();
        if (trimmedInvalidKey.StartsWith("-----BEGIN"))
        {
            throw new Exception("无效密钥被错误识别为有效");
        }

        XTrace.WriteLine("✓ 无效密钥正确被拒绝");
    }

    /// <summary>
    /// 测试 StarSetting.DisableSshStrictChecking 默认值
    /// </summary>
    public static void TestDisableSshStrictCheckingDefault()
    {
        XTrace.WriteLine("=== 测试 DisableSshStrictChecking 默认值 ===");

        var setting = StarSetting.Current;

        // 默认值应为 false（启用 SSH 严格校验）
        if (setting.DisableSshStrictChecking)
        {
            XTrace.WriteLine("警告：DisableSshStrictChecking 默认值为 true，建议修改为 false");
        }
        else
        {
            XTrace.WriteLine("✓ DisableSshStrictChecking 默认值为 false（启用校验）");
        }
    }

    /// <summary>
    /// 测试 Repository URL 中的凭据被正确脱敏
    /// </summary>
    public static void TestRepositoryCredentialRedaction()
    {
        XTrace.WriteLine("=== 测试 Repository 凭据脱敏 ===");

        var cmd = new CompileCommand
        {
            Repository = "http://user:password@git.example.com/repo.git",
            DeployKey = "should-be-removed",
            Branch = "main",
        };

        var safeCmd = cmd.RedactForHistory();
        var safeJson = safeCmd.ToJson();
        XTrace.WriteLine("脱敏后结果：{0}", safeJson);

        // 验证凭据被移除
        if (safeJson.Contains("user:password"))
        {
            throw new Exception("Repository URL 中的凭据未脱敏");
        }
        if (safeJson.Contains("should-be-removed"))
        {
            throw new Exception("DeployKey 未脱敏");
        }

        // 验证 Repository 主机名保留
        if (!safeCmd.Repository!.Contains("git.example.com"))
        {
            throw new Exception("Repository 主机名丢失");
        }

        // 验证协议保留
        if (!safeCmd.Repository!.StartsWith("http://"))
        {
            throw new Exception("Repository 协议丢失");
        }

        XTrace.WriteLine("✓ Repository 凭据已正确脱敏");
    }

    /// <summary>
    /// 运行所有 DeployKey 相关测试
    /// </summary>
    public static void RunAll()
    {
        XTrace.WriteLine("");
        XTrace.WriteLine("========== DeployKey 测试开始 ==========");
        XTrace.WriteLine("");

        try
        {
            TestDeployKeySerialization();
            TestDeployKeySanitization();
            TestRepositoryCredentialRedaction();
            TestSshKeyFormatValidation();
            TestDisableSshStrictCheckingDefault();

            XTrace.WriteLine("");
            XTrace.WriteLine("========== 所有 DeployKey 测试通过 ✓ ==========");
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("");
            XTrace.WriteLine("========== 测试失败 ✗ ==========");
            XTrace.WriteLine("错误：{0}", ex.Message);
            throw;
        }
    }
}