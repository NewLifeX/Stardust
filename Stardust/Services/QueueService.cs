using System.Collections.Concurrent;

namespace Stardust.Services;

/// <summary>主动式消息服务</summary>
/// <typeparam name="TArg">数据类型</typeparam>
/// <typeparam name="TResult">结果类型</typeparam>
public interface IQueueService<TArg, TResult>
{
    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="value">消息</param>
    /// <returns></returns>
    TResult? Publish(String topic, TArg value);

    /// <summary>订阅</summary>
    /// <param name="topic">主题</param>
    /// <param name="callback">回调</param>
    Boolean Subscribe(String topic, Func<TArg, TResult> callback);

    /// <summary>取消订阅</summary>
    /// <param name="topic">主题</param>
    Boolean UnSubscribe(String topic);
}

/// <summary>轻量级主动式消息服务</summary>
/// <typeparam name="TArg">数据类型</typeparam>
/// <typeparam name="TResult">结果类型</typeparam>
public class QueueService<TArg, TResult> : IQueueService<TArg, TResult>
{
    #region 属性
    /// <summary>每个主题的所有订阅者</summary>
    private readonly ConcurrentDictionary<String, Func<TArg, TResult>> _topics = new();
    #endregion

    #region 方法
    /// <summary>发布消息</summary>
    /// <param name="topic">主题</param>
    /// <param name="value">消息</param>
    /// <returns></returns>
    public TResult? Publish(String topic, TArg value)
    {
        if (_topics.TryGetValue(topic, out var callback))
        {
            return callback(value);
        }

        return default;
    }

    /// <summary>订阅</summary>
    /// <param name="topic">主题</param>
    /// <param name="callback">回调</param>
    public Boolean Subscribe(String topic, Func<TArg, TResult> callback) => _topics.TryAdd(topic, callback);

    /// <summary>取消订阅</summary>
    /// <param name="topic">主题</param>
    public Boolean UnSubscribe(String topic) => _topics.TryRemove(topic, out _);
    #endregion
}