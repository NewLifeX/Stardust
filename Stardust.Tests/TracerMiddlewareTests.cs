using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using NewLife.Log;
using Stardust.Extensions;
using Xunit;

namespace Stardust.Tests;

/// <summary>性能跟踪中间件测试。覆盖埋点名称自动截断、学习归并、解析器定制与旧规则回退</summary>
public class TracerMiddlewareTests : IDisposable
{
    #region 辅助
    private readonly String _oldPrefixes;
    private readonly Boolean _oldEnable;
    private readonly String[] _oldArea;
    private readonly IList<String> _oldAllow;
    private readonly Func<HttpContext, Boolean>? _oldAbandon;
    private readonly ITracer? _oldTracer;

    /// <summary>实例化。临时重置全局配置，避免外部状态影响测试</summary>
    public TracerMiddlewareTests()
    {
        _oldPrefixes = StarSetting.Current.TraceNamePrefixes;
        _oldEnable = TracerMiddleware.EnableAutoName;
        _oldArea = TracerMiddleware.AreaNames;
        _oldAllow = TracerMiddleware.AllowPaths;
        _oldAbandon = TracerMiddleware.ShouldAbandon;
        _oldTracer = TracerMiddleware.Tracer;

        StarSetting.Current.TraceNamePrefixes = "";
        TracerMiddleware.EnableAutoName = true;
        TracerMiddleware.AreaNames = [];
        TracerMiddleware.AllowPaths = [];
        TracerMiddleware.ShouldAbandon = null;
    }

    /// <summary>恢复全局配置</summary>
    public void Dispose()
    {
        StarSetting.Current.TraceNamePrefixes = _oldPrefixes;
        TracerMiddleware.EnableAutoName = _oldEnable;
        TracerMiddleware.AreaNames = _oldArea;
        TracerMiddleware.AllowPaths = _oldAllow;
        TracerMiddleware.ShouldAbandon = _oldAbandon;
        TracerMiddleware.Tracer = _oldTracer;
    }

    private static TracerMiddleware Create() => new(static ctx => Task.CompletedTask);

    /// <summary>创建中间件与可验证的埋点，用于验证埋点是否被抛弃</summary>
    private static (TracerMiddleware, Mock<ISpan>) CreateWithSpan()
    {
        var span = new Mock<ISpan>();
        span.SetupAllProperties();

        var tracer = new Mock<ITracer>();
        tracer.Setup(t => t.NewSpan(It.IsAny<String>())).Returns(span.Object);
        TracerMiddleware.Tracer = tracer.Object;

        return (Create(), span);
    }

    private static DefaultHttpContext CreateContext(String path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.Host = new HostString("127.0.0.1");
        ctx.Request.Path = path;

        // 让自动学习的访问地址与配置保持一致，避免测试期间写配置文件
        StarSetting.Current.ServiceAddress = "http://127.0.0.1";

        return ctx;
    }

    private static async Task<List<String>> Run(TracerMiddleware mw, params String[] paths) => await Run(mw, null, paths);

    private static async Task<List<String>> Run(TracerMiddleware mw, ITracerResolver? resolver, params String[] paths)
    {
        var names = new List<String>();
        var span = new Mock<ISpan>();
        span.SetupAllProperties();

        var tracer = new Mock<ITracer>();
        tracer.Setup(t => t.NewSpan(It.IsAny<String>())).Callback((String name) => names.Add(name)).Returns(span.Object);
        if (resolver != null) tracer.SetupGet(t => t.Resolver).Returns(resolver);

        var old = TracerMiddleware.Tracer;
        TracerMiddleware.Tracer = tracer.Object;
        try
        {
            foreach (var path in paths)
            {
                var ctx = CreateContext(path);
                await mw.Invoke(ctx);
            }
        }
        finally
        {
            TracerMiddleware.Tracer = old;
        }

        return names;
    }

    private sealed class FixedNameResolver : DefaultTracerResolver
    {
        private readonly String _name;
        public FixedNameResolver(String name) => _name = name;

        public override String? ResolveName(String name, Object? userState) => _name;
    }

    private static String ToLetters(Int32 value)
    {
        var chars = new Char[8];
        var i = chars.Length;
        do
        {
            chars[--i] = (Char)('a' + value % 26);
            value /= 26;
        } while (value > 0 && i > 0);

        return new String(chars, i, chars.Length - i);
    }
    #endregion

