using System;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Remoting;
using Stardust.Server.Controllers;

namespace Stardust.Server.Common
{
    /// <summary>令牌校验</summary>
    public class TokenFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.Controller is BaseController bc)
            {
                var session = bc.Session;
                if (bc.Token.IsNullOrEmpty()) throw new ApiException(403, "未授权");
                if (session == null) throw new ApiException(402, "令牌无效");
            }

            base.OnActionExecuting(context);
        }
    }
}