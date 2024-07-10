﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;
using Stardust.Models;
using Stardust.Registry;
using NewLife.Caching;
using System.Net.NetworkInformation;
using NewLife.Remoting.Models;
using NewLife.Remoting.Clients;
using NewLife.Remoting;
using NewLife.Http;
using NewLife.Security;


#if !NET40
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace Stardust;

/// <summary>应用客户端。每个应用有一个客户端连接星尘服务端</summary>
public class AppClient : ClientBase, IRegistry
{
    #region 属性
    /// <summary>应用</summary>
    public String AppId { get; set; } = null!;

    /// <summary>应用名</summary>
    public String? AppName { get; set; }

    /// <summary>实例。应用可能多实例部署，ip@proccessid</summary>
    public String? ClientId { get; set; }

    /// <summary>节点编码</summary>
    public String? NodeCode { get; set; }

    /// <summary>看门狗超时时间。默认0秒</summary>
    /// <remarks>
    /// 设置看门狗超时时间，超过该时间未收到心跳，将会重启本应用进程。
    /// 0秒表示不启用看门狗。
    /// </remarks>
    public Int32 WatchdogTimeout { get; set; }

    /// <summary>星尘工厂</summary>
    public StarFactory? Factory { get; set; }

    private AppInfo? _appInfo;
    private readonly String? _version;

    /// <summary>已发布服务，记录下来，定时注册刷新</summary>
    private readonly ConcurrentDictionary<String, PublishServiceInfo> _publishServices = new();

    /// <summary>已消费服务，记录下来，定时刷新获取新的地址信息</summary>
    private readonly ConcurrentDictionary<String, ConsumeServiceInfo> _consumeServices = new();
    private readonly ConcurrentDictionary<String, ServiceModel[]> _consumes = new();
    private readonly ConcurrentDictionary<String, IList<Delegate>> _consumeEvents = new();
    private readonly ConcurrentQueue<AppInfo> _fails = new();
    private readonly ICache _cache = new MemoryCache();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public AppClient()
    {
        Features = Features.Ping | Features.Notify | Features.CommandReply;
        SetActions("App/");

        PasswordProvider = new PasswordProvider();

        // 加载已保存数据
        var dic = LoadConsumeServicese();
        if (dic != null && dic.Count > 0)
        {
            foreach (var item in dic)
            {
                _consumes[item.Key] = item.Value;
            }
        }

        try
        {
            var executing = AssemblyX.Create(Assembly.GetExecutingAssembly());
            var asm = AssemblyX.Entry ?? executing;
            if (asm != null)
            {
                AppId ??= asm.Name;
                AppName = asm.Title;
                _version = asm.Version;
            }

            ClientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
        }
        catch { }

        Log = XTrace.Log;
    }

