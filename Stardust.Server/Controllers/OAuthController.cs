using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data;
using Stardust.Models;

namespace Stardust.Server.Controllers
{
    /// <summary>OAuth服务。向应用提供验证服务</summary>
    [Route("[controller]")]
    public class OAuthController : ControllerBase
    {
        public TokenModel Token(String grant_type, String username, String password, String refresh_token)
        {
            // 密码模式
            if (grant_type == "password")
            {
                if (username.IsNullOrEmpty()) throw new ArgumentNullException(nameof(username));
                if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

                var set = Setting.Current;

                // 查找应用
                var app = App.FindByName(username);
                if (app == null)
                {
                    app = new App
                    {
                        Name = username,
                        Secret = password,
                        Enable = set.AutoRegister,
                    };

                    // 先保存
                    app.Insert();

                    if (!set.AutoRegister) throw new ArgumentOutOfRangeException(nameof(username), $"应用[{username}]不存在且禁止自动注册！");
                }

                // 检查应用有效性
                if (!app.Enable) throw new ArgumentOutOfRangeException(nameof(username), $"应用[{username}]已禁用！");
                if (!app.Secret.IsNullOrEmpty() && password != app.Secret) throw new InvalidOperationException($"非法访问应用[{username}]！");

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
                    TokenType = jwt.Type,
                    ExpireIn = set.TokenExpire,
                    RefreshToken = jwt.Encode(""),
                };
            }
            // 刷新令牌
            else if (grant_type == "refresh_token")
            {
                if (refresh_token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(refresh_token));

                var set = Setting.Current;

                // 解码令牌
                var ss = set.TokenSecret.Split(':');
                var jwt = new JwtBuilder
                {
                    Algorithm = ss[0],
                    Secret = ss[1],
                };
                if (!jwt.TryDecode(refresh_token, out var message)) throw new InvalidOperationException($"非法访问 {message}");

                // 验证应用
                var app = App.FindByName(jwt.Subject);
                if (app == null || !app.Enable) throw new ArgumentOutOfRangeException(nameof(username), $"无效应用[{jwt.Subject}]");

                // 重新颁发令牌
                jwt.Issuer = Assembly.GetEntryAssembly().GetName().Name;
                jwt.Id = Rand.NextString(8);
                jwt.Expire = DateTime.Now.AddSeconds(set.TokenExpire);

                return new TokenModel
                {
                    AccessToken = jwt.Encode(null),
                    TokenType = jwt.Type,
                    ExpireIn = set.TokenExpire,
                    RefreshToken = jwt.Encode(""),
                };
            }
            else
            {
                throw new NotSupportedException($"未支持 grant_type={grant_type}");
            }
        }
    }
}