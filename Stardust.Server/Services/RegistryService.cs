using NewLife;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Models;

namespace Stardust.Server.Services
{
    public class RegistryService
    {
        private readonly AppQueueService _queue;
        private readonly AppOnlineService _appOnline;
        private readonly ITracer _tracer;

        public RegistryService(AppQueueService queue, AppOnlineService appOnline, ITracer tracer)
        {
            _queue = queue;
            _appOnline = appOnline;
            _tracer = tracer;
        }

        public AppOnline Register(App app, AppModel inf, String ip, String clientId, String token)
        {
            if (app == null) return null;

            if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = inf.AppName;
            app.UpdateIP = ip;
            app.SaveAsync();

            app.WriteHistory("Register", true, inf.ToJson(), inf.Version, ip, clientId);

            if (!inf.ClientId.IsNullOrEmpty()) clientId = inf.ClientId;

            // 更新在线记录
            var (online, _) = _appOnline.GetOnline(app, clientId, token, inf?.IP, ip);
            if (online != null)
            {
                // 关联节点，根据NodeCode匹配，如果未匹配上，则在未曾关联节点时才使用IP匹配
                var node = Node.FindByCode(inf.NodeCode);
                if (node == null && online.NodeId == 0) node = Node.SearchByIP(inf.IP).FirstOrDefault();
                if (node != null) online.NodeId = node.ID;

                online.Version = inf.Version;
                online.SaveAsync();
            }

            // 根据节点IP规则，自动创建节点
            if (online.NodeId == 0)
            {
                var node = GetOrAddNode(inf, online.IP, ip);
                if (node != null)
                {
                    online.NodeId = node.ID;
                    online.SaveAsync();
                }
            }

            return online;
        }

        public Node GetOrAddNode(AppModel inf, String localIp, String ip)
        {
            // 根据节点IP规则，自动创建节点
            var rule = NodeResolver.Instance.Match(null, localIp);
            if (rule != null && rule.NewNode)
            {
                using var span = _tracer?.NewSpan("AddNodeForApp", rule);

                var nodes = Node.SearchByIP(localIp);
                if (nodes.Count == 0)
                {
                    var node = new Node
                    {
                        Code = Rand.NextString(8),
                        Name = rule.Name,
                        ProductCode = "App",
                        Category = rule.Category,
                        IP = localIp,
                        Version = inf?.Version,
                        Enable = true,
                    };
                    if (node.Name.IsNullOrEmpty()) node.Name = inf?.AppName;
                    if (node.Name.IsNullOrEmpty()) node.Name = node.Code;
                    node.Insert();

                    node.WriteHistory("AppAddNode", true, inf.ToJson(), ip);

                    return node;
                }
            }

            return null;
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
            var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == clientId);
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
            svc.Scope = AppRule.CheckScope(-1, ip, localIp);

            svc.Enable = app.AutoActive;
            svc.PingCount++;
            svc.Tag = model.Tag;
            svc.Version = model.Version;

            // 地址处理。本地任意地址，更换为IP地址
            var serverAddress = model.IP;
            if (serverAddress.IsNullOrEmpty()) serverAddress = localIp;
            if (serverAddress.IsNullOrEmpty()) serverAddress = ip;

            var ds = new List<String>();
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
                        if (!ds.Contains(elm)) ds.Add(elm);
                    }
                }
            }

            if (service.Address.IsNullOrEmpty())
            {
                svc.Address = ds.Join(",");
            }
            else
            {
                // 地址模版
                var addr = service.Address.Replace("{IP}", ip);
                if (addr.Contains("{Port}"))
                {
                    var port = 0;
                    foreach (var item in ds)
                    {
                        var p = item.IndexOf(":", "http://".Length);
                        if (p >= 0)
                        {
                            port = item[p..].TrimEnd('/').ToInt();
                            if (port > 0) break;
                        }
                    }
                    addr = addr.Replace("{Port}", port + "");
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
            hi.SaveAsync();
        }

        public AppOnline Ping(App app, AppInfo inf, String ip, String clientId, String token)
        {
            if (app == null) return null;

            // 更新在线记录
            var (online, _) = _appOnline.GetOnline(app, clientId, token, inf?.IP, ip);
            if (online != null)
            {
                online.Version = app.Version;
                online.Fill(app, inf);
                online.SaveAsync();
            }

            return online;
        }

        /// <summary>向应用发送命令</summary>
        /// <param name="app"></param>
        /// <param name="model"></param>
        /// <param name="user">创建者</param>
        /// <returns></returns>
        public AppCommand SendCommand(App app, CommandInModel model, String user)
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
            if (model.Expire > 0) cmd.Expire = DateTime.Now.AddSeconds(model.Expire);
            cmd.Insert();

            // 分发命令给该应用的所有实例
            var cmdModel = cmd.ToModel();
            foreach (var item in AppOnline.FindAllByApp(app.Id))
            {
                _queue.Publish(app.Name, item.Client, cmdModel);
            }

            return cmd;
        }

        public AppCommand SendCommand(App app, String command, String argument, String user = null)
        {
            var model = new CommandInModel
            {
                Command = command,
                Argument = argument,
            };
            return SendCommand(app, model, user);
        }

        public AppCommand CommandReply(App app, CommandReplyModel model)
        {
            var cmd = AppCommand.FindById(model.Id);
            if (cmd == null) return null;

            // 防止越权
            if (cmd.AppId != app.Id) throw new InvalidOperationException($"[{app}]越权访问[{cmd.AppName}]的服务");

            cmd.Status = model.Status;
            cmd.Result = model.Data;
            cmd.Update();

            // 推入服务响应队列，让服务调用方得到响应
            _queue.Publish(model);

            return cmd;
        }

        /// <summary>通知该服务的所有消费者，服务信息有变更</summary>
        /// <param name="service"></param>
        /// <param name="command"></param>
        /// <param name="user"></param>
        public void NotifyConsumers(Service service, String command, String user = null)
        {
            var list = AppConsume.FindAllByService(service.Id);
            if (list.Count == 0) return;

            var appIds = list.Select(e => e.AppId).Distinct().ToArray();

            using var span = _tracer?.NewSpan(nameof(NotifyConsumers), $"{command} appIds={appIds.Join()} user={user}");

            foreach (var item in appIds)
            {
                var app = App.FindById(item);
                if (app != null) SendCommand(app, command, null, user);
            }
        }
    }
}