using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using Stardust.Models;
#if NET45_OR_GREATER || NETCOREAPP || NETSTANDARD
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust.Registry;

/// <summary>服务改变的委托</summary>
/// <param name="serviceName">服务名</param>
/// <param name="services">服务提供者集合</param>
public delegate void ServiceChangedCallback(String serviceName, ServiceModel[] services);

/// <summary>注册客户端</summary>
public interface IRegistry
{
    /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
    /// <param name="serviceName"></param>
    /// <param name="callback"></param>
    void Bind(String serviceName, ServiceChangedCallback callback);

    /// <summary>发布服务（延迟），直到回调函数返回地址信息才做真正发布</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="addressCallback">服务地址回调</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    PublishServiceInfo Register(String serviceName, Func<String?> addressCallback, String? tag = null, String? health = null);

    /// <summary>发布服务（直达）</summary>
    /// <remarks>
    /// 可以多次调用注册，用于更新服务地址和特性标签等信息。
    /// 例如web应用，刚开始时可能并不知道自己的外部地址（域名和端口），有用户访问以后，即可得知并更新。
    /// </remarks>
    /// <param name="serviceName">服务名</param>
    /// <param name="address">服务地址</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    Task<PublishServiceInfo> RegisterAsync(String serviceName, String address, String? tag = null, String? health = null);

    /// <summary>发布服务（底层）。定时反复执行，让服务端更新注册信息</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    Task<ServiceModel?> RegisterAsync(PublishServiceInfo service);

    /// <summary>消费服务（底层）</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    Task<ServiceModel[]?> ResolveAsync(ConsumeServiceInfo service);

    /// <summary>消费得到服务地址信息</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="minVersion">最小版本</param>
    /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
    /// <returns></returns>
    Task<ServiceModel[]?> ResolveAsync(String serviceName, String? minVersion = null, String? tag = null);

    /// <summary>取消服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    PublishServiceInfo? Unregister(String serviceName);

    /// <summary>取消服务（底层）</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    Task<ServiceModel?> UnregisterAsync(PublishServiceInfo service);
}

/// <summary>
/// 服务注册客户端扩展
/// </summary>
public static class RegistryExtensions
{
    /// <summary>为指定服务创建客户端，从星尘注册中心获取服务地址。单例，应避免频繁创建客户端</summary>
    /// <param name="registry">服务注册客户端</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static async Task<IApiClient> CreateForServiceAsync(this IRegistry registry, String serviceName, String? tag = null)
    {
        var http = new ApiHttpClient
        {
            RoundRobin = true,

            //Log = (registry as ILogFeature).Log,
            Tracer = DefaultTracer.Instance,
        };
        if (registry is ILogFeature logFeature) http.Log = logFeature.Log;
        if (registry is ITracerFeature tracerFeature) http.Tracer = tracerFeature.Tracer;

        var models = await registry.ResolveAsync(serviceName, null, tag).ConfigureAwait(false);

        if (models != null) BindServices(http, models);

        registry.Bind(serviceName, (k, ms) => BindServices(http, ms));

        return http;
    }

    /// <summary>为指定服务创建客户端，从星尘注册中心获取服务地址。单例，应避免频繁创建客户端</summary>
    /// <param name="registry">服务注册客户端</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public static IApiClient CreateForService(this IRegistry registry, String serviceName, String? tag = null) => TaskEx.Run(() => CreateForServiceAsync(registry, serviceName, tag)).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>绑定客户端到服务集合，更新服务地址</summary>
    /// <param name="client"></param>
    /// <param name="ms"></param>
    public static void BindServices(this ApiHttpClient client, ServiceModel[] ms)
    {
        if (ms == null || ms.Length == 0) return;

        var serviceName = ms[0].ServiceName;
        var services = client.Services;
        var dic = services.ToDictionary(e => e.Name, e => e);
        var names = new List<String>();
        foreach (var item in ms)
        {
            // 同时考虑两个地址
            var name = item.Client;
            var addrs = (item.Address + "," + item.Address2).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var set = new HashSet<String>();
            for (var i = 0; i < addrs.Length; i++)
            {
                var addr = addrs[i];
                if (!addr.StartsWithIgnoreCase("http://", "https://")) continue;
                if (set.Contains(addr)) continue;
                set.Add(addr);

                // 第一个使用Client名，后续地址增加#2后缀
                var svcName = i <= 0 && !name.IsNullOrEmpty() ? name : $"{name}#{i + 1}";
                if (!dic.TryGetValue(svcName, out var svc))
                {
                    svc = new ApiHttpClient.Service
                    {
                        Name = svcName,
                        Address = new Uri(addr),
                        Weight = item.Weight,
                    };
                    services.Add(svc);
                    dic.Add(svcName, svc);

                    XTrace.WriteLine("服务[{0}]新增地址：name={1} address={2} weight={3}", serviceName, svcName, svc.Address, item.Weight);
                }
                else
                {
                    svc.Address = new Uri(addr);
                    svc.Weight = item.Weight;
                }
                names.Add(svcName);
            }
        }

        // 删掉旧的
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var svc = services[i];
            if (!svc.Name.IsNullOrEmpty() && !names.Contains(svc.Name))
            {
                XTrace.WriteLine("服务[{0}]删除地址：name={1} address={2} weight={3}", serviceName, svc.Name, svc.Address, svc.Weight);

                services.RemoveAt(i);
            }
        }
    }

    /// <summary>消费得到服务地址信息</summary>
    /// <param name="registry">服务注册客户端</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="minVersion">最小版本</param>
    /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
    /// <returns></returns>
    public static async Task<String[]> ResolveAddressAsync(this IRegistry registry, String serviceName, String? minVersion = null, String? tag = null)
    {
        var ms = await registry.ResolveAsync(serviceName, minVersion, tag).ConfigureAwait(false);
        if (ms == null) return [];

        var addrs = new List<String>();
        foreach (var item in ms)
        {
            if (!item.Address.IsNullOrEmpty())
            {
                var ss = item.Address.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var elm in ss)
                {
                    if (!elm.IsNullOrEmpty() && !addrs.Contains(elm)) addrs.Add(elm);
                }
            }
        }

        return addrs.ToArray();
    }
}