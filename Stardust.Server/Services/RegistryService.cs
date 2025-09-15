using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.Models;
using XCode;
using Service = Stardust.Data.Service;

namespace Stardust.Server.Services;

public class RegistryService(AppQueueService queue, AppOnlineService appOnline, IPasswordProvider passwordProvider, AppSessionManager sessionManager, ICacheProvider cacheProvider, StarServerSetting setting, ITracer tracer, IServiceProvider serviceProvider) : DefaultDeviceService<Node, NodeOnline>(sessionManager, passwordProvider, cacheProvider, serviceProvider)
{
    #region 登录注销
    public override ILoginResponse Login(DeviceContext context, ILoginRequest request, String source)
    {
        var rs = base.Login(context, request, source);

        var inf = request as AppModel;
        if (context.Online is AppOnline online)
        {
            // 关联节点，根据NodeCode匹配，如果未匹配上，则在未曾关联节点时才使用IP匹配
            var node = Node.FindByCode(inf.NodeCode);
            if (node == null && online.NodeId == 0) node = Node.SearchByIP(inf.IP).FirstOrDefault();
            if (node != null) online.NodeId = node.ID;

            if (!inf.Version.IsNullOrEmpty()) online.Version = inf.Version;
            var compile = inf.Compile.ToDateTime().ToLocalTime();
            if (compile.Year > 2000) online.Compile = compile;
        }

        return rs;
    }

