extern alias web;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using NewLife;
using Stardust.Data;
using Xunit;

namespace ServerTest.Web;

/// <summary>应用在线控制器测试</summary>
public class AppOnlineControllerTests
{
    [Fact]
    [DisplayName("应用在线状态按心跳时间判定，历史离线会话不参与批量命令")]
    public void IsOnline_UsesHeartbeatTimeout()
    {
        var method = typeof(web::Stardust.Web.Areas.Registry.Controllers.AppOnlineController)
            .GetMethod("IsOnline", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal(false, method.Invoke(null, [null]));
        Assert.Equal(true, method.Invoke(null, [new AppOnline { UpdateTime = DateTime.Now }]));
        Assert.Equal(false, method.Invoke(null, [new AppOnline { UpdateTime = DateTime.Now.AddHours(-1) }]));
    }

    [Fact]
    [DisplayName("应用命令摘要区分已下发与无回执，不再把即发即忘误报成超时")]
    public void Summary_ReportsDispatchedAndSkipped()
    {
        var old = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("en");

            var method = typeof(web::Stardust.Web.Areas.Registry.Controllers.AppOnlineController)
                .GetMethod("Summary", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var paramType = method.GetParameters()[0].ParameterType;
            var list = Activator.CreateInstance(paramType);
            var rs = (String)method.Invoke(null, [list, 2])!;

            Assert.Contains("Dispatched 0, replied 0, no reply 0", rs);
            Assert.Contains("Skipped 2 offline", rs);
            Assert.DoesNotContain("timeout", rs, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Localization.Culture = old;
        }
    }

    [Fact]
    [DisplayName("旧版 SDK 未注册日志上报指令时给出升级提示")]
    public void FriendlyReportError_OldSdk_PromptsUpgrade()
    {
        var old = Localization.Culture;
        try
        {
            Localization.Culture = CultureInfo.GetCultureInfo("en");

            var method = typeof(web::Stardust.Web.Areas.Registry.Controllers.AppOnlineController)
                .GetMethod("FriendlyReportError", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var reply = new NewLife.Remoting.Models.CommandReplyModel { Data = "Cannot find service [app/reportLog]" };
            var rs = (String)method.Invoke(null, [reply])!;

            Assert.Contains("SDK version is too old", rs);
        }
        finally
        {
            Localization.Culture = old;
        }
    }
}
