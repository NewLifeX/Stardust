using System;
using System.Reflection;
using Stardust;
using Xunit;

namespace ClientTest;

public class DpiNormalizeTests
{
    private static String? Normalize(String? dpi)
    {
        var mi = typeof(StarClient).GetMethod("NormalizeDpi", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);

        return (String?)mi.Invoke(null, [dpi]);
    }

    [Theory(DisplayName = "DPI 规范化：过滤异常值")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0*96")]
    [InlineData("96*0")]
    [InlineData("3072*3072")]
    [InlineData("9999*96")]
    public void NormalizeDpi_FilterInvalid(String? dpi)
    {
        var rs = Normalize(dpi);
        Assert.Null(rs);
    }

    [Theory(DisplayName = "DPI 规范化：保留或规整合法值")]
    [InlineData("96*96", "96*96")]
    [InlineData("96x96", "96*96")]
    [InlineData("120*120", "120*120")]
    [InlineData("96*97", "96*96")]
    public void NormalizeDpi_NormalizeValid(String input, String expected)
    {
        var rs = Normalize(input);
        Assert.Equal(expected, rs);
    }
}
