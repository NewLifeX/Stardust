using System.Net;
using System.Text;
using System.Text.Json;
using NewLife.Remoting;
using Stardust.Models;
using Stardust.Server;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Services;

/// <summary>AI对话服务单元测试。覆盖地址解析、开关与配置校验、模型白名单、上游请求组装与响应解析</summary>
public class AIServiceTests
{
    private static AIChatRequest NewRequest(String content = "hello", String system = null) => new()
    {
        Messages = [new AIChatMessage { Content = content }],
        SystemPrompt = system,
    };

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

    private const String SuccessBody = """
        {
          "id": "chatcmpl-1",
          "object": "chat.completion",
          "model": "deepseek-chat",
          "choices": [
            { "index": 0, "message": { "role": "assistant", "content": "你好，我是AI" }, "finish_reason": "stop" }
          ],
          "usage": { "prompt_tokens": 12, "completion_tokens": 8, "total_tokens": 20 }
        }
        """;

    [Theory(DisplayName = "对话地址支持基础地址、v1地址与完整地址")]
    [InlineData("https://ai.example.com", "https://ai.example.com/v1/chat/completions")]
    [InlineData("https://ai.example.com/", "https://ai.example.com/v1/chat/completions")]
    [InlineData("https://ai.example.com/v1", "https://ai.example.com/v1/chat/completions")]
    [InlineData("https://ai.example.com/v1/chat/completions", "https://ai.example.com/v1/chat/completions")]
    public void ResolveUrl_Normalizes(String input, String expected) => Assert.Equal(expected, AIService.ResolveUrl(input));

    [Fact(DisplayName = "未配置对话地址时返回空字符串")]
    public void ResolveUrl_Empty() => Assert.Equal("", AIService.ResolveUrl(""));

    [Fact(DisplayName = "未启用AI对话时拒绝调用")]
    public async Task Chat_Disabled_Throws()
    {
        using var scope = UseDefaultSettings();
        StarServerSetting.Current.EnableAI = false;

        var service = new AIService(new AIStubFactory(new AIStubHandler()));
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ChatAsync(NewRequest(), "app1", "127.0.0.1", CancellationToken.None));

