using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Services;
using NewLife.Web;
using Stardust.Data;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>OAuth服务。向应用提供验证服务</summary>
[Route("[controller]/[action]")]
public class OAuthController(ITokenService tokenService, AppTokenService appTokenService, AppOnlineService appOnline, StarServerSetting setting) : ControllerBase
{
    [ApiFilter]
    public TokenModel Token([FromBody] TokenInModel model)
    {
        if (model.grant_type.IsNullOrEmpty()) model.grant_type = "password";

        var ip = HttpContext.GetUserHost();
        var clientId = model.ClientId;

        try
        {
            // 密码模式
            if (model.grant_type == "password")
            {
                var app = appTokenService.Authorize(model.UserName, model.Password, setting.AppAutoRegister, ip);

                // 更新应用信息
                app.LastLogin = DateTime.Now;
                app.LastIP = ip;
                app.Update();

                var tokenModel = tokenService.IssueToken(app.Name, clientId);

                var olt = appOnline.UpdateOnline(app, clientId, ip, tokenModel.AccessToken);

                app.WriteHistory("Authorize", true, model.UserName, olt?.Version, ip, clientId);

                return tokenModel;
            }
            // 刷新令牌
            else if (model.grant_type == "refresh_token")
            {
                var (jwt, ex) = tokenService.DecodeToken(model.refresh_token);

                // 验证应用
                var app = App.FindByName(jwt?.Subject);
                if (app == null || !app.Enable)
                {
                    ex ??= new ApiException(ApiCode.Unauthorized, $"无效应用[{jwt.Subject}]");
                }

                if (clientId.IsNullOrEmpty()) clientId = jwt.Id;

                if (ex != null)
                {
                    app.WriteHistory("RefreshToken", false, ex.ToString(), null, ip, clientId);
                    throw ex;
                }

                var tokenModel = tokenService.IssueToken(app.Name, clientId);

                var olt = appOnline.UpdateOnline(app, clientId, ip, tokenModel.AccessToken);

                //app.WriteHistory("RefreshToken", true, model.refresh_token, olt?.Version, ip, clientId);

                return tokenModel;
            }
            else
            {
                throw new NotSupportedException($"未支持 grant_type={model.grant_type}");
            }
        }
        catch (Exception ex)
        {
            var app = App.FindByName(model.UserName);
            app?.WriteHistory("Authorize", false, ex.ToString(), null, ip, clientId);

            throw;
        }
    }

    [ApiFilter]
    public Object UserInfo(String token)
    {
        var (jwt, ex) = tokenService.DecodeToken(token);
        if (ex != null) throw ex;

        var app = appTokenService.ValidApp(jwt?.Subject);
        return new
        {
            app.Id,
            app.Name,
            app.DisplayName,
            app.Category,
        };
    }
}