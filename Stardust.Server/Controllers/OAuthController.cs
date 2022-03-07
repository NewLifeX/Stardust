using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Web;
using Stardust.Data;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    /// <summary>OAuth服务。向应用提供验证服务</summary>
    [Route("[controller]/[action]")]
    public class OAuthController : ControllerBase
    {
        /// <summary>用户主机</summary>
        public String UserHost => HttpContext.GetUserHost();

        private readonly TokenService _tokenService;
        public OAuthController(TokenService tokenService) => _tokenService = tokenService;

        [ApiFilter]
        public TokenModel Token([FromBody] TokenInModel model)
        {
            var set = Setting.Current;

            if (model.grant_type.IsNullOrEmpty()) model.grant_type = "password";

            var ip = HttpContext.GetUserHost();
            var clientId = model.ClientId;

            // 密码模式
            if (model.grant_type == "password")
            {
                var app = _tokenService.Authorize(model.UserName, model.Password, set.AutoRegister);

                // 更新应用信息
                app.LastLogin = DateTime.Now;
                app.LastIP = ip;
                app.SaveAsync();

                app.WriteHistory("Authorize", true, model.UserName, UserHost, clientId);

                var tokenModel = _tokenService.IssueToken(app.Name, set.TokenSecret, set.TokenExpire, clientId);

                AppOnline.UpdateOnline(app, clientId, ip, tokenModel.AccessToken);

                return tokenModel;
            }
            // 刷新令牌
            else if (model.grant_type == "refresh_token")
            {
                var (jwt, ex) = _tokenService.DecodeTokenWithError(model.refresh_token, set.TokenSecret);

                // 验证应用
                var app = App.FindByName(jwt?.Subject);
                if ((app == null || !app.Enable) && ex == null) ex = new InvalidOperationException($"无效应用[{jwt.Subject}]");

                if (clientId.IsNullOrEmpty()) clientId = jwt.Id;

                if (ex != null)
                {
                    app.WriteHistory("RefreshToken", false, ex.ToString(), UserHost, clientId);
                    throw ex;
                }

                app.WriteHistory("RefreshToken", true, model.refresh_token, UserHost, clientId);

                var tokenModel = _tokenService.IssueToken(app.Name, set.TokenSecret, set.TokenExpire, clientId);

                AppOnline.UpdateOnline(app, clientId, ip, tokenModel.AccessToken);

                return tokenModel;
            }
            else
            {
                throw new NotSupportedException($"未支持 grant_type={model.grant_type}");
            }
        }

        [ApiFilter]
        public Object UserInfo(String token)
        {
            var set = Setting.Current;

            var (_, app) = _tokenService.DecodeToken(token, set.TokenSecret);
            return new
            {
                app.Id,
                app.Name,
                app.DisplayName,
                app.Category,
            };
        }
    }
}