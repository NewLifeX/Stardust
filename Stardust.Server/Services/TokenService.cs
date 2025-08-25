﻿using System.Reflection;
using NewLife;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Server.Services;

/// <summary>应用服务</summary>
public class TokenService
{
    /// <summary>验证应用密码，不存在时新增</summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="autoRegister"></param>
    /// <returns></returns>
    public App Authorize(String username, String password, Boolean autoRegister, String ip = null)
    {
        if (username.IsNullOrEmpty()) throw new ArgumentNullException(nameof(username));
        //if (password.IsNullOrEmpty()) throw new ArgumentNullException(nameof(password));

        // 查找应用
        var app = App.FindByName(username);
        // 查找或创建应用，避免多线程创建冲突
        app ??= App.GetOrAdd(username, App.FindByName, k => new App
        {
            Name = username,
            Secret = password,
            Enable = autoRegister,
        });

        // 检查黑白名单
        if (!app.MatchIp(ip))
            throw new ApiException(ApiCode.Forbidden, $"应用[{username}]禁止{ip}访问！");
        if (app.Project != null && !app.Project.MatchIp(ip))
            throw new ApiException(ApiCode.Forbidden, $"项目[{app.Project}]禁止{ip}访问！");

        // 检查应用有效性
        if (!app.Enable) throw new ApiException(ApiCode.Forbidden, $"应用[{username}]已禁用！");
        if (!app.Secret.IsNullOrEmpty() && password != app.Secret) throw new ApiException(ApiCode.Forbidden, $"非法访问应用[{username}]！");

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

    /// <summary>验证并续发新令牌，过期前10分钟才能续发</summary>
    /// <param name="name"></param>
    /// <param name="token"></param>
    /// <param name="secret"></param>
    /// <param name="expire"></param>
    /// <returns></returns>
    public TokenModel ValidAndIssueToken(String name, String token, String secret, Int32 expire, String clientId)
    {
        if (token.IsNullOrEmpty()) return null;

        // 令牌有效期检查，10分钟内过期者，重新颁发令牌
        var ss = secret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
        if (!jwt.TryDecode(token, out _)) return null;

        return DateTime.Now.AddMinutes(10) > jwt.Expire ? IssueToken(name, secret, expire, clientId) : null;
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
        if (!jwt.TryDecode(token, out var message)) ex = new ApiException(ApiCode.Unauthorized, $"[{jwt.Subject}]非法访问 {message}");

        return (jwt, ex);
    }

    /// <summary>解码令牌，得到App应用</summary>
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
        if (!jwt.TryDecode(token, out var message)) throw new ApiException(ApiCode.Forbidden, $"非法访问[{jwt.Subject}]，{message}");

        // 验证应用
        var app = App.FindByName(jwt.Subject);
        if (app == null)
        {
            // 可能是StarAgent混用了token
            var node = Data.Nodes.Node.FindByCode(jwt.Subject);
            if (node == null) throw new ApiException(ApiCode.Forbidden, $"无效应用[{jwt.Subject}]");

            app = new App { Name = node.Code, DisplayName = node.Name, Enable = true };
        }
        if (!app.Enable) throw new ApiException(ApiCode.Forbidden, $"已停用应用[{jwt.Subject}]");

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
        if (!jwt.TryDecode(token, out var message)) ex = new ApiException(ApiCode.Forbidden, $"非法访问 {message}");

        // 验证应用
        var app = App.FindByName(jwt.Subject);
        if ((app == null || !app.Enable) && ex == null) ex = new ApiException(ApiCode.NotFound, $"无效应用[{jwt.Subject}]");

        return (app, ex);
    }
}