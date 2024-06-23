using NewLife.Caching;
using NewLife.Remoting.Models;
using NewLife.Serialization;

namespace Stardust.Server.Services;

/// <summary>队列服务</summary>
public class AppQueueService
{
    #region 属性
    private readonly ICacheProvider _cacheProvider;
    #endregion

    #region 构造
    /// <summary>
    /// 实例化队列服务
    /// </summary>
    public AppQueueService(ICacheProvider cacheProvider)
    {
        _cacheProvider = cacheProvider;
    }
    #endregion

    /// <summary>
    /// 获取指定设备的命令队列
    /// </summary>
    /// <param name="deviceCode"></param>
    /// <returns></returns>
    public IProducerConsumer<String> GetQueue(String app, String client) => _cacheProvider.GetQueue<String>($"appcmd:{app}:{client}");

    /// <summary>
    /// 向指定应用实例发送命令
    /// </summary>
    /// <param name="app"></param>
    /// <param name="client"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public void Publish(String app, String client, CommandModel model)
    {
        var topic = $"appcmd:{app}:{client}";
        var q = _cacheProvider.GetQueue<String>(topic);
        q.Add(model.ToJson());

        // 设置过期时间，过期自动清理
        _cacheProvider.Cache.SetExpire(topic, TimeSpan.FromMinutes(30));
    }

    /// <summary>
    /// 获取指定服务响应队列
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public IProducerConsumer<CommandReplyModel> GetReplyQueue(Int64 id) => _cacheProvider.GetQueue<CommandReplyModel>($"appreply:{id}");

    /// <summary>
    /// 发送消息到服务响应队列
    /// </summary>
    /// <param name="model"></param>
    public void Reply(CommandReplyModel model)
    {
        var topic = $"appreply:{model.Id}";
        var q = _cacheProvider.GetQueue<CommandReplyModel>(topic);
        q.Add(model);

        // 设置过期时间，过期自动清理
        _cacheProvider.Cache.SetExpire(topic, TimeSpan.FromMinutes(30));
    }
}