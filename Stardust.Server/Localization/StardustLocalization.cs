using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NewLife;

namespace Stardust.Server;

/// <summary>请求语言中间件。按查询串 lang、Cookie 与 Accept-Language 识别当前请求的语言</summary>
/// <remarks>
/// 只修改 <see cref="CultureInfo.CurrentUICulture"/>，不修改 <see cref="CultureInfo.CurrentCulture"/>，
/// 避免影响协议中的数字与日期格式。未识别到受支持语言时不改变宿主语言。
/// </remarks>
public class LanguageMiddleware(RequestDelegate next)
{
    /// <summary>处理请求</summary>
    /// <param name="context">HTTP上下文</param>
    public async Task Invoke(HttpContext context)
    {
        using var scope = RequestCultureScope.Create(GetLanguage(context));

        await next(context);
    }

    /// <summary>取请求语言。优先查询串 lang，其次 Cookie，最后 Accept-Language</summary>
    /// <param name="context">HTTP上下文</param>
    /// <returns>语言名称；未指定时返回 null</returns>
    public static String? GetLanguage(HttpContext context)
    {
        var name = context.Request.Query["lang"].FirstOrDefault();
        if (name.IsNullOrEmpty()) name = context.Request.Cookies[".AspNet.Culture"];
        if (name.IsNullOrEmpty()) name = context.Request.Headers.AcceptLanguage.FirstOrDefault();

        return name;
    }
}

/// <summary>星尘多语言宿主扩展</summary>
public static class StardustLocalizationExtensions
{
    /// <summary>启用多语言。配置了固定语言时全进程生效，否则按请求语言识别</summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    /// <remarks>
    /// 请求语言识别期间，日志与配置文件注释固定为宿主语言，避免同一日志文件随请求语言混杂。
    /// 词表由各组件编译期 <c>Locales/*.yml</c> 生成，英文为源文与兜底。
    /// </remarks>
    public static IApplicationBuilder UseStardustLocalization(this IApplicationBuilder app)
    {
        var language = StarServerSetting.Current?.Language;
        if (!language.IsNullOrWhiteSpace() && StarLocalization.UseLanguage(language) != null) return app;

        // 界面语言跟随请求，由中间件写入 CurrentUICulture
        Localization.Culture = null;

        // 日志与配置注释使用宿主语言
        Localization.LogCulture = CultureInfo.CurrentUICulture;

        return app.UseMiddleware<LanguageMiddleware>();
    }
}
