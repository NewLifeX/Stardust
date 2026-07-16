using System;
using System.Reflection;
using Xunit;

namespace ServerTest.Services;

/// <summary>ShardTableService 单元测试。覆盖分表日期计算逻辑</summary>
public class ShardTableServiceTests
{
    [Theory]
    [InlineData("2026-07-15", 10, "2026-07-10")]
    [InlineData("2026-07-01", 1, "2026-07-01")]
    [InlineData("2026-03-20", 15, "2026-03-15")]
    public void GetMostRecentDate_DayAlreadyPassed_ReturnsThisMonth(String nowStr, Int32 dd, String expectedStr)
    {
        var now = DateTime.Parse(nowStr);
        var expected = DateTime.Parse(expectedStr);

        var result = CallGetMostRecentDate(now, dd);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2026-07-10", 20, "2026-06-20")]
    [InlineData("2026-01-05", 10, "2025-12-10")]
    [InlineData("2026-04-15", 30, "2026-03-30")]
    public void GetMostRecentDate_DayNotYetPassed_ReturnsLastMonth(String nowStr, Int32 dd, String expectedStr)
    {
        var now = DateTime.Parse(nowStr);
        var expected = DateTime.Parse(expectedStr);

        var result = CallGetMostRecentDate(now, dd);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2026-03-15", 31, "2026-01-31")]
    [InlineData("2026-04-15", 31, "2026-03-31")]
    [InlineData("2026-05-15", 31, "2026-03-31")]
    [InlineData("2026-02-15", 30, "2026-01-30")]
    public void GetMostRecentDate_ShortMonth_Backtracks(String nowStr, Int32 dd, String expectedStr)
    {
        var now = DateTime.Parse(nowStr);
        var expected = DateTime.Parse(expectedStr);

        var result = CallGetMostRecentDate(now, dd);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2026-07-15", 32)]
    [InlineData("2026-07-15", 0)]
    [InlineData("2026-07-15", -1)]
    public void GetMostRecentDate_InvalidDay_ReturnsMinValue(String nowStr, Int32 dd)
    {
        var now = DateTime.Parse(nowStr);

        var result = CallGetMostRecentDate(now, dd);

        Assert.Equal(DateTime.MinValue, result);
    }

    /// <summary>通过反射调用 ShardTableService 的私有静态方法 GetMostRecentDate</summary>
    private static DateTime CallGetMostRecentDate(DateTime now, Int32 dd)
    {
        var type = typeof(Stardust.Server.Services.ShardTableService);
        var method = type.GetMethod("GetMostRecentDate",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
            throw new InvalidOperationException("GetMostRecentDate method not found");

        return (DateTime)method.Invoke(null, [now, dd])!;
    }
}
