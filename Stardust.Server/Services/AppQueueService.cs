using NewLife;
using NewLife.Caching;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Server.Services
{
    /// <summary>队列服务</summary>
    public class AppQueueService
    {
        #region 属性
        /// <summary>
        /// 队列主机
        /// </summary>
        public ICache Host { get; set; }

        private readonly ITracer _tracer;
        #endregion

        #region 构造
        /// <summary>
        /// 实例化队列服务
        /// </summary>
        public AppQueueService(IConfigProvider config, ICache cache, ITracer tracer)
        {
            if (config != null && !config["redisQueue"].IsNullOrEmpty())
            {
                var rds = new FullRedis { Name = "Queue", Tracer = tracer };
                config.Bind(rds, true, "redisQueue");
                cache = rds;
            }

            Host = cache;
            _tracer = tracer;
        }
        #endregion

        /// <summary>
        /// 获取指定设备的命令队列
        /// </summary>
        /// <param name="deviceCode"></param>
        /// <returns></returns>
        public IProducerConsumer<String> GetQueue(String app, String client)
        {
            var topic = $"appcmd:{app}:{client}";
            var q = Host.GetQueue<String>(topic);

            return q;
        }

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
            var q = Host.GetQueue<String>(topic);
            q.Add(model.ToJson());

            // 设置过期时间，过期自动清理
            Host.SetExpire(topic, TimeSpan.FromMinutes(30));
        }

        /// <summary>
        /// 获取指定服务响应队列
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IProducerConsumer<String> GetReplyQueue(Int64 id)
        {
            var q = Host.GetQueue<String>($"appreply:{id}");

            return q;
        }

        /// <summary>
        /// 发送消息到服务响应队列
        /// </summary>
        /// <param name="model"></param>
        public void Publish(CommandReplyModel model)
        {
            var topic = $"appreply:{model.Id}";
            var q = Host.GetQueue<String>(topic);
            q.Add(model.ToJson());

            // 设置过期时间，过期自动清理
            Host.SetExpire(topic, TimeSpan.FromMinutes(30));
        }
    }
}