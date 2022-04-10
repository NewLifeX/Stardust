using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;

namespace Stardust.Server.Common
{
    /// <summary>统一Api过滤处理</summary>
    public sealed class ApiFilterAttribute : ActionFilterAttribute
    {
        /// <summary>从请求头中获取令牌</summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static String GetToken(HttpContext httpContext)
        {
            var request = httpContext.Request;
            var token = request.Query["Token"] + "";
            if (token.IsNullOrEmpty()) token = (request.Headers["Authorization"] + "").TrimStart("Bearer ");
            if (token.IsNullOrEmpty()) token = request.Headers["X-Token"] + "";
            if (token.IsNullOrEmpty()) token = request.Cookies["Token"] + "";

            return token;
        }

        /// <summary>执行前，验证模型</summary>
        /// <param name="context"></param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            //if (!context.ModelState.IsValid)
            //    throw new ApplicationException(context.ModelState.Values.First(p => p.Errors.Count > 0).Errors[0].ErrorMessage);

            // 访问令牌
            var token = GetToken(context.HttpContext);
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
                    //context.Result = new JsonResult(new { code = obj.StatusCode ?? 0, data = obj.Value });
                    var rs = new { code = obj.StatusCode ?? 0, data = obj.Value };
                    context.Result = new ContentResult
                    {
                        Content = rs.ToJson(false, true, true),
                        ContentType = "application/json",
                        StatusCode = 200
                    };
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

                var span = DefaultSpan.Current;
                span?.SetError(ex, null);

                // 输出异常日志
                if (XTrace.Debug)
                {
                    var ip = GetUserHost(context.HttpContext);
                    if (!ip.IsNullOrEmpty()) XTrace.WriteLine("[{0}]访问异常", ip);

                    XTrace.WriteException(ex);
                }
            }

            base.OnActionExecuted(context);
        }

        /// <summary>获取用户主机</summary>
        /// <param name="context"></param>
        /// <returns></returns>
        static String GetUserHost(HttpContext context)
        {
            var request = context.Request;

            var str = "";
            if (str.IsNullOrEmpty()) str = request.Headers["HTTP_X_FORWARDED_FOR"];
            if (str.IsNullOrEmpty()) str = request.Headers["X-Real-IP"];
            if (str.IsNullOrEmpty()) str = request.Headers["X-Forwarded-For"];
            if (str.IsNullOrEmpty()) str = request.Headers["REMOTE_ADDR"];
            if (str.IsNullOrEmpty())
            {
                var addr = context.Connection?.RemoteIpAddress;
                if (addr != null)
                {
                    if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
                    str = addr + "";
                }
            }

            return str;
        }
    }
}