    /// <summary>验证设备合法性</summary>
    public override Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is not App app) return false;

        var ip = context.UserHost;
        var secret = request.Secret;

        // 检查黑白名单
        if (!app.MatchIp(ip))
            throw new ApiException(ApiCode.Forbidden, $"应用[{app.Name}]禁止{ip}访问！");
        if (app.Project != null && !app.Project.MatchIp(ip))
            throw new ApiException(ApiCode.Forbidden, $"项目[{app.Project}]禁止{ip}访问！");

        // 检查应用有效性
        if (!app.Enable) throw new ApiException(ApiCode.Forbidden, $"应用[{app.Name}]已禁用！");

        // 未设置密钥，直接通过
        if (app.Secret.IsNullOrEmpty()) return true;
        if (app.Secret == secret) return true;

        if (setting.SaltTime > 0 && passwordProvider is SaltPasswordProvider saltProvider)
        {
            // 使用盐值偏差时间，允许客户端时间与服务端时间有一定偏差
            saltProvider.SaltTime = setting.SaltTime;
        }
        if (secret.IsNullOrEmpty() || !passwordProvider.Verify(app.Secret, secret))
        {
            app.WriteHistory("应用鉴权", false, "密钥校验失败", null, ip, context.ClientId);
            return false;
        }

        return true;
    }

    /// <summary>自动注册</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public override IDeviceModel Register(DeviceContext context, ILoginRequest request)
    {
        var name = request.Code;

        // 查找应用
        var app = App.FindByName(name);
        // 查找或创建应用，避免多线程创建冲突
        app ??= App.GetOrAdd(name, App.FindByName, k => new App
        {
            Name = name,
            Secret = Rand.NextString(16),
            Enable = setting.AppAutoRegister,
        });

        app.WriteHistory("应用注册", true, $"[{app.Name}]注册成功", null, context.UserHost, context.ClientId);
        context.Device = app;

        return app;
    }

    /// <summary>登录中</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    public override void OnLogin(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is not App app) return;

        var model = request as AppModel;
        var ip = context.UserHost;

        // 设置默认项目
        if (app.ProjectId == 0 || app.ProjectName == "默认")
        {
            var project = GalaxyProject.FindByName(model.Project);
            if (project != null)
                app.ProjectId = project.Id;
        }

        if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = model.AppName;

        // 比较编译时间，只要最新的
        var compile = model.Compile.ToDateTime().ToLocalTime();
        if (app.Compile < compile)
        {
            app.Compile = compile;
            app.Version = model.Version;
        }

        if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = model.AppName;
        app.LastLogin = DateTime.Now;
        app.LastIP = ip;
        app.UpdateIP = ip;
        app.Update();

        context.Online = GetOnline(context) ?? CreateOnline(context);

        // 登录历史
        WriteHistory(context, "应用鉴权", true, $"[{app.DisplayName}/{app.Name}]鉴权成功 " + model.ToJson(false, false, false));
    }

    /// <summary>注销</summary>
    /// <param name="reason">注销原因</param>
    /// <param name="ip">IP地址</param>
    /// <returns></returns>
    public override IOnlineModel Logout(DeviceContext context, String reason, String source)
    {
        //var online = appOnline.GetOnline(context.ClientId);
        //if (online == null) return null;

        //var app = context.Device as App;
        //var msg = $"{reason} [{app}]]登录于{online.CreateTime}，最后活跃于{online.UpdateTime}";
        //app.WriteHistory("应用下线", true, msg, context.UserHost);

        ////!! 应用注销，不删除在线记录，保留在线记录用于查询
        ////online.Delete();

        //appOnline.RemoveOnline(context.ClientId);

        var online = base.Logout(context, reason, source);
        if (online is AppOnline online2)
        {
            appOnline.RemoveOnline(context.ClientId);
        }

        return online;
    }

    /// <summary>激活应用。更新在线信息和关联节点</summary>
    /// <param name="app"></param>
    /// <param name="inf"></param>
    /// <param name="ip"></param>
    /// <param name="clientId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public AppOnline SetOnline(App app, AppModel inf, String ip, String clientId, String token)
    {
        if (app == null) return null;

        if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = inf.AppName;
        app.UpdateIP = ip;
        app.Update();

        if (!inf.ClientId.IsNullOrEmpty()) clientId = inf.ClientId;

        // 更新在线记录
        var (online, _) = appOnline.GetOnline(app, clientId, token, inf?.IP, ip);
        if (online != null)
        {
            // 关联节点，根据NodeCode匹配，如果未匹配上，则在未曾关联节点时才使用IP匹配
            var node = Node.FindByCode(inf.NodeCode);
            if (node == null && online.NodeId == 0) node = Node.SearchByIP(inf.IP).FirstOrDefault();
            if (node != null) online.NodeId = node.ID;

            if (!inf.Version.IsNullOrEmpty()) online.Version = inf.Version;
            var compile = inf.Compile.ToDateTime().ToLocalTime();
            if (compile.Year > 2000) online.Compile = compile;
        }
        online.Update();

        return online;
    }
    #endregion

    #region 服务注册
    public (AppService, Boolean changed) RegisterService(App app, Service service, PublishServiceInfo model, AppOnline online, String ip)
    {
        var clientId = model.ClientId;
        var localIp = clientId;
        if (!localIp.IsNullOrEmpty())
        {
            var p = localIp.IndexOf('@');
            if (p > 0) localIp = localIp[..p];
        }

        // 单例部署服务，每个节点只有一个实例，使用本地IP作为唯一标识，无需进程ID，减少应用服务关联数
        if (service.Singleton) clientId = localIp;

        // 所有服务
        var services = AppService.FindAllByService(service.Id);
        var changed = false;
        var svc = services.FirstOrDefault(e => e.AppId == app.Id && (e.Client == clientId || service.Singleton && !localIp.IsNullOrEmpty() && e.Client.StartsWith($"{localIp}@")));
        if (svc == null)
        {
            svc = new AppService
            {
                AppId = app.Id,
                ServiceId = service.Id,
                ServiceName = model.ServiceName,
                Client = clientId,

                CreateIP = ip,
            };
            services.Add(svc);

            changed = true;
            WriteHistory(app, "RegisterService", true, $"注册服务[{model.ServiceName}] {model.ClientId}", ip, clientId);
        }
        else
        {
            if (!svc.Enable)
            {
                svc.Enable = app.AutoActive;

                if (svc.Enable) changed = true;
            }
        }

        // 节点信息
        //var online = AppOnline.FindByClient(model.ClientId);
        if (online != null) svc.NodeId = online.NodeId;

        // 作用域
        if (service.UseScope)
            svc.Scope = AppRule.CheckScope(-1, ip, localIp);

        svc.Enable = app.AutoActive;
        svc.PingCount++;
        svc.Tag = model.Tag;
        svc.Version = model.Version;
        svc.OriginAddress = model.Address;

        // 地址处理。本地任意地址，更换为IP地址
        var serverAddress = "";
        if (service.Extranet && ip != "127.0.0.1" && ip != "::1")
        {
            serverAddress = ip;
        }
        else
        {
            serverAddress = model.IP;
            if (serverAddress.IsNullOrEmpty()) serverAddress = localIp;
            if (serverAddress.IsNullOrEmpty()) serverAddress = ip;
        }

        var urls = new List<String>();
        foreach (var item in serverAddress.Split(","))
        {
            var addrs = model.Address
                ?.Replace("://*", $"://{item}")
                ?.Replace("://+", $"://{item}")
                .Replace("://0.0.0.0", $"://{item}")
                .Replace("://[::]", $"://{item}")
                .Split(",");
            if (addrs != null)
            {
                foreach (var elm in addrs)
                {
                    var url = elm;
                    if (url.StartsWithIgnoreCase("http://", "https://")) url = new Uri(url).ToString().TrimEnd('/');
                    if (!urls.Contains(url)) urls.Add(url);
                }
            }
        }

        if (service.Address.IsNullOrEmpty())
        {
            if (!model.ExternalAddress.IsNullOrEmpty())
                svc.Address = model.ExternalAddress?.Split(',').Take(5).Join(",");
            else
                svc.Address = urls.Take(5).Join(",");
        }
        else
        {
            // 地址模版
            var addr = service.Address.Replace("{LocalIP}", localIp).Replace("{IP}", ip);
            if (addr.Contains("{Port}"))
            {
                var port = 0;
                foreach (var item in urls)
                {
                    var p = item.IndexOf(":", "http://".Length);
                    if (p >= 0)
                    {
                        port = item[(p + 1)..].TrimEnd('/').ToInt();
                        if (port > 0) break;
                    }
                }
                if (port > 0) addr = addr.Replace("{Port}", port + "");
            }
            svc.Address = addr;
        }

        // 无需健康监测，直接标记为健康
        if (!model.Health.IsNullOrEmpty()) service.HealthAddress = model.Health;
        if (!service.HealthCheck || service.HealthAddress.IsNullOrEmpty()) svc.Healthy = true;

        svc.Save();

        service.Providers = services.Count;
        service.Save();

        // 如果有改变，异步监测健康状况
        if (changed && service.HealthCheck && !service.HealthAddress.IsNullOrEmpty())
        {
            _ = Task.Run(() => HealthCheck(svc));
        }

        return (svc, changed);
    }

    public async Task HealthCheck(AppService svc)
    {
        var url = svc.Service?.HealthAddress;
        if (url.IsNullOrEmpty()) return;

        try
        {
            if (!url.StartsWithIgnoreCase("http://", "https://"))
            {
                var ss = svc.Address.Split(',');
                var uri = new Uri(new Uri(ss[0]), url);
                url = uri.ToString();
            }

            var http = tracer.CreateHttpClient();
            var rs = await http.GetStringAsync(url);

            svc.Healthy = true;
            svc.CheckResult = rs;
        }
        catch (Exception ex)
        {
            svc.Healthy = false;
            svc.CheckResult = ex.ToString();

            XTrace.WriteLine("HealthCheck: {0}", url);
            XTrace.Log.Error(ex.Message);
        }

        svc.CheckTimes++;
        svc.LastCheck = DateTime.Now;
        svc.Update();
    }

    public (AppService, Boolean changed) UnregisterService(App app, Service info, PublishServiceInfo model, String ip)
    {
        // 单例部署服务，每个节点只有一个实例，使用本地IP作为唯一标识，无需进程ID，减少应用服务关联数
        var clientId = model.ClientId;
        if (info.Singleton && !clientId.IsNullOrEmpty())
        {
            var p = clientId.IndexOf('@');
            if (p > 0) clientId = clientId[..p];
        }

        // 所有服务
        var services = AppService.FindAllByService(info.Id);
        var changed = false;
        var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == clientId);
        if (svc != null)
        {
            //svc.Delete();
            svc.Enable = false;
            svc.Healthy = false;
            svc.Update();

            services.Remove(svc);

            changed = true;
            WriteHistory(app, "UnregisterService", true, $"服务[{model.ServiceName}]下线 {svc.Client}", ip, svc.Client);
        }

        info.Providers = services.Count;
        info.Save();

        return (svc, changed);
    }

    public ServiceModel[] ResolveService(Service service, ConsumeServiceInfo model, String scope)
    {
        var list = new List<ServiceModel>();
        var tags = model.Tag?.Split(",");

        // 该服务所有生产
        var services = AppService.FindAllByService(service.Id);
        foreach (var item in services)
        {
            // 启用，匹配规则，健康
            if (item.Enable && item.Healthy && item.Match(model.MinVersion, scope, tags))
            {
                list.Add(item.ToModel());
            }
        }

        return list.ToArray();
    }
    #endregion

    #region 心跳保活
    //public override IOnlineModel OnPing(DeviceContext context, IPingRequest request)
    //{
    //    if (context.Device is not App app) return null;

    //    // 更新在线记录
    //    var inf = request as AppInfo;
    //    //var (online, _) = appOnline.GetOnline(app, context.ClientId, context.Token, inf?.IP, context.UserHost);
    //    var online = base.OnPing(context, request) as AppOnline;
    //    if (online != null)
    //    {
    //        //online.Version = app.Version;
    //        online.Fill(app, inf);
    //        online.SaveAsync();
    //    }

    //    //// 保存性能数据
    //    //AppMeter.WriteData(app, inf, "Ping", clientId, ip);

    //    return online;
    //}

    private static Int32 _totalCommands;
    private static IList<AppCommand> _commands;
    private static DateTime _nextTime;

    public override CommandModel[] AcquireCommands(DeviceContext context)
    {
        // 缓存最近1000个未执行命令，用于快速过滤，避免大量节点在线时频繁查询命令表
        if (_nextTime < DateTime.Now || _totalCommands != AppCommand.Meta.Count)
        {
            _totalCommands = AppCommand.Meta.Count;
            _commands = AppCommand.AcquireCommands(-1, 1000);
            _nextTime = DateTime.Now.AddMinutes(1);
        }

        if (context.Device is not App app) return null;
        var appId = app.Id;

        // 是否有本节点
        if (!_commands.Any(e => e.AppId == appId)) return null;

        using var span = tracer?.NewSpan(nameof(AcquireCommands), new { appId });

        var cmds = AppCommand.AcquireCommands(appId, 100);
        if (cmds.Count == 0) return null;

        var rs = new List<CommandModel>();
        foreach (var item in cmds)
        {
            // 命令要提前下发，在客户端本地做延迟处理，这里不应该过滤掉
            //// 命令是否已经开始
            //if (item.StartTime > DateTime.Now) continue;

            // 带有过期时间的命令，加大重试次数
            var maxTimes = item.Expire.Year > 2000 ? 100 : 10;
            if (item.Times > maxTimes || item.Expire.Year > 2000 && item.Expire < DateTime.Now)
                item.Status = CommandStatus.取消;
            else
            {
                // 如果命令正在处理中，则短期内不重复下发
                if (item.Status == CommandStatus.处理中 && item.UpdateTime.AddSeconds(30) > DateTime.Now) continue;

                // 即时指令，或者已到开始时间的未来指令，才增加次数
                if (item.StartTime.Year < 2000 || item.StartTime < DateTime.Now)
                    item.Times++;
                item.Status = CommandStatus.处理中;

                var commandModel = BuildCommand(item.App, item);

                rs.Add(commandModel);
            }
            item.UpdateTime = DateTime.Now;
        }
        cmds.Update(false);

        return rs.ToArray();
    }

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online"></param>
    /// <returns></returns>
    public override void SetOnline(DeviceContext context, Boolean online)
    {
        if ((GetOnline(context) ?? context.Online) is AppOnline olt)
        {
            olt.WebSocket = online;
            olt.Update();
        }
    }
    #endregion

    #region 下行通知
    /// <summary>向应用发送命令</summary>
    /// <param name="app">应用</param>
    /// <param name="clientId">应用实例标识。向特定应用实例发送命令时指定</param>
    /// <param name="model">命令模型</param>
    /// <param name="user">创建者</param>
    /// <returns></returns>
    public async Task<CommandReplyModel> SendCommand(App app, String clientId, CommandInModel model, String user, CancellationToken cancellationToken = default)
    {
        //if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定应用");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var cmd = new AppCommand
        {
            AppId = app.Id,
            Command = model.Command,
            Argument = model.Argument,
            //Expire = model.Expire,
            TraceId = DefaultSpan.Current?.TraceId,

            CreateUser = user,
        };
        if (model.StartTime > 0) cmd.StartTime = DateTime.Now.AddSeconds(model.StartTime);
        if (model.Expire > 0) cmd.Expire = DateTime.Now.AddSeconds(model.Expire);
        cmd.Insert();

        // 分发命令给该应用的所有实例
        var cmdModel = BuildCommand(app, cmd);
        var ts = new List<Task>();
        foreach (var item in AppOnline.FindAllByApp(app.Id))
        {
            // 对特定实例发送
            if (!clientId.IsNullOrEmpty() && item.Client != clientId) continue;

            //_queue.Publish(app.Name, item.Client, cmdModel);
            var code = $"{app.Name}@{item.Client}";
            ts.Add(sessionManager.PublishAsync(code, cmdModel, null, cancellationToken));
        }
        await Task.WhenAll(ts);

        // 挂起等待。借助redis队列，等待响应
        if (model.Timeout > 0)
        {
            var q = queue.GetReplyQueue(cmd.Id);
            var reply = await q.TakeOneAsync(model.Timeout, cancellationToken);
            if (reply != null)
            {
                // 埋点
                using var span = tracer?.NewSpan($"mq:AppCommandReply", reply);

                if (reply.Status == CommandStatus.错误)
                    throw new Exception($"命令错误！{reply.Data}");
                else if (reply.Status == CommandStatus.取消)
                    throw new Exception($"命令已取消！{reply.Data}");

                return reply;
            }
        }

        return null;
    }

    public override Task<CommandReplyModel> SendCommand(DeviceContext context, CommandInModel model, CancellationToken cancellationToken = default)
    {
        if (context.Device is not App app) return null;

        return SendCommand(app, null, model, null);
    }

    public override Int32 CommandReply(DeviceContext context, CommandReplyModel model)
    {
        var app = context.Device as App;
        var cmd = AppCommand.FindById((Int32)model.Id);
        if (cmd == null) return 0;

        // 防止越权
        if (cmd.AppId != app.Id) throw new ApiException(ApiCode.Forbidden, $"[{app}]越权访问[{cmd.AppName}]的服务");

        cmd.Status = model.Status;
        cmd.Result = model.Data;
        cmd.Update();

        // 推入服务响应队列，让服务调用方得到响应
        var topic = $"appreply:{model.Id}";
        var q = cacheProvider.GetQueue<CommandReplyModel>(topic);
        q.Add(model);

        // 设置过期时间，过期自动清理
        cacheProvider.Cache.SetExpire(topic, TimeSpan.FromSeconds(60));

        return 1;
    }

    /// <summary>通知该服务的所有消费者，服务信息有变更</summary>
    /// <param name="service"></param>
    /// <param name="command"></param>
    /// <param name="user"></param>
    public async Task NotifyConsumers(AppService service, String command, String user = null)
    {
        var list = AppConsume.FindAllByService(service.ServiceId);
        if (list.Count == 0) return;

        // 获取所有订阅该服务的应用，可能相同应用多实例订阅，需要去重
        var appIds = list.Select(e => e.AppId).Distinct().ToArray();
        var arguments = new { service.AppName, service.ServiceName, service.Address }.ToJson();

        using var span = tracer?.NewSpan(nameof(NotifyConsumers), $"{command} appIds={appIds.Join()} user={user} arguments={arguments}");

        var ts = new List<Task>();
        foreach (var item in appIds)
        {
            var app = App.FindById(item);
            if (app != null)
            {
                var model = new CommandInModel
                {
                    Command = command,
                    Argument = arguments,
                    Expire = 600,
                };
                ts.Add(SendCommand(app, null, model, user));
            }
        }

        await Task.WhenAll(ts);
    }

    //private static Version _version = new(3, 1, 2025, 0103);
    private CommandModel BuildCommand(App app, AppCommand cmd)
    {
        var model = cmd.ToModel();
        model.TraceId = DefaultSpan.Current + "";

        // 新版本使用UTC时间
        if (app.Compile.Year >= 2025)
        {
            if (model.StartTime.Year > 2000)
                model.StartTime = model.StartTime.ToUniversalTime();
            if (model.Expire.Year > 2000)
                model.Expire = model.Expire.ToUniversalTime();
        }

        return model;
    }
    #endregion

    #region 事件上报
    //public Int32 PostEvents(DeviceContext context, EventModel[] events)
    //{
    //    var app = context.Device as App;
    //    var ip = context.UserHost;
    //    var olt = AppOnline.FindByClient(clientId);
    //    var his = new List<AppHistory>();
    //    foreach (var model in events)
    //    {
    //        //WriteHistory(model.Name, !model.Type.EqualIgnoreCase("error"), model.Time.ToDateTime().ToLocalTime(), model.Remark, null);
    //        var success = !model.Type.EqualIgnoreCase("error");
    //        var time = model.Time.ToDateTime().ToLocalTime();
    //        var hi = AppHistory.Create(app, model.Name, success, model.Remark, olt?.Version, Environment.MachineName, ip);
    //        hi.Client = clientId;
    //        if (time.Year > 2000) hi.CreateTime = time;
    //        his.Add(hi);
    //    }

    //    his.Insert();

    //    return events.Length;
    //}

    protected override IEntity CreateEvent(DeviceContext context, IDeviceModel2 device, EventModel model)
    {
        var entity = base.CreateEvent(context, device, model);
        if (entity is AppHistory history)
        {
            var online = GetOnline(context) as AppOnline;
            history.Version = online?.Version;
            history.Client = online?.Client;

            var time = model.Time.ToDateTime().ToLocalTime();
            if (time.Year > 2000) history.CreateTime = time;
        }

        return entity;
    }
    #endregion

    #region 辅助
    public override IDeviceModel QueryDevice(String code) => App.FindByName(code);

    public override IOnlineModel QueryOnline(String sessionId) => AppOnline.FindBySessionId(sessionId, true);

    protected override String GetSessionId(DeviceContext context) => context.ClientId ?? base.GetSessionId(context);

    private void WriteHistory(App app, String action, Boolean success, String remark, String ip, String clientId)
    {
        var online = AppOnline.FindBySessionId(clientId, true);
        var version = online?.Version;

        var history = AppHistory.Create(app, action, success, remark, version, Environment.MachineName, ip);
        history.Client = clientId;
        history.Insert();
    }

    /// <summary>写设备历史</summary>
    /// <param name="context">上下文</param>
    /// <param name="action">动作</param>
    /// <param name="success">成功</param>
    /// <param name="remark">备注内容</param>
    public override void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        var version = (context.Online as AppOnline)?.Version;
        var history = AppHistory.Create(context.Device as App, action, success, remark, version, Environment.MachineName, context.UserHost);
        history.Client = context.ClientId;
        history.Insert();
    }
    #endregion
}