using System;
using Stardust.Data;
using Stardust.Models;
using Xunit;

namespace ServerTest;

/// <summary>复现埋点[/App/Ping]告警：AppOnline.Fill 中 new Version 解析非法版本串</summary>
public class AppOnlineTests
{
    /// <summary>
    /// 复现线上告警：客户端上报的版本串不是标准数字点分版本（如超过4段），
    /// Fill 内 new Version(info.Version) 抛出
    /// System.ArgumentException: Version string portion was too short or too long.
    /// 正确行为：Fill 应容忍无法解析的版本串，正常采用而不抛异常。
    /// </summary>
    [Fact]
    public void Fill_UnparseableInfoVersion_ShouldNotThrow()
    {
        // 线上场景：应用早已注册（Compile 超过30天），库中已有版本串
        var app = new App
        {
            Name = "TestApp",
            Version = "10.0.2026.0901",
            Compile = new DateTime(2025, 1, 1),
        };

        // 客户端埋点上报的版本串超过4段，new Version 解析失败
        var info = new AppInfo { Version = "10.0.2026.1013.1" };

        var online = new AppOnline();

        // 正确行为：不抛异常，且在线记录采用上报版本
        online.Fill(app, info);

        Assert.Equal("10.0.2026.1013.1", online.Version);
    }

    /// <summary>
    /// 复现第二触发路径：库中 App.Version 本身已是非法版本串
    /// （由历史登录/心跳未经校验写入），后续任意实例心跳都会在
    /// new Version(app.Version) 处抛出，导致该应用所有 Ping 持续报错。
    /// 正确行为：Fill 不应抛异常。
    /// </summary>
    [Fact]
    public void Fill_UnparseableAppVersion_ShouldNotThrow()
    {
        var app = new App
        {
            Name = "TestApp",
            Version = "1.2.3.4.5",
            Compile = new DateTime(2025, 1, 1),
        };

        // 本次上报的版本串合法，抛异常的是库中旧版本串
        var info = new AppInfo { Version = "10.0.2026.1013" };

        var online = new AppOnline();

        online.Fill(app, info);

        Assert.Equal("10.0.2026.1013", online.Version);
    }
}
