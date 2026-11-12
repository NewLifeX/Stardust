using NewLife;
using NewLife.Log;
using NewLife.Remoting;

namespace Stardust.Server.Controllers;

/// <summary>OpenAI 兼容错误映射。把内部异常转换为 OpenAI 错误体与 HTTP 状态码</summary>
internal static class OpenAIErrors
{
    /// <summary>映射API异常</summary>
    /// <param name="ex">API异常</param>
    /// <returns>状态码、消息、错误类型与错误码</returns>
    public static (Int32 Status, String Message, String Type, String? Code) Map(ApiException ex)
    {
        var status = ex.Code switch
        {
            ApiCode.BadRequest => 400,
            ApiCode.Unauthorized => 401,
            ApiCode.Forbidden => 403,
            ApiCode.NotFound => 404,
            ApiCode.TooManyRequests => 429,
            _ => 500,
        };
        var type = status switch
        {
            400 => "invalid_request_error",
            401 => "authentication_error",
            403 => "permission_error",
            429 => "rate_limit_error",
            _ => "server_error",
        };

        return (status, ex.Message, type, ex.Code + "");
    }

    /// <summary>映射任意异常</summary>
    /// <param name="ex">异常</param>
    /// <returns>状态码、消息、错误类型与错误码</returns>
    public static (Int32 Status, String Message, String Type, String? Code) Map(Exception ex) => ex switch
    {
        ApiException aex => Map(aex),
        OperationCanceledException => (500, L.T("AI upstream request failed: {0}", "timeout"), "server_error", "timeout"),
        _ => (500, ex.Message, "server_error", "internal_error"),
    };
}