    [Fact]
    public async Task AutoName_StablePath_KeepFullName()
    {
        var mw = Create();

        // 下游系统把参数放在查询串，路径本身稳定，应完整保留
        var names = await Run(mw, "/v1/chat/completions", "/v1/chat/completions");

        Assert.Equal(2, names.Count);
        Assert.All(names, e => Assert.Equal("/v1/chat/completions", e));
    }

    [Fact]
    public async Task AutoName_ActionSet_KeepFullName()
    {
        var mw = Create();

        // 控制器固定动作集合（6个 action，低于阈值16）：不同子值增长有限，不会被归并
        var actions = new[] { "a1", "a2", "a3", "a4", "a5", "a6" };
        var paths = actions.Concat(actions).Select(e => $"/v1/user/{e}").ToArray();
        var names = await Run(mw, paths);

        Assert.Equal(12, names.Count);
        Assert.Equal(6, names.Distinct().Count());
        Assert.All(names, e => Assert.StartsWith("/v1/user/a", e));
        Assert.Contains("/v1/user/a6", names);
    }

    [Fact]
    public async Task AutoName_AllowPaths_KeepFullName()
    {
        var mw = Create();

        // 自定义放行规则支持*模糊匹配，可直接追加：命中后保留完整路径且不参与学习，20个动作也不会被归并
        TracerMiddleware.AllowPaths.Add("/t7/user/*");
        var paths = Enumerable.Range(1, 20).Select(e => $"/t7/user/a{e}").ToArray();
        var names = await Run(mw, paths);

        Assert.Equal(20, names.Count);
        Assert.Equal(20, names.Distinct().Count());
        Assert.All(names, e => Assert.StartsWith("/t7/user/a", e));
    }

    [Fact]
    public async Task AutoName_AreaNames_KeepFullName()
    {
        var mw = Create();

        // 魔方区域整体放行：首段命中即保留完整路径且不参与学习
        TracerMiddleware.AreaNames = ["t8"];
        var paths = Enumerable.Range(1, 20).Select(e => $"/t8/User/a{e}").ToArray();
        var names = await Run(mw, paths);

        Assert.Equal(20, names.Count);
        Assert.Equal(20, names.Distinct().Count());
        Assert.All(names, e => Assert.StartsWith("/t8/User/a", e));
    }

    [Fact]
    public async Task AutoName_RepeatedValue_KeepFullName()
    {
        var mw = Create();

        // 少量或重复的参数值不会触发学习阈值，保持完整名称（基数有界）
        var names = await Run(mw, "/t2/api/order/12345", "/t2/api/order/12345");

        Assert.Equal(2, names.Count);
        Assert.All(names, e => Assert.Equal("/t2/api/order/12345", e));
    }

    [Fact]
    public async Task AutoName_ParameterBurst_MergeToOneName()
    {
        var mw = Create();

        // 60秒内1000次调用，参数不同导致URL不同，客户端必须归并为一个埋点
        var paths = Enumerable.Range(0, 1000).Select(e => $"/t3/api/user/{100000 + e}").ToArray();
        var names = await Run(mw, paths);

        // 冷启动前16个不同参数为完整名称，第17个起归并
        Assert.Equal(1000, names.Count);
        Assert.Equal(17, names.Distinct().Count());
        Assert.Equal(984, names.Count(e => e == "/t3/api/user"));
        Assert.Equal("/t3/api/user", names[^1]);
    }

    [Fact]
    public async Task AutoName_SlugBurst_LimitTransientNames()
    {
        var mw = Create();

        // 字母型参数同样依靠不同子值数量识别，瞬态名称数量有界
        var paths = Enumerable.Range(0, 100).Select(e => $"/t4/files/{ToLetters(e)}").ToArray();
        var names = await Run(mw, paths);

        Assert.Equal(100, names.Count);
        Assert.Equal(17, names.Distinct().Count());
        Assert.Equal(84, names.Count(e => e == "/t4/files"));
        Assert.Equal("/t4/files", names[^1]);
    }

    [Fact]
    public async Task AutoName_ActionMatrix_KeepFullName()
    {
        var mw = Create();

        // 固定动作集合反复出现，不应被截断（不劣于传统三段规则）
        var actions = new[] { "Edit", "Add", "Delete", "Info" };
        var paths = Enumerable.Range(0, 10).SelectMany(e => actions.Select(a => $"/t5/Admin/User/{a}")).ToArray();
        var names = await Run(mw, paths);

        Assert.Equal(40, names.Count);
        Assert.All(names, e => Assert.StartsWith("/t5/Admin/User/", e));
        Assert.Contains("/t5/Admin/User/Edit", names);
    }

