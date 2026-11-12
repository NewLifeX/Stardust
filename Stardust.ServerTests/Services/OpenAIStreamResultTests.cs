using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Stardust.Models;
using Stardust.Server;
using Stardust.Server.Controllers;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>OpenAI 兼容流式结果测试。覆盖分片写出、未启用时的JSON错误回退与中途错误的错误分片</summary>
public class OpenAIStreamResultTests
{
    private static AISettingScope UseDefaultSettings() => new(s =>
    {
        s.EnableAI = true;
        s.AIUrl = "https://ai.example.com";
        s.AIKey = "sk-test";
        s.AIModel = "deepseek-chat";
        s.AIModels = "";
        s.AISystemPrompt = "";
        s.AITimeout = 30;
        s.AITemperature = 0.7;
        s.AIMaxTokens = 1024;
    });

    private sealed class TestResponseFeature : IHttpResponseFeature
    {
        public Int32 StatusCode { get; set; } = 200;
        public String ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public Boolean HasStarted { get; set; }

        public void OnStarting(Func<Object, Task> callback, Object state) { }
        public void OnCompleted(Func<Object, Task> callback, Object state) { }
    }

    private static (DefaultHttpContext context, MemoryStream body) CreateContext(Boolean started = false)
    {
        var body = new MemoryStream();
        var feature = new TestResponseFeature { Body = body, HasStarted = started };
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));

        return (context, body);
    }

    private static AIChatRequest NewRequest() => new() { Messages = [new AIChatMessage { Content = "hi" }] };

    private static ActionContext NewActionContext(DefaultHttpContext context) => new(context, new RouteData(), new ActionDescriptor());

    [Fact(DisplayName = "OpenAI流式结果按chat.completion.chunk写出并以DONE结束")]
    public async Task Execute_WritesChunks()
    {
        var handler = new AIStubHandler
        {
            ResponseBody = "data: {\"model\":\"deepseek-chat\",\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
                "data: [DONE]\n\n",
        };
        using var scope = UseDefaultSettings();
        var service = new AIService(new AIStubFactory(handler));
        var result = new OpenAIStreamResult(service, NewRequest(), "app", null, "deepseek-chat");

        var (context, body) = CreateContext();
        await result.ExecuteResultAsync(NewActionContext(context));

        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.StartsWith("text/event-stream", context.Response.ContentType);
        Assert.Contains("chat.completion.chunk", text);
        Assert.Contains("\"role\":\"assistant\"", text);
        Assert.Contains("你好", text);
        Assert.Contains("\"finish_reason\":\"stop\"", text);
        Assert.Contains("data: [DONE]", text);
    }

    [Fact(DisplayName = "OpenAI接口未启用时流式结果回退为标准错误JSON")]
    public async Task Execute_NotEnabled_WritesJsonError()
    {
        using var scope = UseDefaultSettings();
        StarServerSetting.Current.EnableAI = false;

        var service = new AIService(new AIStubFactory(new AIStubHandler()));
        var result = new OpenAIStreamResult(service, NewRequest(), "app", null, "deepseek-chat");

        var (context, body) = CreateContext();
        await result.ExecuteResultAsync(NewActionContext(context));

        Assert.Equal(403, context.Response.StatusCode);
        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("\"error\"", text);
        Assert.Contains("permission_error", text);
    }

    [Fact(DisplayName = "OpenAI流式输出中途出错时写错误分片")]
    public async Task Execute_MidStreamError_WritesErrorChunk()
    {
        // 先给一段正常内容，再给错误段，触发“已开始响应”的错误分支
        var handler = new AIStubHandler
        {
            ResponseBody = "data: {\"choices\":[{\"delta\":{\"content\":\"半\"}}]}\n\n" +
                "data: {\"error\":{\"message\":\"quota exceeded\"}}\n\n",
        };
        using var scope = UseDefaultSettings();
        var service = new AIService(new AIStubFactory(handler));
        var result = new OpenAIStreamResult(service, NewRequest(), "app", null, "deepseek-chat");

        var (context, body) = CreateContext(started: true);
        await result.ExecuteResultAsync(NewActionContext(context));

        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("半", text);
        Assert.Contains("quota exceeded", text);
        Assert.Contains("data: [DONE]", text);
    }
}
