using NewLife;
using NewLife.Remoting;
using Stardust.Data;

namespace Stardust.Server.Services;

/// <summary>应用令牌服务</summary>
public class AppTokenService
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

    public App ValidApp(String appId)
    {
        // 验证应用
        var app = App.FindByName(appId);
        if (app == null)
        {
            // 可能是StarAgent混用了token
            var node = Data.Nodes.Node.FindByCode(appId);
            if (node == null) throw new ApiException(ApiCode.Forbidden, $"无效应用[{appId}]");

            app = new App { Name = node.Code, DisplayName = node.Name, Enable = true };
        }
        if (!app.Enable) throw new ApiException(ApiCode.Forbidden, $"已停用应用[{appId}]");

        return app;
    }
}