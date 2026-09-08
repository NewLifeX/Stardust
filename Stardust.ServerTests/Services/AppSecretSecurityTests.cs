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

    [Fact(DisplayName = "开窗超过宽限期后兜底关闭重注册窗口")]
    public void CloseReRegisterWindows_Test()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-def", Enable = true, AllowReRegister = true };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            // 宽限期传0=无条件强制兜底关闭，等价于窗口开启远超宽限时长的场景
            service.CloseReRegisterWindows(0);

            // 已关闭重注册窗口（不再出现在允许重注册集合）
            Assert.DoesNotContain(name, App.FindAllAllowReRegister().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "开窗未满宽限时长不被兜底关闭，慢启动客户端仍有收敛时间")]
    public void ReRegisterWindow_Fresh_NotClosed()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-fresh", Enable = true, AllowReRegister = true };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            service.CloseReRegisterWindows();

            // 刚开窗（UpdateTime=now）未满10分钟宽限，窗口保留等待客户端收敛
            Assert.Contains(name, App.FindAllAllowReRegister().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "补密钥开窗后同轮关窗仍保留窗口（回归：同轮秒关导致客户端错过收敛）")]
    public void FixEmptySecret_ThenClose_WindowStillOpen()
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
            service.CloseReRegisterWindows();

            // 刚补密钥并开窗，宽限期内不应被同轮兜底关闭，客户端下次重试仍可收敛
            Assert.Contains(name, App.FindAllAllowReRegister().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }
}
