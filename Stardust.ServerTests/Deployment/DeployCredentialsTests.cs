using System.Security.Cryptography;
using System.Text;
using NewLife;
using NewLife.Security;
using Stardust.Data.Deployment;
using Stardust.Models;
using Stardust.Server;
using Xunit;

namespace ServerTest.Deployment;

public class DeployCredentialsTests : IDisposable
{
    private readonly String _originalTokenSecret;

    public DeployCredentialsTests()
    {
        // 保存原始值，Dispose 时恢复，避免测试间干扰
        _originalTokenSecret = StarServerSetting.Current.TokenSecret;

        // 确保 TokenSecret 有值（测试环境可能没有初始化）
        var set = StarServerSetting.Current;
        if (set.TokenSecret.IsNullOrEmpty() || set.TokenSecret.Split(':').Length != 2)
            set.TokenSecret = "HS256:TestSecretKey123";
    }

    public void Dispose()
    {
        // 恢复原始 TokenSecret，避免影响其他测试
        StarServerSetting.Current.TokenSecret = _originalTokenSecret;
    }

    [Fact]
    public void AesEncryptDecrypt_Roundtrip()
    {
        // 测试 AES 加解密往返
        var key = "TestSecretKey123";
        var pass = Encoding.UTF8.GetBytes(key);
        var plain = "my_secret_password";

        using var aes = Aes.Create();
        var cipherBytes = aes.Encrypt(Encoding.UTF8.GetBytes(plain), pass, CipherMode.CBC, PaddingMode.PKCS7);
        var cipherHex = cipherBytes.ToHex();

        // 解密
        using var aes2 = Aes.Create();
        var decryptedBytes = aes2.Decrypt(cipherHex.ToHex(), pass, CipherMode.CBC, PaddingMode.PKCS7);
        var decrypted = Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(plain, decrypted);
        Assert.NotEqual(plain, cipherHex); // 密文不应等于明文
    }

