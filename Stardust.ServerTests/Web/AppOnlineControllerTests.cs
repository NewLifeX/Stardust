extern alias web;
using System;
using System.ComponentModel;
using System.Reflection;
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
}
