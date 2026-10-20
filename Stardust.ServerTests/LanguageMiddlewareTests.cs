using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NewLife;
using Stardust.Server;
using Xunit;

namespace ServerTest;

/// <summary>请求语言中间件测试。验证语言来源优先级与界面文化作用域</summary>
[Collection("Localization")]
public class LanguageMiddlewareTests : IDisposable
{
    private readonly CultureInfo? _old = Localization.Culture;

    /// <summary>恢复原始目标语言</summary>
    public void Dispose() => Localization.Culture = _old;

    private static void LoadAssemblies() => RuntimeHelpers.RunModuleConstructor(typeof(Startup).Module.ModuleHandle);

    [Fact(DisplayName = "语言来源优先查询串，其次 Cookie，最后请求头")]
    public void LanguagePrecedence()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "ja";
        Assert.Equal("ja", LanguageMiddleware.GetLanguage(context));

        context.Request.Headers.Cookie = ".AspNet.Culture=de";
        Assert.Equal("de", LanguageMiddleware.GetLanguage(context));

        context.Request.QueryString = new QueryString("?lang=fr");
        Assert.Equal("fr", LanguageMiddleware.GetLanguage(context));
    }

    [Fact(DisplayName = "中间件按请求头设置界面语言并在请求结束后还原")]
    public async Task MiddlewareAppliesRequestCulture()
    {
        LoadAssemblies();
        Localization.Culture = null;

        var host = CultureInfo.CurrentUICulture;
        var inner = "";
        var middleware = new LanguageMiddleware(_ =>
        {
            inner = CultureInfo.CurrentUICulture.Name;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "ja";

        await middleware.Invoke(context);

        Assert.Equal("ja", inner);
        Assert.Equal(host, CultureInfo.CurrentUICulture);
    }

    [Fact(DisplayName = "未提供受支持语言时不改变界面语言")]
    public async Task UnsupportedLanguageKeepsCulture()
    {
        LoadAssemblies();
        Localization.Culture = null;

        var host = CultureInfo.CurrentUICulture;
        var inner = "";
        var middleware = new LanguageMiddleware(_ =>
        {
            inner = CultureInfo.CurrentUICulture.Name;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "sw-KE";

        await middleware.Invoke(context);

        Assert.Equal(host.Name, inner);
    }
}
