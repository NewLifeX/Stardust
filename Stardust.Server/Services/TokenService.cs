using System.Reflection;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services
{
    /// <summary>应用服务</summary>
    public class TokenService
    {
        /// <summary>验证应用密码，不存在时新增</summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="autoRegister"></param>
        /// <returns></returns>
        public App Authorize(String username, String password, Boolean autoRegister)
        {
            if (username.IsNullOrEmpty()) throw new ArgumentNullException(nameof(username));
            //if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

            // 查找应用
            var app = App.FindByName(username);
            if (app == null)
            {
                app = new App
                {
                    Name = username,
                    Secret = password,
                    Enable = autoRegister,
                };

                // 先保存
                app.Insert();

                //if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(username), $"应用[{username}]不存在且禁止自动注册！");
            }

            // 检查应用有效性
            if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(username), $"应用[{username}]已禁用！");
            if (!app.Secret.IsNullOrEmpty() && password != app.Secret) throw new InvalidOperationException($"非法访问应用[{username}]！");

            return app;
        }

        /// <summary>颁发令牌</summary>
        /// <param name="name"></param>
        /// <param name="secret"></param>
        /// <param name="expire"></param>
        /// <returns></returns>
        public TokenModel IssueToken(String name, String secret, Int32 expire, String id = null)
        {
            if (id.IsNullOrEmpty()) id = Rand.NextString(8);

            // 颁发令牌
            var ss = secret.Split(':');
            var jwt = new JwtBuilder
            {
                Issuer = Assembly.GetEntryAssembly().GetName().Name,
                Subject = name,
                Id = id,
                Expire = DateTime.Now.AddSeconds(expire),

                Algorithm = ss[0],
                Secret = ss[1],
            };

            return new TokenModel
            {
                AccessToken = jwt.Encode(null),
                TokenType = jwt.Type ?? "JWT",
                ExpireIn = expire,
                RefreshToken = jwt.Encode(null),
            };
        }

        /// <summary>解码令牌</summary>
        /// <param name="token"></param>
        /// <param name="tokenSecret"></param>
        /// <returns></returns>
        public (JwtBuilder, Exception) DecodeTokenWithError(String token, String tokenSecret)
        {
            if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

            // 解码令牌
            var ss = tokenSecret.Split(':');
            var jwt = new JwtBuilder
            {
                Algorithm = ss[0],
                Secret = ss[1],
            };

            Exception ex = null;
            if (!jwt.TryDecode(token, out var message)) ex = new ApiException(403, $"非法访问 {message}");

            return (jwt, ex);
        }

        /// <summary>解码令牌</summary>
        /// <param name="token"></param>
        /// <param name="tokenSecret"></param>
        /// <returns></returns>
        public (JwtBuilder, App) DecodeToken(String token, String tokenSecret)
        {
            if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

            // 解码令牌
            var ss = tokenSecret.Split(':');
            var jwt = new JwtBuilder
            {
                Algorithm = ss[0],
                Secret = ss[1],
            };
            if (!jwt.TryDecode(token, out var message)) throw new ApiException(403, $"非法访问 {message}");

            // 验证应用
            var app = App.FindByName(jwt.Subject);
            if (app == null)
            {
                // 可能是StarAgent混用了token
                var node = Data.Nodes.Node.FindByCode(jwt.Subject);
                if (node == null) throw new ApiException(403, $"无效应用[{jwt.Subject}]");

                app = new App { Name = node.Code, DisplayName = node.Name, Enable = true };
            }
            if (!app.Enable) throw new InvalidOperationException($"已停用应用[{jwt.Subject}]");

            return (jwt, app);
        }

        /// <summary>解码令牌</summary>
        /// <param name="token"></param>
        /// <param name="tokenSecret"></param>
        /// <returns></returns>
        public (App, Exception) TryDecodeToken(String token, String tokenSecret)
        {
            if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

            // 解码令牌
            var ss = tokenSecret.Split(':');
            var jwt = new JwtBuilder
            {
                Algorithm = ss[0],
                Secret = ss[1],
            };

            Exception ex = null;
            if (!jwt.TryDecode(token, out var message)) ex = new ApiException(403, $"非法访问 {message}");

            // 验证应用
            var app = App.FindByName(jwt.Subject);
            if ((app == null || !app.Enable) && ex == null) ex = new InvalidOperationException($"无效应用[{jwt.Subject}]");

            return (app, ex);
        }

        /// <summary>
        /// 更新在线状态
        /// </summary>
        /// <param name="app"></param>
        /// <param name="clientId"></param>
        /// <param name="ip"></param>
        /// <param name="token"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        public AppOnline UpdateOnline(App app, String clientId, String ip, String token, AppInfo info = null)
        {
            // 首先根据ClientId和Token直接查找应用在线
            var online = AppOnline.FindByClient(clientId) ?? AppOnline.FindByToken(token);

            var localIp = "";
            if (!clientId.IsNullOrEmpty())
            {
                var p = clientId.IndexOf('@');
                if (p > 0) localIp = clientId[..p];
            }

            // 如果是每节点单例部署，则使用本地IP作为会话匹配。可能是应用重启，前一次会话还在
            if (online == null && app.Singleton && !localIp.IsNullOrEmpty())
            {
                // 要求内网IP与外网IP都匹配，才能认为是相同会话，因为有可能不同客户端部署在各自内网而具有相同本地IP
                var list = AppOnline.FindAllByIP(localIp);
                online = list.OrderBy(e => e.Id).FirstOrDefault(e => e.AppId == app.Id && e.UpdateIP == ip);

                // 处理多IP
                if (online == null)
                {
                    list = AppOnline.FindAllByApp(app.Id);
                    online = list.OrderBy(e => e.Id).FirstOrDefault(e => !e.IP.IsNullOrEmpty() && e.IP.Contains(localIp) && e.UpdateIP == ip);
                }
            }

            // 早期客户端没有clientId
            if (online == null) online = AppOnline.GetOrAddClient(clientId) ?? AppOnline.GetOrAddClient(ip, token);

            online.PingCount++;
            if (!clientId.IsNullOrEmpty()) online.Client = clientId;
            if (!token.IsNullOrEmpty()) online.Token = token;
            if (online.CreateIP.IsNullOrEmpty()) online.CreateIP = ip;
            if (!ip.IsNullOrEmpty()) online.UpdateIP = ip;

            // 更新跟踪标识
            var traceId = DefaultSpan.Current?.TraceId;
            if (!traceId.IsNullOrEmpty()) online.TraceId = traceId;

            // 本地IP
            if (online.IP.IsNullOrEmpty()) online.IP = localIp;

            // 关联节点
            if (online.NodeId == 0)
            {
                var node = Node.FindAllByIP(online.IP).FirstOrDefault();
                if (node != null) online.NodeId = node.ID;
            }

            online.Fill(app, info);

            online.SaveAsync();

            return online;
        }
    }
}