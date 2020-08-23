using System;
using System.Reflection;
using NewLife;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data;
using Stardust.Models;

namespace Stardust.Server.Services
{
    /// <summary>应用服务</summary>
    public class AppService
    {
        /// <summary>验证应用密码</summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="autoRegister"></param>
        /// <returns></returns>
        public App Authorize(String username, String password, Boolean autoRegister)
        {
            if (username.IsNullOrEmpty()) throw new ArgumentNullException(nameof(username));
            if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

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
        /// <param name="app"></param>
        /// <param name="set"></param>
        /// <returns></returns>
        public TokenModel IssueToken(App app, Setting set)
        {
            // 颁发令牌
            var ss = set.TokenSecret.Split(':');
            var jwt = new JwtBuilder
            {
                Issuer = Assembly.GetEntryAssembly().GetName().Name,
                Subject = app.Name,
                Id = Rand.NextString(8),
                Expire = DateTime.Now.AddSeconds(set.TokenExpire),

                Algorithm = ss[0],
                Secret = ss[1],
            };

            return new TokenModel
            {
                AccessToken = jwt.Encode(null),
                TokenType = jwt.Type ?? "JWT",
                ExpireIn = set.TokenExpire,
                RefreshToken = jwt.Encode(null),
            };
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
            if (!jwt.TryDecode(token, out var message)) throw new InvalidOperationException($"非法访问 {message}");

            // 验证应用
            var app = App.FindByName(jwt.Subject);
            if (app == null || !app.Enable) throw new InvalidOperationException($"无效应用[{jwt.Subject}]");

            return app;
        }
    }
}