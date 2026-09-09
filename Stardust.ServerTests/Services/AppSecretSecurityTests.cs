using System;
using System.Linq;
using NewLife;
using NewLife.Security;
using Stardust.Data;
using Stardust.Models;
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

    [Fact(DisplayName = "自愈服务为空密钥应用补密钥并开启收敛窗口")]
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

            // 已不在空密钥集合（被补密钥），关闭态转为收敛窗口模式
            Assert.DoesNotContain(name, App.FindAllEmptySecret().Select(e => e.Name));
            Assert.Contains(name, App.FindAllReRegisterWindow().Select(e => e.Name));
            Assert.Equal(ReRegisterModes.Auto, App.FindByName(name)?.ReRegister);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "收敛窗口超时后兜底关闭")]
    public void CloseReRegisterWindows_Test()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-def", Enable = true, ReRegister = ReRegisterModes.Auto };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            // 宽限期传0=无条件强制兜底关闭，等价于窗口开启远超宽限时长的场景
            service.CloseReRegisterWindows(0);

            // 收敛窗口已关闭（不再出现在窗口集合），模式回退到关闭
            Assert.DoesNotContain(name, App.FindAllReRegisterWindow().Select(e => e.Name));
            Assert.Equal(ReRegisterModes.Off, App.FindByName(name)?.ReRegister);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "收敛窗口未满宽限时长不被兜底关闭，慢启动客户端仍有收敛时间")]
    public void ReRegisterWindow_Fresh_NotClosed()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-fresh", Enable = true, ReRegister = ReRegisterModes.Auto };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            service.CloseReRegisterWindows();

            // 刚开窗（UpdateTime=now）未满10分钟宽限，窗口保留等待客户端收敛
            Assert.Contains(name, App.FindAllReRegisterWindow().Select(e => e.Name));
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
            Assert.Contains(name, App.FindAllReRegisterWindow().Select(e => e.Name));
            Assert.Equal(ReRegisterModes.Auto, App.FindByName(name)?.ReRegister);
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "开放模式应用不被兜底关闭，也不进收敛窗口集合")]
    public void CloseReRegisterWindows_SkipsOpen()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-open-fix", Enable = true, ReRegister = ReRegisterModes.Open };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            service.CloseReRegisterWindows(0);

            // 开放模式长期有效，不被兜底关闭，也不出现在收敛窗口集合
            Assert.Equal(ReRegisterModes.Open, App.FindByName(name)?.ReRegister);
            Assert.DoesNotContain(name, App.FindAllReRegisterWindow().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "可信接入模式应用不被兜底关闭")]
    public void CloseReRegisterWindows_SkipsTrusted()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-trust-fix", Enable = true, ReRegister = ReRegisterModes.Trusted };
        app.Insert();
        try
        {
            var service = new AppSecurityService(null);
            service.CloseReRegisterWindows(0);

            Assert.Equal(ReRegisterModes.Trusted, App.FindByName(name)?.ReRegister);
            Assert.DoesNotContain(name, App.FindAllReRegisterWindow().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }

    [Fact(DisplayName = "自愈服务为空密钥开放模式应用补密钥后保持开放")]
    public void FixEmptySecret_OpenApp_KeepsOpen()
    {
        var name = "sec_" + Rand.NextString(8);
        var app = new App { Name = name, Secret = "secret-open-abc", Enable = true, ReRegister = ReRegisterModes.Open };
        app.Insert();
        try
        {
            // 模拟存量脏数据：绕过 Valid 直接置空密钥
            App.Update("Secret=null", $"Name='{name}'");

            var service = new AppSecurityService(null);
            service.FixEmptySecret();

            var app2 = App.FindByName(name);
            Assert.False(app2?.Secret.IsNullOrEmpty());
            Assert.Equal(ReRegisterModes.Open, app2?.ReRegister);
            Assert.DoesNotContain(name, App.FindAllReRegisterWindow().Select(e => e.Name));
        }
        finally
        {
            app.Delete();
        }
    }
}