        Assert.Equal(ApiCode.Forbidden, ex.Code);
    }

    [Fact(DisplayName = "未配置上游地址或默认模型时拒绝调用")]
    public async Task Chat_NotConfigured_Throws()
    {
        using var scope = UseDefaultSettings();
        StarServerSetting.Current.AIUrl = "";

        var service = new AIService(new AIStubFactory(new AIStubHandler()));
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ChatAsync(NewRequest(), "app1", null, CancellationToken.None));

        Assert.Equal(ApiCode.InternalServerError, ex.Code);
    }

    [Fact(DisplayName = "消息为空时拒绝调用")]
    public async Task Chat_EmptyMessages_Throws()
    {
        using var scope = UseDefaultSettings();

        var service = new AIService(new AIStubFactory(new AIStubHandler()));
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ChatAsync(new AIChatRequest(), "app1", null, CancellationToken.None));

        Assert.Equal(ApiCode.BadRequest, ex.Code);
    }

    [Fact(DisplayName = "消息内容为空时拒绝调用")]
    public async Task Chat_EmptyContent_Throws()
    {
        using var scope = UseDefaultSettings();

        var service = new AIService(new AIStubFactory(new AIStubHandler()));
        var request = new AIChatRequest { Messages = [new AIChatMessage { Content = " " }] };
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ChatAsync(request, "app1", null, CancellationToken.None));

        Assert.Equal(ApiCode.BadRequest, ex.Code);
    }

    [Fact(DisplayName = "请求非白名单模型时拒绝调用")]
    public async Task Chat_ModelNotAllowed_Throws()
    {
        using var scope = UseDefaultSettings();
        StarServerSetting.Current.AIModels = "deepseek-chat,glm-4";

        var service = new AIService(new AIStubFactory(new AIStubHandler()));
        var request = NewRequest();
        request.Model = "gpt-5";

        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ChatAsync(request, "app1", null, CancellationToken.None));

        Assert.Equal(ApiCode.BadRequest, ex.Code);
    }

    [Fact(DisplayName = "调用成功时按OpenAI协议组装请求并解析回复")]
    public async Task Chat_Success_SendsRequestAndParsesResponse()
    {
        var handler = new AIStubHandler { ResponseBody = SuccessBody };
        using var scope = UseDefaultSettings();
        StarServerSetting.Current.AISystemPrompt = "你是星尘助手";

        var service = new AIService(new AIStubFactory(handler));
        var request = NewRequest("写一个快速排序");
        request.Temperature = 0.2;
        request.MaxTokens = 256;

        var rs = await service.ChatAsync(request, "coder", "10.0.0.1", CancellationToken.None);

        Assert.Equal("https://ai.example.com/v1/chat/completions", handler.RequestUri);
        Assert.Equal("Bearer sk-test", handler.Authorization);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var root = doc.RootElement;
        Assert.Equal("deepseek-chat", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("coder", root.GetProperty("user").GetString());
        Assert.Equal(0.2, root.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(256, root.GetProperty("max_tokens").GetInt32());

        // 平台默认系统提示词插入到消息最前面
        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("你是星尘助手", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("写一个快速排序", messages[1].GetProperty("content").GetString());

        Assert.Equal("你好，我是AI", rs.Content);
        Assert.Equal("deepseek-chat", rs.Model);
        Assert.Equal("stop", rs.FinishReason);
        Assert.Equal(12, rs.Usage?.InputTokens);
        Assert.Equal(8, rs.Usage?.OutputTokens);
        Assert.Equal(20, rs.Usage?.TotalTokens);
        Assert.True(rs.Elapsed >= 0);
    }

    [Fact(DisplayName = "请求自带系统提示词时覆盖平台默认提示词")]
    public async Task Chat_RequestSystemPrompt_Wins()
    {
        var handler = new AIStubHandler { ResponseBody = SuccessBody };
        using var scope = UseDefaultSettings();
        StarServerSetting.Current.AISystemPrompt = "平台默认";

        var service = new AIService(new AIStubFactory(handler));
        await service.ChatAsync(NewRequest("hi", "自定义系统提示词"), "app1", null, CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal("自定义系统提示词", messages[0].GetProperty("content").GetString());
        Assert.Equal(2, messages.GetArrayLength());
    }

    [Fact(DisplayName = "上游返回错误状态码时抛出带错误信息的异常")]
    public async Task Chat_UpstreamError_Throws()
    {
        var handler = new AIStubHandler
        {
            StatusCode = HttpStatusCode.Unauthorized,
            ResponseBody = """{"error":{"message":"invalid api key"}}""",
        };
        using var scope = UseDefaultSettings();

        var service = new AIService(new AIStubFactory(handler));
        var ex = await Assert.ThrowsAsync<ApiException>(() => service.ChatAsync(NewRequest(), "app1", null, CancellationToken.None));

        Assert.Equal(ApiCode.InternalServerError, ex.Code);
        Assert.Contains("invalid api key", ex.Message);
    }

    [Fact(DisplayName = "上游返回200但含error字段时按失败处理")]
    public void ParseResponse_ErrorBody_Throws()
    {
        var ex = Assert.Throws<ApiException>(() => AIService.ParseResponse("""{"error":{"message":"quota exceeded"}}""", 0));

        Assert.Equal(ApiCode.InternalServerError, ex.Code);
        Assert.Contains("quota exceeded", ex.Message);
    }

    [Fact(DisplayName = "上游返回非JSON时按失败处理")]
    public void ParseResponse_InvalidBody_Throws()
    {
        var ex = Assert.Throws<ApiException>(() => AIService.ParseResponse("<html>502</html>", 0));

        Assert.Equal(ApiCode.InternalServerError, ex.Code);
    }

    [Fact(DisplayName = "流式对话按SSE逐段返回增量并在结束段带结束原因")]
    public async Task Stream_Success_YieldsDeltas()
    {
        var handler = new AIStubHandler
        {
            ResponseBody = """
data: {"model":"deepseek-chat","choices":[{"delta":{"role":"assistant","content":"你"}}]}

data: {"choices":[{"delta":{"content":"好"}}]}

data: {"choices":[{"delta":{},"finish_reason":"stop"}]}

data: [DONE]

""",
        };
        using var scope = UseDefaultSettings();
        var service = new AIService(new AIStubFactory(handler));

        var list = new List<AIChatDelta>();
        await foreach (var item in service.StreamAsync(NewRequest("hi"), "app1", null, CancellationToken.None))
        {
            list.Add(item);
        }

        Assert.Equal(3, list.Count);
        Assert.Equal("你", list[0].Content);
        Assert.Equal("deepseek-chat", list[0].Model);
        Assert.Equal("好", list[1].Content);
        Assert.Equal("stop", list[2].FinishReason);
        Assert.True(list[2].Elapsed >= 0);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("deepseek-chat", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact(DisplayName = "流式对话遇到data错误段时抛出上游异常")]
    public async Task Stream_ErrorChunk_Throws()
    {
        var handler = new AIStubHandler
        {
            ResponseBody = """data: {"error":{"message":"quota exceeded"}}""" + "\n\n",
        };
        using var scope = UseDefaultSettings();
        var service = new AIService(new AIStubFactory(handler));

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var _ in service.StreamAsync(NewRequest(), "app1", null, CancellationToken.None)) { }
        });

        Assert.Equal(ApiCode.InternalServerError, ex.Code);
        Assert.Contains("quota exceeded", ex.Message);
    }

    [Fact(DisplayName = "流式对话上游返回错误状态码时抛出异常")]
    public async Task Stream_UpstreamError_Throws()
    {
        var handler = new AIStubHandler
        {
            StatusCode = HttpStatusCode.TooManyRequests,
            ResponseBody = """{"error":{"message":"rate limited"}}""",
        };
        using var scope = UseDefaultSettings();
        var service = new AIService(new AIStubFactory(handler));

        var ex = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var _ in service.StreamAsync(NewRequest(), "app1", null, CancellationToken.None)) { }
        });

        Assert.Equal(ApiCode.InternalServerError, ex.Code);
        Assert.Contains("rate limited", ex.Message);
    }
}