    [Fact]
    public async Task AutoName_Disabled_UseLegacyRule()
    {
        var old = TracerMiddleware.EnableAutoName;
        TracerMiddleware.EnableAutoName = false;
        try
        {
            var mw = Create();

            // 传统规则：WebApi保留两段，第三个路径段命中魔方动作时保留三段
            var names = await Run(mw, "/api/user/list", "/Admin/User/Edit");

            Assert.Equal("/api/user", names[0]);
            Assert.Equal("/Admin/User/Edit", names[1]);
        }
        finally
        {
            TracerMiddleware.EnableAutoName = old;
        }
    }

    [Fact]
    public async Task Resolver_CustomName_Applied()
    {
        var mw = Create();

        var names = await Run(mw, new FixedNameResolver("/custom/t7"), "/t7/whatever/x");

        Assert.Equal("/custom/t7", Assert.Single(names));
    }

    [Fact]
    public async Task Resolver_EmptyName_SkipSpan()
    {
        var mw = Create();

        var names = await Run(mw, new FixedNameResolver(""), "/t7/whatever/y");

        Assert.Empty(names);
    }

    [Fact]
    public async Task Prefix_FromConfig_AppliedImmediately()
    {
        var old = StarSetting.Current.TraceNamePrefixes;
        StarSetting.Current.TraceNamePrefixes = "/t8/api/order";
        try
        {
            var mw = Create();

            // 配置文件中的前缀（学习结果或手工调整）立即生效
            var names = await Run(mw, "/t8/api/order/history", "/t8/api/order/detail");

            Assert.All(names, e => Assert.Equal("/t8/api/order", e));
        }
        finally
        {
            StarSetting.Current.TraceNamePrefixes = old;
        }
    }

    [Fact]
    [DisplayName("404扫描探测_未命中路由终结点时抛弃埋点")]
    public async Task Abandon_UnmatchedRoute_DiscardsSpan()
    {
        var (mw, span) = CreateWithSpan();
        var ctx = CreateContext("/wp-login.php");

        // 路径不存在：没有命中任何终结点，状态码404
        ctx.Response.StatusCode = 404;
        await mw.Invoke(ctx);

        span.Verify(s => s.Abandon(), Times.Once);
        // 垃圾埋点不标记异常，避免额外产生异常埋点
        span.Verify(s => s.SetError(It.IsAny<Exception>(), It.IsAny<Object>()), Times.Never);
    }

    [Fact]
    [DisplayName("404业务响应_命中路由终结点时保留埋点")]
    public async Task Abandon_MatchedRoute_KeepsSpan()
    {
        var (mw, span) = CreateWithSpan();
        var ctx = CreateContext("/api/user/1");

        // 资源不存在：路由已命中，属于正常业务行为
        ctx.SetEndpoint(new Endpoint(static _ => Task.CompletedTask, EndpointMetadataCollection.Empty, "UserApi"));
        ctx.Response.StatusCode = 404;
        await mw.Invoke(ctx);

        span.Verify(s => s.Abandon(), Times.Never);
    }

    [Fact]
    [DisplayName("自定义抛弃规则_接管默认判定")]
    public async Task Abandon_CustomRule_TakesOver()
    {
        var (mw, span) = CreateWithSpan();
        var ctx = CreateContext("/api/user/1");

        // 命中终结点的405，默认规则不抛弃；自定义规则接管后抛弃
        ctx.SetEndpoint(new Endpoint(static _ => Task.CompletedTask, EndpointMetadataCollection.Empty, "UserApi"));
        ctx.Response.StatusCode = 405;
        TracerMiddleware.ShouldAbandon = static ctx => ctx.Response.StatusCode == 405;

        await mw.Invoke(ctx);

        span.Verify(s => s.Abandon(), Times.Once);
    }

    [Fact]
    [DisplayName("自定义抛弃规则_返回false时保留默认不抛弃的埋点")]
    public async Task Abandon_CustomRuleCanKeep()
    {
        var (mw, span) = CreateWithSpan();
        var ctx = CreateContext("/wp-login.php");

        ctx.Response.StatusCode = 404;
        TracerMiddleware.ShouldAbandon = static ctx => false;

        await mw.Invoke(ctx);

        span.Verify(s => s.Abandon(), Times.Never);
    }
}
