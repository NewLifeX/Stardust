using NewLife;
using NewLife.Log;
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
        private readonly ITracer _tracer;

        public RegistryService(AppQueueService queue, ITracer tracer)
        {
            _queue = queue;
            _tracer = tracer;
        }

        public AppOnline Register(App app, AppModel inf, String ip, String clientId, String token)
        {
            if (app == null) return null;

            if (app.DisplayName.IsNullOrEmpty()) app.DisplayName = inf.AppName;
            app.UpdateIP = ip;
            app.SaveAsync();

            if (!inf.ClientId.IsNullOrEmpty()) clientId = inf.ClientId;
            if (!clientId.IsNullOrEmpty())
            {
                var olt = GetOrAddOnline(app, inf.Version, ip, clientId, token);

                // 本地IP
                if (!inf.IP.IsNullOrEmpty())
                    olt.IP = inf.IP;
                else
                {
                    var p = clientId.IndexOf('@');
                    if (p > 0) olt.IP = clientId[..p];
                }

                // 关联节点
                var node = Node.FindByCode(inf.NodeCode);
                if (node == null) node = Node.FindAllByIPs(olt.IP).FirstOrDefault();
                if (node != null) olt.NodeId = node.ID;

                olt.SaveAsync();

                return olt;
            }

            return null;
        }

        public AppService RegisterService(App app, Service service, PublishServiceInfo model, String ip, out Boolean changed)
        {
            // 单例部署服务，每个节点只有一个实例，使用本地IP作为唯一标识，无需进程ID，减少应用服务关联数
            var clientId = model.ClientId;
            if (service.Singleton && !clientId.IsNullOrEmpty())
            {
                var p = clientId.IndexOf('@');
                if (p > 0) clientId = clientId[..p];
            }

            // 所有服务
            var services = AppService.FindAllByService(service.Id);
            changed = false;
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
                WriteHistory(app, "RegisterService", true, $"注册服务[{model.ServiceName}] {model.ClientId}", clientId);
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
            var olt = AppOnline.GetOrAddClient(model.ClientId);
            if (olt != null) svc.NodeId = olt.NodeId;

            // 作用域
            svc.Scope = AppRule.CheckScope(-1, ip, clientId);

            // 地址处理。本地任意地址，更换为IP地址
            var serverAddress = model.IP;
            if (serverAddress.IsNullOrEmpty()) serverAddress = clientId;
            if (serverAddress.IsNullOrEmpty()) serverAddress = ip;
            var addrs = model.Address
                ?.Replace("://*", $"://{serverAddress}")
                .Replace("://0.0.0.0", $"://{serverAddress}")
                .Replace("://[::]", $"://{serverAddress}");

            svc.Enable = app.AutoActive;
            svc.PingCount++;
            svc.Tag = model.Tag;
            svc.Version = model.Version;
            svc.Address = addrs;

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

            return svc;
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

        public AppService UnregisterService(App app, Service info, PublishServiceInfo model, String ip, out Boolean changed)
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
            changed = false;
            var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == clientId);
            if (svc != null)
            {
                //svc.Delete();
                svc.Enable = false;
                svc.Healthy = false;
                svc.Update();

                services.Remove(svc);

                changed = true;
                WriteHistory(app, "UnregisterService", true, $"服务[{model.ServiceName}]下线 {svc.Client}", svc.Client, ip);
            }

            info.Providers = services.Count;
            info.Save();

            return svc;
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
                    list.Add(new ServiceModel
                    {
                        ServiceName = item.ServiceName,
                        DisplayName = service.DisplayName,
                        Client = item.Client,
                        Version = item.Version,
                        Address = item.Address,
                        Scope = item.Scope,
                        Tag = item.Tag,
                        Weight = item.Weight,
                        CreateTime = item.CreateTime,
                        UpdateTime = item.UpdateTime,
                    });
                }
            }

            return list.ToArray();
        }

        private void WriteHistory(App app, String action, Boolean success, String remark, String clientId, String ip = null)
        {
            var hi = AppHistory.Create(app, action, success, remark, Environment.MachineName, ip);
            hi.Client = clientId;
            hi.SaveAsync();
        }

        public AppOnline GetOrAddOnline(App app, String version, String ip, String clientId, String token)
        {
            if (app == null) return null;

            if (clientId.IsNullOrEmpty()) return null;

            var olt = AppOnline.GetOrAddClient(clientId);
            olt.AppId = app.Id;
            olt.Name = app.ToString();
            olt.Category = app.Category;
            olt.Version = version;
            olt.Token = token;
            olt.PingCount++;
            if (olt.CreateIP.IsNullOrEmpty()) olt.CreateIP = ip;

            return olt;
        }

        public AppOnline Ping(App app, AppInfo inf, String ip, String clientId, String token)
        {
            if (app == null) return null;

            if (!clientId.IsNullOrEmpty())
            {
                var olt = GetOrAddOnline(app, inf.Version, ip, clientId, token);

                olt.Fill(app, inf);
                olt.SaveAsync();

                return olt;
            }

            return null;
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