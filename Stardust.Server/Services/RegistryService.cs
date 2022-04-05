using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Models;

namespace Stardust.Server.Services
{
    public class RegistryService
    {
        private readonly ICache _queue;

        public RegistryService(ICache queue) => _queue = queue;

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
            var cmdModel = cmd.ToModel().ToJson();
            foreach (var item in AppOnline.FindAllByApp(app.Id))
            {
                var topic = $"appcmd:{app.Name}:{item.Client}";
                var queue = _queue.GetQueue<String>(topic);
                queue.Add(cmdModel);

                // 设置过期时间，过期自动清理
                _queue.SetExpire(topic, TimeSpan.FromMinutes(30));
            }

            return cmd;
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

            return cmd;
        }
    }
}