    /// <summary>实例化</summary>
    /// <param name="urls"></param>
    public AppClient(String urls) : this() => Server = urls;

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        foreach (var item in _publishServices)
        {
            //UnregisterAsync(item.Value).Wait();
            Unregister(item.Key);
        }
    }
    #endregion

    #region 方法
    /// <summary>开始客户端</summary>
    public void Start()
    {
        try
        {
            Code = AppId;

            if (AppId != "StarServer")
            {
                // 等待注册到平台
                var task = TaskEx.Run(Register);
                task.Wait(1_000);
            }
        }
        catch (Exception ex)
        {
            Log?.Error("注册失败：{0}", ex.GetTrue().Message);
        }

        StartTimer();
        Attach(this);
    }

    /// <summary>创建Http客户端</summary>
    /// <param name="urls"></param>
    /// <returns></returns>
    protected override ApiHttpClient CreateHttp(String urls)
    {
        var client = base.CreateHttp(urls);
        client.Filter = new TokenHttpFilter
        {
            UserName = AppId,
            Password = Secret,
            ClientId = ClientId,
        };

        return client;
    }

    /// <summary>注册</summary>
    /// <returns></returns>
    public async Task<Object?> Register()
    {
        try
        {
            var inf = new AppModel
            {
                AppId = AppId,
                AppName = AppName,
                ClientId = ClientId,
                Version = _version,
                NodeCode = NodeCode,
                IP = AgentInfo.GetIps()
            };

            var rs = await InvokeAsync<String>("App/Register", inf);
            WriteLog("接入星尘服务端：{0}", rs);

            Logined = true;

            return rs;
        }
        catch (Exception ex)
        {
            if (ex is HttpRequestException)
                Log?.Info("注册异常[{0}] {1}", (Client as ApiHttpClient)?.Source, ex.GetTrue()?.Message);
            else
                Log?.Info(ex.ToString());

            //throw;
            return null;
        }
    }
    #endregion

    #region 心跳
    /// <summary>构建心跳请求</summary>
    /// <returns></returns>
    public override IPingRequest BuildPingRequest()
    {
        var inf = _appInfo;
        if (inf == null)
            inf = _appInfo = new AppInfo(Process.GetCurrentProcess()) { Version = _version };
        else
            inf.Refresh();

        return inf;
    }

    /// <summary>向本地StarAgent发送心跳</summary>
    /// <returns></returns>
    public async Task<Object?> PingLocal()
    {
        if (WatchdogTimeout <= 0) return null;

        var local = Factory?.Local;
        if (local == null || local.Info == null) return null;

        if (_appInfo == null) return null;

        return await local.PingAsync(_appInfo, WatchdogTimeout);
    }

    /// <summary>心跳</summary>
    /// <param name="state"></param>
    /// <returns></returns>
    protected override async Task OnPing(Object state)
    {
        DefaultSpan.Current = null;
        using var span = Tracer?.NewSpan("AppPing");
        try
        {
            if (!Logined)
            {
                if (!NetworkInterface.GetIsNetworkAvailable()) return;

                var rs = await Register();
                if (rs == null) return;
            }

            // 向服务端发送心跳后，再向本地发送心跳
            await base.OnPing(state);
            await PingLocal();

            if (!NetworkInterface.GetIsNetworkAvailable()) return;

            await RefreshPublish();
            await RefreshConsume();
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            Log?.Debug("{0}", ex);
        }
    }
    #endregion

    #region 发布、消费
    /// <summary>发布服务（底层）。定时反复执行，让服务端更新注册信息</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    public async Task<ServiceModel?> RegisterAsync(PublishServiceInfo service)
    {
        AddService(service);

        // 如果没有设置地址，则不要调用接口
        if (service.Address.IsNullOrEmpty()) return null;

        return await InvokeAsync<ServiceModel>("App/RegisterService", service);
    }

    /// <summary>取消服务（底层）</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    public async Task<ServiceModel?> UnregisterAsync(PublishServiceInfo service)
    {
        _publishServices.TryRemove(service.ServiceName, out _);

        return await InvokeAsync<ServiceModel>("App/UnregisterService", service);
    }

    private void AddService(PublishServiceInfo service)
    {
        if (_publishServices.TryGetValue(service.ServiceName, out var svc) && svc == service) return;

        if (_publishServices.TryAdd(service.ServiceName, service))
        {
            WriteLog("注册服务 {0}", service.ToJson());

            StartTimer();
        }
        else
        {
            // 如果服务已注册，则更新，用于更新服务地址等信息
            _publishServices[service.ServiceName] = service;
        }
    }

    /// <summary>创建服务对象，用于自定义需要，再通过RegisterAsync发布到注册中心</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public PublishServiceInfo CreatePublishService(String serviceName)
    {
        var asmx = AssemblyX.Entry;
        var ip = AgentInfo.GetIps();

        var service = new PublishServiceInfo
        {
            ServiceName = serviceName,

            ClientId = ClientId,
            IP = ip,
            Version = asmx?.Version,
        };

        return service;
    }

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
    public async Task<PublishServiceInfo> RegisterAsync(String serviceName, String address, String? tag = null, String? health = null)
    {
        if (address == null) throw new ArgumentNullException(nameof(address));

        var service = CreatePublishService(serviceName);
        service.Address = address;
        service.ExternalAddress = NewLife.Setting.Current.ServiceAddress;
        service.Tag = tag;
        service.Health = health;

        var rs = await RegisterAsync(service);
        WriteLog("注册完成 {0}", rs?.ToJson());

        return service;
    }

    /// <summary>发布服务（延迟），直到回调函数返回地址信息才做真正发布</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="addressCallback">服务地址回调</param>
    /// <param name="tag">特性标签</param>
    /// <param name="health">健康监测接口地址</param>
    /// <returns></returns>
    public PublishServiceInfo Register(String serviceName, Func<String?> addressCallback, String? tag = null, String? health = null)
    {
        if (addressCallback == null) throw new ArgumentNullException(nameof(addressCallback));

        var service = CreatePublishService(serviceName);
        service.AddressCallback = addressCallback;
        service.ExternalAddress = NewLife.Setting.Current.ServiceAddress;
        service.Tag = tag;
        service.Health = health;

        AddService(service);

        return service;
    }

    /// <summary>取消服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public PublishServiceInfo? Unregister(String serviceName)
    {
        if (!_publishServices.TryGetValue(serviceName, out var service)) return null;
        if (service == null) return null;

        WriteLog("取消注册 {0}", service.ToJson());
        UnregisterAsync(service).Wait();

        return service;
    }

    /// <summary>消费服务（底层）</summary>
    /// <param name="service">应用服务</param>
    /// <returns></returns>
    public async Task<ServiceModel[]?> ResolveAsync(ConsumeServiceInfo service) => await InvokeAsync<ServiceModel[]>("App/ResolveService", service);

    /// <summary>消费得到服务地址信息</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="minVersion">最小版本</param>
    /// <param name="tag">特性标签。只要包含该特性的服务提供者</param>
    /// <returns></returns>
    public async Task<ServiceModel[]?> ResolveAsync(String serviceName, String? minVersion = null, String? tag = null)
    {
        var service = new ConsumeServiceInfo
        {
            ServiceName = serviceName,
            MinVersion = minVersion,
            Tag = tag,

            ClientId = ClientId,
        };

        // 已缓存数据的Tag可能不一致，需要重新消费
        if (!_consumeServices.TryGetValue(serviceName, out var svc) || svc.Tag + "" != tag + "")
        {
            WriteLog("消费服务 {0}", service.ToJson());

            StartTimer();

            // 消费即使报错，也要往下走，借助缓存
            try
            {
                var models = await ResolveAsync(service);
                if (models != null && models.Length > 0)
                {
                    _consumes[serviceName] = models;

                    SaveConsumeServices(_consumes);
                }

                // 缓存消费服务，避免频繁消费
                _consumeServices[serviceName] = service;

                return models;
            }
            catch (Exception ex)
            {
                WriteLog("消费服务[{0}]报错：{1}", serviceName, ex.Message);
            }
        }

        _consumeServices[serviceName] = service;

        if (_consumes.TryGetValue(serviceName, out var models2)) return models2;

        return null;
    }

    /// <summary>绑定消费服务名到指定事件，服务改变时通知外部</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="callback">回调方法</param>
    public void Bind(String serviceName, ServiceChangedCallback callback)
    {
        var list = _consumeEvents.GetOrAdd(serviceName, k => new List<Delegate>());
        list.Add(callback);

        StartTimer();
    }

    private async Task RefreshPublish()
    {
        // 刷新已发布服务
        foreach (var item in _publishServices)
        {
            var svc = item.Value;
            if (svc.Address.IsNullOrEmpty() && svc.AddressCallback != null)
            {
                var address = svc.AddressCallback();
                if (!address.IsNullOrEmpty()) svc.Address = address;
            }

            if (!svc.Address.IsNullOrEmpty()) await RegisterAsync(svc);
        }
    }

    private async Task RefreshConsume()
    {
        // 刷新已消费服务
        foreach (var item in _consumeServices)
        {
            var svc = item.Value;
            var ms = await ResolveAsync(svc);
            if (ms != null && ms.Length > 0)
            {
                _consumes[svc.ServiceName] = ms;

                // 需要判断，只有服务改变才调用相应事件
                if (_consumeEvents.TryGetValue(svc.ServiceName, out var list))
                {
                    foreach (var action in list)
                    {
                        (action as ServiceChangedCallback)?.Invoke(svc.ServiceName, ms);
                    }
                }

                SaveConsumeServices(_consumes);
            }
        }
    }

    IDictionary<String, ServiceModel[]>? LoadConsumeServicese()
    {
        var file = NewLife.Setting.Current.DataPath.CombinePath("star_services.json").GetBasePath();
        if (!File.Exists(file)) return null;

        try
        {
            var json = File.ReadAllText(file);
            return json.ToJsonEntity<IDictionary<String, ServiceModel[]>>();
        }
        catch { return null; }
    }

    void SaveConsumeServices(IDictionary<String, ServiceModel[]> models)
    {
        try
        {
            var file = NewLife.Setting.Current.DataPath.CombinePath("star_services.json").GetBasePath();
            file.EnsureDirectory(true);

            var json = models.ToJson(true);
            File.WriteAllText(file, json);
        }
        catch { }
    }
    #endregion

    #region 刷新
    /// <summary>附加刷新命令</summary>
    /// <param name="client"></param>
    public void Attach(ICommandClient client)
    {
        client.RegisterCommand("registry/register", DoRefresh);
        client.RegisterCommand("registry/unregister", DoRefresh);
    }

    private async Task<String?> DoRefresh(String? argument)
    {
        await RefreshConsume();

        return "刷新服务成功";
    }
    #endregion

    #region 辅助
    /// <summary>
    /// 设置服务地址
    /// </summary>
    /// <param name="serverAddress"></param>
    public void SetServerAddress(String serverAddress)
    {
        if (serverAddress == null) return;

        var set = NewLife.Setting.Current;
        if (serverAddress == set.ServiceAddress) return;

        WriteLog("设置服务地址为：{0}", serverAddress);

        var count = 0;
        foreach (var service in _publishServices.Values.ToArray())
        {
            if (service.Address.IsNullOrEmpty() || IsLocal(service.Address))
            {
                service.Address = serverAddress;
                count++;
            }
        }

        //if (count > 0 && _timer != null) _timer.SetNext(-1);

        set.ServiceAddress = serverAddress;
        set.Save();
    }

    /// <summary>
    /// 是否本地地址
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static Boolean IsLocal(String? address)
    {
        if (address.IsNullOrEmpty()) return false;

        if (address.Contains("://*")) return true;
        if (address.Contains("://+")) return true;
        if (address.Contains("://0.0.0.0")) return true;
        if (address.Contains("://[::]")) return true;

        return false;
    }
    #endregion
}