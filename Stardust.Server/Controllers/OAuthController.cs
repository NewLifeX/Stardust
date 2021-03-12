using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    /// <summary>OAuth服务。向应用提供验证服务</summary>
    [Route("[controller]/[action]")]
    public class OAuthController : ControllerBase
    {
        private readonly AppService _service;
        public OAuthController(AppService appService) => _service = appService;

        [ApiFilter]
        public TokenModel Token([FromBody] TokenInModel model)
        {
            var set = Setting.Current;

            if (model.grant_type.IsNullOrEmpty()) model.grant_type = "password";

            // 密码模式
            if (model.grant_type == "password")
            {
                var app = _service.Authorize(model.UserName, model.Password, set.AutoRegister);

                // 更新应用信息
                app.LastLogin = DateTime.Now;
                app.LastIP = HttpContext.GetUserHost();
                app.SaveAsync();

                app.WriteHistory("Authorize", true, model.UserName);

                return _service.IssueToken(app, set);
            }
            // 刷新令牌
            else if (model.grant_type == "refresh_token")
            {
                var app = _service.DecodeToken(model.refresh_token, set);

                app.WriteHistory("RefreshToken", true, model.refresh_token);

                return _service.IssueToken(app, set);
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

            var app = _service.DecodeToken(token, set);
            return new
            {
                app.ID,
                app.Name,
                app.DisplayName,
                app.Category,
            };
        }
    }
}