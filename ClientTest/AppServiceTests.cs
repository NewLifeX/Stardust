using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ClientTest;

public class AppServiceTests
{
    [Fact]
    public void Test1()
    {
        var str = "http://star.newlifex.com:80/";
        var uri = new Uri(str);

        var str2 = uri.ToString();
        Assert.Equal("http://star.newlifex.com/", str2);
    }

    [Fact]
    public void Test2()
    {
        var str = "https://star.newlifex.com:443/";
        var uri = new Uri(str);

        var str2 = uri.ToString();
        Assert.Equal("https://star.newlifex.com/", str2);
    }
}
