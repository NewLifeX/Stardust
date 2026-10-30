using System;
using Stardust.Models;
using Xunit;

namespace ServerTest.Services;

/// <summary>应用重注册全局默认测试。验证“默认（跟随全局）”解析，以及枚举取值对历史数据的兼容</summary>
public class AppReRegisterGlobalTests
{
    [Fact(DisplayName = "重注册模式取值：0=默认，1=开放，2=可信接入，3=临时放行，4=关闭")]
    public void Values_AreCompatible()
    {
        Assert.Equal(0, (Int32)ReRegisterModes.Default);
        Assert.Equal(1, (Int32)ReRegisterModes.Open);
        Assert.Equal(2, (Int32)ReRegisterModes.Trusted);
        Assert.Equal(3, (Int32)ReRegisterModes.Temporary);
        Assert.Equal(4, (Int32)ReRegisterModes.Off);
    }

    [Fact(DisplayName = "跟随全局模式解析为全局默认")]
    public void Resolve_Default_FollowsGlobal()
    {
        Assert.Equal(ReRegisterModes.Open, ReRegisterModes.Default.Resolve(ReRegisterModes.Open));
        Assert.Equal(ReRegisterModes.Trusted, ReRegisterModes.Default.Resolve(ReRegisterModes.Trusted));
        Assert.Equal(ReRegisterModes.Off, ReRegisterModes.Default.Resolve(ReRegisterModes.Off));
    }

    [Fact(DisplayName = "显式模式优先于全局默认")]
    public void Resolve_Explicit_IgnoresGlobal()
    {
        Assert.Equal(ReRegisterModes.Off, ReRegisterModes.Off.Resolve(ReRegisterModes.Open));
        Assert.Equal(ReRegisterModes.Temporary, ReRegisterModes.Temporary.Resolve(ReRegisterModes.Off));
        Assert.Equal(ReRegisterModes.Trusted, ReRegisterModes.Trusted.Resolve(ReRegisterModes.Open));
        Assert.Equal(ReRegisterModes.Open, ReRegisterModes.Open.Resolve(ReRegisterModes.Off));
    }

    [Fact(DisplayName = "全局默认不可再为跟随全局，按关闭兜底")]
    public void Resolve_GlobalDefault_FallsBackToOff()
    {
        Assert.Equal(ReRegisterModes.Off, ReRegisterModes.Default.Resolve(ReRegisterModes.Default));
    }
}
