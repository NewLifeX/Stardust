#if !NET40
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors
{
    /// <summary>诊断监听器的观察者</summary>
    public class DiagnosticListenerObserver : IObserver<DiagnosticListener>
    {
        /// <summary>跟踪器</summary>
        public ITracer Tracer { get; set; }

        private readonly Dictionary<String, TraceDiagnosticListener> _listeners = new Dictionary<String, TraceDiagnosticListener>();

        private static Int32 _inited;
        private void Init()
        {
            if (_inited == 0 && Interlocked.CompareExchange(ref _inited, 1, 0) == 0)
            {
                DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        /// <summary>订阅新的监听器</summary>
        /// <param name="listenerName"></param>
        public void Subscribe(String listenerName)
        {
            Init();

            _listeners.Add(listenerName, new TraceDiagnosticListener
            {
                Name = listenerName,
                Tracer = Tracer,
            });
        }

        /// <summary>订阅新的监听器</summary>
        /// <param name="listener"></param>
        public void Subscribe(TraceDiagnosticListener listener)
        {
            Init();

            listener.Tracer = Tracer;
            _listeners.Add(listener.Name, listener);
        }

        void IObserver<DiagnosticListener>.OnCompleted() => throw new NotImplementedException();

        void IObserver<DiagnosticListener>.OnError(Exception error) => throw new NotImplementedException();

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
        {
#if DEBUG
            XTrace.WriteLine("DiagnosticListener: {0}", value.Name);
#endif

            if (_listeners.TryGetValue(value.Name, out var listener)) value.Subscribe(listener);
        }
    }

    /// <summary>跟踪诊断监听器</summary>
    public class TraceDiagnosticListener : IObserver<KeyValuePair<String, Object>>
    {
        #region 属性
        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>跟踪器</summary>
        public ITracer Tracer { get; set; }
        #endregion

        /// <summary>完成时</summary>
        public virtual void OnCompleted() => throw new NotImplementedException();

        /// <summary>出错</summary>
        /// <param name="error"></param>
        public virtual void OnError(Exception error) => throw new NotImplementedException();

        /// <summary>下一步</summary>
        /// <param name="value"></param>
        public virtual void OnNext(KeyValuePair<String, Object> value)
        {
            if (value.Key.IsNullOrEmpty()) return;

            // 当前活动名字匹配
            var activity = Activity.Current;
            if (activity != null)
            {
                if (activity.OperationName + ".Start" == value.Key)
                {
                    Tracer.NewSpan(activity.OperationName);
                }
                else if (activity.OperationName + ".Stop" == value.Key)
                {
                    var span = DefaultSpan.Current;
                    span?.Dispose();
                }
                else if (value.Key.EndsWith(".Exception"))
                {
                    var span = DefaultSpan.Current;
                    if (span != null && value.Value.GetValue("Exception") is Exception ex)
                    {
                        span.SetError(ex, null);
                    }
                }
            }
        }
    }
}
#endif