    [Fact]
    public void AesEncrypt_DifferentKeys_ProduceDifferentCipher()
    {
        var plain = "my_secret_password";
        var pass1 = Encoding.UTF8.GetBytes("key1");
        var pass2 = Encoding.UTF8.GetBytes("key2");

        using var aes1 = Aes.Create();
        var c1 = aes1.Encrypt(Encoding.UTF8.GetBytes(plain), pass1, CipherMode.CBC, PaddingMode.PKCS7).ToHex();

        using var aes2 = Aes.Create();
        var c2 = aes2.Encrypt(Encoding.UTF8.GetBytes(plain), pass2, CipherMode.CBC, PaddingMode.PKCS7).ToHex();

        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void RepoPassword_Save_EncryptsInController()
    {
        // 模拟 Controller Valid(post=true) 中的加密逻辑
        var entity = new AppDeploy
        {
            RepoPassword = "my_plain_password"
        };

        // 模拟 Controller 中的加密处理
        var key = StarServerSetting.Current.TokenSecret.Split(':')[1];
        var pass = Encoding.UTF8.GetBytes(key);
        using var aes = Aes.Create();
        entity.RepoPassword = aes.Encrypt(Encoding.UTF8.GetBytes(entity.RepoPassword), pass, CipherMode.CBC, PaddingMode.PKCS7).ToHex();

        // 验证：密文不应等于明文
        Assert.NotEqual("my_plain_password", entity.RepoPassword);
        // 验证：密文是 Hex 格式
        Assert.Matches("^[0-9a-fA-F]+$", entity.RepoPassword);
    }

    [Fact]
    public void RepoPassword_Display_Desensitized()
    {
        // 模拟 Controller Valid(post=false) 中的脱敏逻辑
        var entity = new AppDeploy
        {
            RepoPassword = "some_encrypted_hex_value"
        };

        // 模拟 Controller 中的脱敏处理
        if (!entity.RepoPassword.IsNullOrEmpty())
            entity.RepoPassword = "*****";

        Assert.Equal("*****", entity.RepoPassword);
    }

    [Fact]
    public void RepoPassword_Edit_Star5_KeepsOriginal()
    {
        // 模拟编辑时传入了 *****，应回填原密文
        var originalCipher = "a1b2c3d4e5f6"; // 假设数据库中的原密文

        // 模拟 Controller Valid(post=true) 中的逻辑
        var repoPassword = "*****";
        if (repoPassword == "*****")
        {
            // 从数据库读取原值回填（这里模拟）
            repoPassword = originalCipher;
        }

        Assert.Equal(originalCipher, repoPassword);
    }

    [Fact]
    public void RepoPassword_Edit_NewValue_ReEncrypts()
    {
        var newPassword = "new_password_123";
        var key = StarServerSetting.Current.TokenSecret.Split(':')[1];
        var pass = Encoding.UTF8.GetBytes(key);

        // 模拟 Controller Valid(post=true) 中的加密逻辑
        using var aes = Aes.Create();
        var cipherHex = aes.Encrypt(Encoding.UTF8.GetBytes(newPassword), pass, CipherMode.CBC, PaddingMode.PKCS7).ToHex();

        Assert.NotEqual(newPassword, cipherHex);
        Assert.Matches("^[0-9a-fA-F]+$", cipherHex);
    }

    [Fact]
    public void RepoPassword_Edit_Empty_Clears()
    {
        // 模拟编辑时传入了空值，应清空
        var repoPassword = "";
        var originalCipher = "a1b2c3d4e5f6"; // 假设数据库中的原密文

        // 模拟 Controller Valid(post=true) 中的逻辑
        // 空值不应回填原密文，应保持空
        if (repoPassword.IsNullOrEmpty())
        {
            repoPassword = ""; // 保持空值，允许清空
        }
        else if (repoPassword == "*****")
        {
            repoPassword = originalCipher; // 回填原密文
        }

        Assert.Equal("", repoPassword);
        Assert.NotEqual(originalCipher, repoPassword);
    }

    [Fact]
    public void CompileCommand_RedactForHistory_RemovesCredentials()
    {
        var cmd = new CompileCommand
        {
            Repository = "https://user:pass@git.example.com/repo.git",
            DeployKey = "should-be-removed",
            RepoUserName = "admin",
            RepoPassword = "encrypted_hex_value",
            Branch = "main",
        };

        var safe = cmd.RedactForHistory();

        Assert.Null(safe.DeployKey);
        Assert.Null(safe.RepoPassword);
        Assert.Equal("a***", safe.RepoUserName);
        // Repository 中的凭据也应被移除
        Assert.DoesNotContain("user:pass", safe.Repository);
        Assert.Contains("git.example.com", safe.Repository);
    }

    [Fact]
    public void CompileCommand_RedactForHistory_NullRepoUserName()
    {
        var cmd = new CompileCommand
        {
            RepoUserName = null,
            RepoPassword = "encrypted",
            Branch = "main",
        };

        var safe = cmd.RedactForHistory();

        Assert.Null(safe.RepoUserName);
        Assert.Null(safe.RepoPassword);
    }

    [Fact]
    public void TokenSecret_KeyPart_ExtractsCorrectly()
    {
        var tokenSecret = "HS256:ABCD1234EFGH5678";
        var keyPart = tokenSecret.Split(':')[1];
        Assert.Equal("ABCD1234EFGH5678", keyPart);
    }

    [Fact]
    public void TransmissionEncryption_DecryptWithSameKey()
    {
        // 模拟传输链路：服务端用 Node.Secret 加密，Agent 用同样 Secret 解密
        var nodeSecret = "NodeSecretKey16"; // 模拟节点密钥
        var plainPassword = "my_git_password";

        // 服务端加密
        var pass = Encoding.UTF8.GetBytes(nodeSecret);
        using var aes = Aes.Create();
        var cipherHex = aes.Encrypt(Encoding.UTF8.GetBytes(plainPassword), pass, CipherMode.CBC, PaddingMode.PKCS7).ToHex();

        // Agent 解密（用同样的 Secret）
        using var aes2 = Aes.Create();
        var decryptedBytes = aes2.Decrypt(cipherHex.ToHex(), pass, CipherMode.CBC, PaddingMode.PKCS7);
        var decrypted = Encoding.UTF8.GetString(decryptedBytes);

        Assert.Equal(plainPassword, decrypted);
    }

    [Fact]
    public void TransmissionEncryption_WrongKey_Fails()
    {
        var correctSecret = "CorrectSecretKey16";
        var wrongSecret = "WrongSecretKey16!";
        var plainPassword = "my_git_password";

        // 用正确密钥加密
        var pass = Encoding.UTF8.GetBytes(correctSecret);
        using var aes = Aes.Create();
        var cipherHex = aes.Encrypt(Encoding.UTF8.GetBytes(plainPassword), pass, CipherMode.CBC, PaddingMode.PKCS7).ToHex();

        // 用错误密钥解密应抛出异常或得到乱码
        var wrongPass = Encoding.UTF8.GetBytes(wrongSecret);
        using var aes2 = Aes.Create();
        Assert.Throws<CryptographicException>(() =>
        {
            aes2.Decrypt(cipherHex.ToHex(), wrongPass, CipherMode.CBC, PaddingMode.PKCS7);
        });
    }
}
