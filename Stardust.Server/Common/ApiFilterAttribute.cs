using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Log;
using System;
using System.Linq;

namespace Stardust.Server.Common
{
    /// <summary>统一Api过滤处理</summary>
    public sealed class ApiFilterAttribute : ActionFilterAttribute
    {
        /// <summary>执行前，验证模型</summary>
        /// <param name="context"></param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
                throw new ApplicationException(context.ModelState.Values.First(p => p.Errors.Count > 0).Errors[0].ErrorMessage);

            // 访问令牌
            var request = context.HttpContext.Request;
            var token = request.Query["Token"] + "";
            if (token.IsNullOrEmpty()) token = (request.Headers["Authorization"] + "").TrimStart("Bearer ");
            if (token.IsNullOrEmpty()) token = request.Headers["X-Token"] + "";
            if (token.IsNullOrEmpty()) token = request.Cookies["Token"] + "";
            context.HttpContext.Items["Token"] = token;
            if (!context.ActionArguments.ContainsKey("token")) context.ActionArguments.Add("token", token);

            base.OnActionExecuting(context);
        }

        /// <summary>执行后，包装结果和异常</summary>
        /// <param name="context"></param>
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Result != null)
            {
                if (context.Result is ObjectResult obj)
                {
                    context.Result = new JsonResult(new { code = obj.StatusCode ?? 0, data = obj.Value });
                }
                else if (context.Result is EmptyResult)
                {
                    context.Result = new JsonResult(new { code = 0, data = new { } });
                }
            }
            else if (context.Exception != null && !context.ExceptionHandled)
            {
                var ex = context.Exception.GetTrue();
                if (ex is NewLife.Remoting.ApiException aex)
                    context.Result = new JsonResult(new { code = aex.Code, data = aex.Message });
                else
                    context.Result = new JsonResult(new { code = 500, data = ex.Message });

                context.ExceptionHandled = true;

                // 输出异常日志
                if (XTrace.Debug) XTrace.WriteException(ex);
            }

            base.OnActionExecuted(context);
        }
    }
}