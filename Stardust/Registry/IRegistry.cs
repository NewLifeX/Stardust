using System.Linq;
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
            LoadBalanceMode = LoadBalanceMode.RoundRobin,

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
    /// <param name="models"></param>
    public static void BindServices(this ApiHttpClient client, ServiceModel[] models)
    {
        if (models == null || models.Length == 0) return;

        // 从服务模型中提取地址
        var dicModels = new Dictionary<String, (ServiceModel Model, String Address, Boolean Internal)>();
        foreach (var model in models)
        {
            var ds = new[] { model.Address, model.Address2 };
            for (var i = 0; i < ds.Length; i++)
            {
                var elm = ds[i];
                if (elm.IsNullOrEmpty()) continue;

                var addrs = elm.Split([','], StringSplitOptions.RemoveEmptyEntries);
                foreach (var addr in addrs)
                {
                    if (!Uri.TryCreate(addr, UriKind.Absolute, out var uri)) continue;

                    var name = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
                    dicModels[name] = (model, addr, i == 0);
                }
            }
        }

        // 如果地址没有改变，则不更新
        var services = client.Services;
        if (services.Count == dicModels.Count)
        {
            var str1 = services.Select(e => e.UriName).OrderBy(e => e).Join(",");
            var str2 = dicModels.Keys.OrderBy(e => e).Join(",");
            if (str1.EqualIgnoreCase(str2)) return;
        }

        // 逐个地址落实更新
        var serviceName = models[0].ServiceName;
        foreach (var item in dicModels)
        {
            var svc = services.FirstOrDefault(e => e.UriName.EqualIgnoreCase(item.Key));
            if (svc != null)
            {
                services.Remove(svc);
                XTrace.WriteLine("服务[{0}]删除地址：name={1} address={2} weight={3}", serviceName, svc.Name, svc.Address, svc.Weight);
            }

            var v = item.Value;
            client.AddServer(v.Internal ? "内网" : "外网", v.Address, v.Model.Weight);
            svc = services[^1];
            XTrace.WriteLine("服务[{0}]新增地址：name={1} address={2} weight={3}", serviceName, svc.Name, v.Address, v.Model.Weight);
        }

        // 删掉旧的
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var svc = services[i];
            if (!svc.UriName.IsNullOrEmpty() && !dicModels.ContainsKey(svc.UriName))
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