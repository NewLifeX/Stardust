using System.Net.Http;
using System.Threading.Tasks;
using NewLife.Log;
using Stardust.Monitors;
using Xunit;

// 所有测试用例放入一个汇编级集合，除非单独指定Collection特性
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace ClientTest;

[TestCaseOrderer("NewLife.UnitTest.DefaultOrderer", "NewLife.UnitTest")]
public class DiagnosticTests
{
    [Fact]
    public async Task TestHttp()
    {
        XTrace.WriteLine(nameof(TestHttp));

        //var tracer = NewLife.Log.DefaultTracer.Instance;
        var tracer = new DefaultTracer { Period = 11 };

        var observer = new DiagnosticListenerObserver { Tracer = tracer };
        observer.Subscribe(new HttpDiagnosticListener());

        var http = new HttpClient();
        await http.GetStringAsync("http://newlifex.com?id=1234");

        var builders = tracer.TakeAll();
        Assert.True(builders.Length > 0);
        Assert.Single(builders[0].Samples);
        Assert.Null(builders[0].ErrorSamples);
    }

    [Fact]
    public async Task TestHttp2()
    {
        XTrace.WriteLine(nameof(TestHttp2));

        //var tracer = NewLife.Log.DefaultTracer.Instance;
        var tracer = new DefaultTracer { Period = 22 };

        var observer = new DiagnosticListenerObserver { Tracer = tracer };
        observer.Subscribe("HttpHandlerDiagnosticListener", "System.Net.Http.HttpRequestOut.Start", "System.Net.Http.HttpRequestOut.Stop", "System.Net.Http.Exception");

        var http = new HttpClient();
        await http.GetStringAsync("http://newlifex.com?id=1234");

        var builders = tracer.TakeAll();
        Assert.Single(builders);
        Assert.Single(builders[0].Samples);
        Assert.Null(builders[0].ErrorSamples);
    }

    [Fact]
    public async Task TestHttp3()
    {
        XTrace.WriteLine(nameof(TestHttp3));

        //var tracer = NewLife.Log.DefaultTracer.Instance;
        var tracer = new DefaultTracer { Period = 33 };

        var observer = new DiagnosticListenerObserver { Tracer = tracer };
        observer.Subscribe("HttpHandlerDiagnosticListener", null, null, null);

        var http = new HttpClient();
        await http.GetStringAsync("http://newlifex.com?id=1234");

        var builders = tracer.TakeAll();
        Assert.Single(builders);
        Assert.Single(builders[0].Samples);
        Assert.Null(builders[0].ErrorSamples);
    }
}