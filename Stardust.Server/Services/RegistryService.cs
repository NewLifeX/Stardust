using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.Models;
using XCode;

namespace Stardust.Server.Services;

public class RegistryService
{
    private readonly AppQueueService _queue;
    private readonly AppOnlineService _appOnline;
    private readonly IPasswordProvider _passwordProvider;
    private readonly AppSessionManager _sessionManager;
    private readonly ITracer _tracer;

    public RegistryService(AppQueueService queue, AppOnlineService appOnline, IPasswordProvider passwordProvider, AppSessionManager sessionManager, ITracer tracer)
    {
        _queue = queue;
        _appOnline = appOnline;
        _passwordProvider = passwordProvider;
        _sessionManager = sessionManager;
        _tracer = tracer;
    }

    /// <summary>应用鉴权</summary>
    /// <param name="app"></param>
    /// <param name="secret"></param>
    /// <param name="ip"></param>
    /// <param name="clientId"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public Boolean Auth(App app, String secret, String ip, String clientId)
    {
        if (app == null) return false;

        // 检查黑白名单
        if (!app.MatchIp(ip))
            throw new ApiException(403, $"应用[{app.Name}]禁止{ip}访问！");
        if (app.Project != null && !app.Project.MatchIp(ip))
            throw new ApiException(403, $"项目[{app.Project}]禁止{ip}访问！");

        // 检查应用有效性
        if (!app.Enable) throw new ApiException(403, $"应用[{app.Name}]已禁用！");

        // 未设置密钥，直接通过
        if (app.Secret.IsNullOrEmpty()) return true;
        if (app.Secret == secret) return true;
        if (secret.IsNullOrEmpty() || !_passwordProvider.Verify(app.Secret, secret))
        {
            app.WriteHistory("应用鉴权", false, "密钥校验失败", null, ip, clientId);
            return false;
        }

        return true;
    }

    /// <summary>应用注册</summary>
    /// <param name="name"></param>
    /// <param name="secret"></param>
    /// <param name="autoRegister"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public App Register(String name, String secret, Boolean autoRegister, String ip, String clientId)
    {
        // 查找应用
        var app = App.FindByName(name);
        // 查找或创建应用，避免多线程创建冲突
        app ??= App.GetOrAdd(name, App.FindByName, k => new App
        {
            Name = name,
            Secret = Rand.NextString(16),
            Enable = autoRegister,
        });

        app.WriteHistory("应用注册", true, $"[{app.Name}]注册成功", null, ip, clientId);

        return app;
    }

    public App Login(App app, AppModel model, String ip, StarServerSetting setting)
    {
        // 设置默认项目
        if (app.ProjectId == 0 || app.ProjectName == "默认")
        {
            var project = GalaxyProject.FindByName(model.Project);
            if (project != null)
                app.ProjectId = project.Id;
        }

        if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = model.AppName;

        app.LastLogin = DateTime.Now;
        app.LastIP = ip;
        app.UpdateIP = ip;
        app.Update();

        // 登录历史
        app.WriteHistory("应用鉴权", true, $"[{app.DisplayName}/{app.Name}]鉴权成功 " + model.ToJson(false, false, false), model.Version, ip, model.ClientId);

        return app;
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

        app.WriteHistory(nameof(Register), true, inf.ToJson(), inf.Version, ip, clientId);

        if (!inf.ClientId.IsNullOrEmpty()) clientId = inf.ClientId;

        // 更新在线记录
        var (online, _) = _appOnline.GetOnline(app, clientId, token, inf?.IP, ip);
        if (online != null)
        {
            // 关联节点，根据NodeCode匹配，如果未匹配上，则在未曾关联节点时才使用IP匹配
            var node = Node.FindByCode(inf.NodeCode);
            if (node == null && online.NodeId == 0) node = Node.SearchByIP(inf.IP).FirstOrDefault();
            if (node != null) online.NodeId = node.ID;

            if (!inf.Version.IsNullOrEmpty()) online.Version = inf.Version;
        }
        online.Update();

        return online;
    }

    public (AppService, Boolean changed) RegisterService(App app, Service service, PublishServiceInfo model, String ip)
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
        var olt = AppOnline.FindByClient(model.ClientId);
        if (olt != null) svc.NodeId = olt.NodeId;

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
                svc.Address = model.ExternalAddress;
            else
                svc.Address = urls.Take(10).Join(",");
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

            var http = _tracer.CreateHttpClient();
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

    private void WriteHistory(App app, String action, Boolean success, String remark, String ip, String clientId)
    {
        var olt = AppOnline.FindByClient(clientId);
        var version = olt?.Version;

        var hi = AppHistory.Create(app, action, success, remark, version, Environment.MachineName, ip);
        hi.Client = clientId;
        hi.Insert();
    }

