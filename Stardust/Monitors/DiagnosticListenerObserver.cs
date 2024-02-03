#if NET5_0_OR_GREATER
using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors;

/// <summary>诊断监听器的观察者</summary>
public class DiagnosticListenerObserver : IObserver<DiagnosticListener>
{
    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }

    private readonly Dictionary<String, TraceDiagnosticListener> _listeners = [];

    private Int32 _inited;
    private void Init()
    {
        if (_inited == 0 && Interlocked.CompareExchange(ref _inited, 1, 0) == 0)
        {
            DiagnosticListener.AllListeners.Subscribe(this);
        }
    }

    /// <summary>订阅新的监听器</summary>
    /// <param name="listenerName">监听名称</param>
    /// <param name="startName">开始名</param>
    /// <param name="endName">结束名</param>
    /// <param name="errorName">错误名</param>
    public void Subscribe(String listenerName, String startName, String endName, String errorName)
    {
        _listeners.Add(listenerName, new TraceDiagnosticListener
        {
            Name = listenerName,
            StartName = startName,
            EndName = endName,
            ErrorName = errorName,
            Tracer = Tracer,
        });

        Init();
    }

    /// <summary>订阅新的监听器</summary>
    /// <param name="listener"></param>
    public void Subscribe(TraceDiagnosticListener listener)
    {
        listener.Tracer = Tracer;
        _listeners.Add(listener.Name, listener);

        Init();
    }

    void IObserver<DiagnosticListener>.OnCompleted() => throw new NotImplementedException();

    void IObserver<DiagnosticListener>.OnError(Exception error) => throw new NotImplementedException();

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener value)
    {
#if DEBUG
        XTrace.WriteLine("DiagnosticListener: {0}", value.Name);
#endif

        if (_listeners.TryGetValue(value.Name, out var listener) && listener != null) value.Subscribe(listener);
    }
}

/// <summary>追踪诊断监听器</summary>
public class TraceDiagnosticListener : IObserver<KeyValuePair<String, Object?>>
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>开始名称</summary>
    public String? StartName { get; set; }

    /// <summary>结束名称</summary>
    public String? EndName { get; set; }

    /// <summary>异常名称</summary>
    public String? ErrorName { get; set; }

    /// <summary>追踪器</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    /// <summary>完成时</summary>
    public virtual void OnCompleted() => throw new NotImplementedException();

    /// <summary>出错</summary>
    /// <param name="error"></param>
    public virtual void OnError(Exception error) => throw new NotImplementedException();

    /// <summary>下一步</summary>
    /// <param name="value"></param>
    public virtual void OnNext(KeyValuePair<String, Object?> value)
    {
        if (value.Key.IsNullOrEmpty()) return;

        // 当前活动名字匹配
        var activity = Activity.Current;
        if (activity != null)
        {
            var start = !StartName.IsNullOrEmpty() ? StartName : (activity.OperationName + ".Start");
            var end = !EndName.IsNullOrEmpty() ? EndName : (activity.OperationName + ".Stop");
            var error = !ErrorName.IsNullOrEmpty() ? ErrorName : (activity.OperationName + ".Exception");

            if (start == value.Key)
            {
                Tracer?.NewSpan(activity.OperationName);
            }
            else if (end == value.Key)
            {
                var span = DefaultSpan.Current;
                span?.Dispose();
            }
            else if (error == value.Key || value.Key.EndsWith(".Exception"))
            {
                var span = DefaultSpan.Current;
                if (span != null && value.Value != null && value.Value.GetValue("Exception") is Exception ex)
                {
                    span.SetError(ex, null);
                }
            }
        }
    }
}
#endif