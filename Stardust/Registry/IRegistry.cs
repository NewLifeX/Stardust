using System;
using System.Threading.Tasks;
using Stardust.Models;

namespace Stardust.Registry
{
    /// <summary>注册客户端</summary>
    public interface IRegistry
    {
        /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
        /// <param name="serviceName"></param>
        /// <param name="callback"></param>
        void Bind(String serviceName, Action<String, ServiceModel[]> callback);

        /// <summary>发布服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="addressCallback">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        void Register(String serviceName, Func<String> addressCallback, String tag = null);

        /// <summary>发布服务</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        Task<Object> RegisterAsync(PublishServiceInfo service);

        /// <summary>发布服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="address">服务地址</param>
        /// <param name="tag">特性标签</param>
        /// <returns></returns>
        Task<Object> RegisterAsync(String serviceName, String address, String tag = null);

        /// <summary>消费服务</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        Task<ServiceModel[]> ResolveAsync(ConsumeServiceInfo service);

        /// <summary>消费得到服务地址信息</summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="minVersion">最小版本</param>
        /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
        /// <returns></returns>
        Task<ServiceModel[]> ResolveAsync(String serviceName, String minVersion = null, String tag = null);

        /// <summary>取消服务</summary>
        /// <param name="serviceName">服务名</param>
        /// <returns></returns>
        Boolean Unregister(String serviceName);
      
        /// <summary>取消服务</summary>
        /// <param name="service">应用服务</param>
        /// <returns></returns>
        Task<Object> UnregisterAsync(PublishServiceInfo service);
    }
}