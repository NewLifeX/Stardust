using System.Net.Http;
using NewLife.Log;
using Stardust.Monitors;
using Xunit;

namespace ClientTest
{
    public class DiagnosticTests
    {
        [Fact]
        public async void TestHttp()
        {
            //var tracer = NewLife.Log.DefaultTracer.Instance;
            var tracer = new DefaultTracer();

            var observer = new DiagnosticListenerObserver { Tracer = tracer };
            observer.Subscribe(new HttpDiagnosticListener());

            var http = new HttpClient();
            await http.GetStringAsync("http://www.newlifex.com?id=1234");

            var builders = tracer.TakeAll();
            Assert.Single(builders);
            Assert.Single(builders[0].Samples);
            Assert.Null(builders[0].ErrorSamples);
        }

        [Fact]
        public async void TestHttp2()
        {
            //var tracer = NewLife.Log.DefaultTracer.Instance;
            var tracer = new DefaultTracer();

            var observer = new DiagnosticListenerObserver { Tracer = tracer };
            observer.Subscribe("HttpHandlerDiagnosticListener", "System.Net.Http.HttpRequestOut.Start", "System.Net.Http.HttpRequestOut.Stop", "System.Net.Http.Exception");

            var http = new HttpClient();
            await http.GetStringAsync("http://www.newlifex.com?id=1234");

            var builders = tracer.TakeAll();
            Assert.Single(builders);
            Assert.Single(builders[0].Samples);
            Assert.Null(builders[0].ErrorSamples);
        }

        [Fact]
        public async void TestHttp3()
        {
            //var tracer = NewLife.Log.DefaultTracer.Instance;
            var tracer = new DefaultTracer();

            var observer = new DiagnosticListenerObserver { Tracer = tracer };
            observer.Subscribe("HttpHandlerDiagnosticListener", null, null, null);

            var http = new HttpClient();
            await http.GetStringAsync("http://www.newlifex.com?id=1234");

            var builders = tracer.TakeAll();
            Assert.Single(builders);
            Assert.Single(builders[0].Samples);
            Assert.Null(builders[0].ErrorSamples);
        }
    }
}