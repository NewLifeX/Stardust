using System;
using System.Linq;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>应用密钥安全测试。验证强制密钥非空（Valid自动生成）、种子识别、自愈服务补密钥与关窗（安全治理）</summary>
public class AppSecretSecurityTests
{
    [Fact(DisplayName = "新建应用自动生成密钥，不允许空密钥")]
    public void NewApp_AutoSecret()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Enable = true };
        app.Insert();
        try
        {
            Assert.False(app.Secret.IsNullOrEmpty());
            Assert.Equal(16, app.Secret.Length);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "存量空密钥应用编辑保存自动补密钥")]
    public void EmptySecretApp_Update_AutoSecret()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-old", Enable = true };
        app.Insert();
        try
        {
            // 手工清空密钥后保存（模拟管理员误清空），Valid 应自动补回
            app.Secret = null;
            app.Remark = "update";
            app.Update();

            Assert.False(app.Secret.IsNullOrEmpty());
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "自愈服务为空密钥应用补密钥并开窗")]
    public void FixEmptySecret_BizApp()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-abc", Enable = true };
        app.Insert();
        try
        {
            // 模拟存量脏数据：绕过 Valid 直接置空密钥
            App.Update("Secret=null", $"Name='{name}'");

            var service = new AppSecurityService(null);
            service.FixEmptySecret();

            // 已不在空密钥集合（被补密钥），且已开启重注册窗口
            Assert.DoesNotContain(name, App.FindAllEmptySecret().Select(e => e.Name));
            Assert.Contains(name, App.FindAllAllowReRegister().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "自愈服务兜底关闭重注册窗口")]
    public void CloseReRegisterWindows_Test()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-def", Enable = true, AllowReRegister = true };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            service.CloseReRegisterWindows();

            // 已关闭重注册窗口（不再出现在允许重注册集合）
            Assert.DoesNotContain(name, App.FindAllAllowReRegister().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }
}