    public AppOnline Ping(App app, AppInfo inf, String ip, String clientId, String token)
    {
        if (app == null) return null;

        // 更新在线记录
        var (online, _) = _appOnline.GetOnline(app, clientId, token, inf?.IP, ip);
        if (online != null)
        {
            //online.Version = app.Version;
            online.Fill(app, inf);
            online.SaveAsync();
        }

        //// 保存性能数据
        //AppMeter.WriteData(app, inf, "Ping", clientId, ip);

        return online;
    }

    private static Int32 _totalCommands;
    private static IList<AppCommand> _commands;
    private static DateTime _nextTime;

    public CommandModel[] AcquireAppCommands(Int32 appId)
    {
        // 缓存最近1000个未执行命令，用于快速过滤，避免大量节点在线时频繁查询命令表
        if (_nextTime < DateTime.Now || _totalCommands != AppCommand.Meta.Count)
        {
            _totalCommands = AppCommand.Meta.Count;
            _commands = AppCommand.AcquireCommands(-1, 1000);
            _nextTime = DateTime.Now.AddMinutes(1);
        }

        // 是否有本节点
        if (!_commands.Any(e => e.AppId == appId)) return null;

        using var span = _tracer?.NewSpan(nameof(AcquireAppCommands), new { appId });

        var cmds = AppCommand.AcquireCommands(appId, 100);
        if (cmds.Count == 0) return null;

        var rs = new List<CommandModel>();
        foreach (var item in cmds)
        {
            if (item.Times > 10 || item.Expire.Year > 2000 && item.Expire < DateTime.Now)
                item.Status = CommandStatus.取消;
            else
            {
                if (item.Status == CommandStatus.处理中 && item.UpdateTime.AddMinutes(10) < DateTime.Now) continue;

                item.Times++;
                item.Status = CommandStatus.处理中;
                rs.Add(item.ToModel());
            }
            item.UpdateTime = DateTime.Now;
        }
        cmds.Update(false);

        return rs.ToArray();
    }

    /// <summary>向应用发送命令</summary>
    /// <param name="app"></param>
    /// <param name="model"></param>
    /// <param name="user">创建者</param>
    /// <returns></returns>
    public async Task<AppCommand> SendCommand(App app, String clientId, CommandInModel model, String user)
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
        var cmdModel = cmd.ToModel();
        var ts = new List<Task>();
        foreach (var item in AppOnline.FindAllByApp(app.Id))
        {
            // 对特定实例发送
            if (!clientId.IsNullOrEmpty() && item.Client != clientId) continue;

            //_queue.Publish(app.Name, item.Client, cmdModel);
            var code = $"{app.Name}@{item.Client}";
            ts.Add(_sessionManager.PublishAsync(code, cmdModel, null, default));
        }
        Task.WaitAll(ts.ToArray());

        // 挂起等待。借助redis队列，等待响应
        if (model.Timeout > 0)
        {
            var q = _queue.GetReplyQueue(cmd.Id);
            var reply = await q.TakeOneAsync(model.Timeout);
            if (reply != null)
            {
                // 埋点
                using var span = _tracer?.NewSpan($"mq:AppCommandReply", reply);

                if (reply.Status == CommandStatus.错误)
                    throw new Exception($"命令错误！{reply.Data}");
                else if (reply.Status == CommandStatus.取消)
                    throw new Exception($"命令已取消！{reply.Data}");
            }
        }

        return cmd;
    }

    public async Task<AppCommand> SendCommand(App app, String clientId, String command, String argument, String user = null)
    {
        var model = new CommandInModel
        {
            Command = command,
            Argument = argument,
        };
        return await SendCommand(app, clientId, model, user);
    }

    public AppCommand CommandReply(App app, CommandReplyModel model)
    {
        var cmd = AppCommand.FindById((Int32)model.Id);
        if (cmd == null) return null;

        // 防止越权
        if (cmd.AppId != app.Id) throw new ApiException(403, $"[{app}]越权访问[{cmd.AppName}]的服务");

        cmd.Status = model.Status;
        cmd.Result = model.Data;
        cmd.Update();

        // 推入服务响应队列，让服务调用方得到响应
        _queue.Reply(model);

        return cmd;
    }

    /// <summary>通知该服务的所有消费者，服务信息有变更</summary>
    /// <param name="service"></param>
    /// <param name="command"></param>
    /// <param name="user"></param>
    public async Task NotifyConsumers(Service service, String command, String user = null)
    {
        var list = AppConsume.FindAllByService(service.Id);
        if (list.Count == 0) return;

        var appIds = list.Select(e => e.AppId).Distinct().ToArray();
        var arguments = new { service.Name, service.Address }.ToJson();

        using var span = _tracer?.NewSpan(nameof(NotifyConsumers), $"{command} appIds={appIds.Join()} user={user} arguments={arguments}");

        var ts = new List<Task>();
        foreach (var item in appIds)
        {
            var app = App.FindById(item);
            if (app != null) ts.Add(SendCommand(app, null, command, arguments, user));
        }

        await Task.WhenAll(ts);
    }
}