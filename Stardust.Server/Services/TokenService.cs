using System;
using System.Reflection;
using NewLife;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Server.Services
{
    /// <summary>应用服务</summary>
    public class TokenService
    {
        /// <summary>验证应用密码</summary>
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
        public TokenModel IssueToken(String name, String secret, Int32 expire)
        {
            // 颁发令牌
            var ss = secret.Split(':');
            var jwt = new JwtBuilder
            {
                Issuer = Assembly.GetEntryAssembly().GetName().Name,
                Subject = name,
                Id = Rand.NextString(8),
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
        public (JwtBuilder, Exception) DecodeToken(String token, String tokenSecret)
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
        /// <param name="set"></param>
        /// <returns></returns>
        public App DecodeToken(String token, Setting set)
        {
            if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

            // 解码令牌
            var ss = set.TokenSecret.Split(':');
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
                var node = Stardust.Data.Nodes.Node.FindByCode(jwt.Subject);
                if (node == null) throw new InvalidOperationException($"无效应用[{jwt.Subject}]");

                app = new App { Name = node.Code, DisplayName = node.Name, Enable = true };
            }
            if (!app.Enable) throw new InvalidOperationException($"已停用应用[{jwt.Subject}]");

            return app;
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
    }
}