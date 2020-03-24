using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Server.Common;
using XCode.Membership;

namespace Stardust.Server.Controllers
{
    public abstract class BaseController : ControllerBase, IActionFilter
    {
        #region 属性
        /// <summary></summary>
        static readonly TokenSession _session = new TokenSession();

        /// <summary>临时会话扩展信息</summary>
        public IDictionary<String, Object> Session { get; private set; }

        /// <summary>令牌</summary>
        public String Token { get; private set; }

        /// <summary>是否使用Cookie</summary>
        public Boolean UseCookies { get; set; }

        /// <summary>用户主机</summary>
        public String UserHost => HttpContext.GetUserHost();

        /// <summary>节点引用，令牌无效时使用</summary>
        protected Node _nodeForHistory;
        #endregion

        #region 校验
        /// <summary>请求处理后</summary>
        /// <param name="context"></param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception != null)
            {
                // 拦截全局异常，写日志
                var action = context.HttpContext.Request.Path + "";
                if (context.ActionDescriptor is ControllerActionDescriptor act) action = $"{act.ControllerName}/{act.ActionName}";

                var node =  Session?["Node"] as Node ?? _nodeForHistory;
                WriteHistory(node, action, false, context.Exception?.GetTrue() + "");
            }
        }

        /// <summary>请求处理前</summary>
        /// <param name="context"></param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var ip = UserHost;
            ManageProvider.UserHost = ip;

            var request = context.HttpContext.Request;
            var token = request.Query["Token"] + "";
            if (token.IsNullOrEmpty()) token = (request.Headers["Authorization"] + "").TrimStart("Bearer ");
            if (token.IsNullOrEmpty()) token = request.Headers["X-Token"] + "";
            if (token.IsNullOrEmpty()) token = request.Cookies["Token"] + "";

            if (!token.IsNullOrEmpty())
            {
                Token = token;
                Session = _session.GetSession(token);
                // 考虑到可能出现的服务器切换或服务端闪断等情况
                if (Session == null) Session = RestoreSession(token);
            }
            else
            {
                CreateToken(null);
            }
        }
        #endregion

        #region 令牌
        private static TokenProvider _tokenProvider;
        private static TokenProvider GetTokenProvider(Boolean create)
        {
            if (_tokenProvider != null) return _tokenProvider;

            var provider = new TokenProvider();
            provider.ReadKey("../keys/token.prvkey", create);
            if (provider.Key.IsNullOrEmpty()) throw new ApplicationException("缺失私钥，无法创建令牌");

            return _tokenProvider = provider;
        }

        /// <summary>刷新令牌</summary>
        /// <param name="newTokedn"></param>
        protected void RefreshToken(String newToken)
        {
            Session = _session.CopySession(Token, newToken);
            Token = newToken;
        }

        /// <summary>创建token同时刷新token有效期</summary>
        /// <param name="code"></param>
        protected void CreateToken(String code)
        {
            var set = Setting.Current;
            var expire = set.TokenExpire;

            var provider = GetTokenProvider(true);

            var token = provider.Encode($"{code}#", DateTime.Now.AddSeconds(expire));
            if (Token.IsNullOrEmpty())
            {
                // 创建新的token
                Session = _session.CreateSession(token);
                Token = token;
            }
            else
            {
                RefreshToken(token);
            }

            if (UseCookies) Response.Cookies.Append("Token", Token);
        }

        /// <summary>由token恢复session</summary>
        /// <param name="token"></param>
        private IDictionary<String, Object> RestoreSession(String token)
        {
            if (token.IsNullOrEmpty()) return null;

            var str = token.Trim().Substring(null, ".")?.ToBase64().ToStr();
            var rlist = str?.Split('#', ',');
            if (rlist == null)
            {
                XTrace.WriteLine($"Token 解析失败！:{token}");
                return null;
            }

            var code = rlist[0];
            var node = Node.FindByCode(code) ?? new Node { Code = code };
            _nodeForHistory = node;

            var provider = GetTokenProvider(false);

            // token解码失败
            if (!provider.TryDecode(token, out var result, out var dt))
            {
                var msg = $"签名错误：{result ?? "null"} {dt:yyyy-MM-dd HH:mm:ss} {token}";
                XTrace.WriteLine(msg);
                WriteHistory(node, "签名错误", false, msg);

                return null;
            }

            // token 过期
            if (dt < DateTime.Now)
            {
                var msg = $"令牌过期：{result ?? "null"} {dt:yyyy-MM-dd HH:mm:ss} {token}";
                XTrace.WriteLine(msg);
                WriteHistory(node, "令牌过期", false, msg);

                return null;
            }

            XTrace.WriteLine("借助令牌复活 {0}/{1}", code, token);

            var dic = _session.CreateSession(token);
            if (dic == null) XTrace.WriteLine($"借助令牌复活 CreateSession null!");

            dic["Node"] = node;

            WriteHistory(node, "令牌复活", true, $"[{code}]{token}");

            return dic;
        }
        #endregion

        #region 日志
        protected virtual void WriteHistory(INode node, String action, Boolean success, String remark)
        {
            NodeHistory.Create(node, action, success, remark, Environment.MachineName, UserHost);
        }
        #endregion
    }
}