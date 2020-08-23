using System;
using Microsoft.AspNetCore.Mvc;
using Stardust.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    /// <summary>OAuth服务。向应用提供验证服务</summary>
    [Route("[controller]")]
    public class OAuthController : ControllerBase
    {
        private readonly AppService _service = new AppService();

        public TokenModel Token(String grant_type, String username, String password, String refresh_token)
        {
            var set = Setting.Current;

            // 密码模式
            if (grant_type == "password")
            {
                var app = _service.Authorize(username, password, set);
                return _service.IssueToken(app, set);
            }
            // 刷新令牌
            else if (grant_type == "refresh_token")
            {
                var app = _service.DecodeToken(refresh_token, set);
                return _service.IssueToken(app, set);
            }
            else
            {
                throw new NotSupportedException($"未支持 grant_type={grant_type}");
            }
        }
    }
}