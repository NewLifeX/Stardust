using NewLife;
using NewLife.Remoting;
using Stardust.Data;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>应用令牌服务</summary>
public class AppTokenService
{
    /// <summary>验证应用密码，不存在时新增。密钥不匹配（含空密钥）时按重注册模式裁决</summary>
    /// <param name="username">应用标识</param>
    /// <param name="password">应用密钥。为空或错误时按该应用的重注册模式放行或拒绝</param>
    /// <param name="autoRegister">应用不存在时是否自动创建</param>
    /// <param name="ip">来源IP</param>
    /// <param name="clientId">客户端标识。仅用于写历史</param>
    /// <returns>验证通过的应用</returns>
    public App Authorize(String username, String password, Boolean autoRegister, String ip = null, String clientId = null)
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

        // 密钥校验。空密钥或错误密钥时按重注册模式裁决，与 App/Login 的 Register 保持同一套语义：
        // 关闭=一律拒绝（默认安全）；收敛窗口=临时放行，客户端携带正确密钥后自动关闭；开放=放行；可信接入暂不支持本接口
        if (app.Secret.IsNullOrEmpty() || password != app.Secret)
        {
            var mode = app.ReRegister;
            if (mode is not (ReRegisterModes.Auto or ReRegisterModes.Open))
            {
                // 安全纵深（V-1/V-2）：空密钥/错误密钥默认禁止，防止身份冒用与真实密钥泄露
                if (mode == ReRegisterModes.Trusted)
                    throw new ApiException(ApiCode.Forbidden, $"应用[{username}]重注册模式[可信接入]暂不支持当前接口，请配置密钥或改用收敛窗口/开放！");
                if (app.Secret.IsNullOrEmpty())
                    throw new ApiException(ApiCode.Forbidden, $"应用[{username}]未配置密钥，禁止访问！请先为该应用设置Secret");
                throw new ApiException(ApiCode.Forbidden, $"非法访问应用[{username}]！");
            }

            // 收敛窗口/开放：放行，等待客户端收敛；密钥由令牌接口（OAuth/Token）随响应下发
            app.WriteHistory("应用重注册", true, $"[{app.Name}]密钥不符，按[{mode}]模式放行", null, ip, clientId);
        }
        // 收敛窗口：客户端已携带正确密钥，自动关闭窗口完成收敛
        else if (app.ReRegister == ReRegisterModes.Auto)
        {
            app.WriteHistory("应用重注册", true, $"[{app.Name}]客户端已携带正确密钥，收敛窗口自动关闭", null, ip, clientId);
            app.ReRegister = ReRegisterModes.Off;
            app.Update();
        }

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