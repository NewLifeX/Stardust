using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Stardust.Monitors;
using Xunit;

namespace ClientTest
{
    public class DiagnosticTests
    {

        [Fact]
        public async void TestActivity()
        {
            var tracer = NewLife.Log.DefaultTracer.Instance;

            var observer = new DiagnosticListenerObserver { Tracer = tracer };
            observer.Subscribe(new HttpDiagnosticListener());

            var http = new HttpClient();
            await http.GetStringAsync("http://www.newlifex.com?id=1234");

            var builders = tracer.TakeAll();
            Assert.Single(builders);
            Assert.Single(builders[0].Samples);
            Assert.Null(builders[0].ErrorSamples);
        }

        //private class MyObserver : IObserver<DiagnosticListener>
        //{
        //    private readonly Dictionary<String, MyObserver2> _listeners = new Dictionary<String, MyObserver2>();

        //    public void Subscribe(String listenerName, String startName, String endName, String errorName)
        //    {
        //        _listeners.Add(listenerName, new MyObserver2
        //        {
        //            StartName = startName,
        //            EndName = endName,
        //            ErrorName = errorName,
        //        });
        //    }

        //    public void OnCompleted() => throw new NotImplementedException();

        //    public void OnError(Exception error) => throw new NotImplementedException();

        //    public void OnNext(DiagnosticListener value)
        //    {
        //        if (_listeners.TryGetValue(value.Name, out var listener)) value.Subscribe(listener);
        //    }
        //}

        //private class MyObserver2 : IObserver<KeyValuePair<String, Object>>
        //{
        //    #region 属性
        //    public String StartName { get; set; }

        //    public String EndName { get; set; }

        //    public String ErrorName { get; set; }
        //    #endregion

        //    public void OnCompleted() => throw new NotImplementedException();

        //    public void OnError(Exception error) => throw new NotImplementedException();

        //    public void OnNext(KeyValuePair<String, Object> value) => XTrace.WriteLine(value.Key);
        //}
    }
}