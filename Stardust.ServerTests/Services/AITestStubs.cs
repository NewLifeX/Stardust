using System.Net;
using System.Text;
using Stardust.Server;

namespace ServerTest.Services;

/// <summary>AI测试用的HttpClient工厂。所有请求交给固定桩处理器</summary>
internal sealed class AIStubFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <summary>创建Http客户端</summary>
    /// <param name="name">客户端名称</param>
    /// <returns>使用桩处理器的Http客户端</returns>
    public HttpClient CreateClient(String name) => new(handler, false);
}

/// <summary>AI测试用的Http桩处理器。记录请求并返回预置响应</summary>
internal sealed class AIStubHandler : HttpMessageHandler
{
    /// <summary>请求地址</summary>
    public String? RequestUri { get; private set; }

    /// <summary>请求体</summary>
    public String? RequestBody { get; private set; }

    /// <summary>授权头</summary>
    public String? Authorization { get; private set; }

    /// <summary>响应状态码</summary>
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>响应正文</summary>
    public String ResponseBody { get; set; } = "";

    /// <summary>发送请求</summary>
    /// <param name="request">请求消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预置响应</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri + "";
        RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Authorization = request.Headers.Authorization?.ToString();

        return new HttpResponseMessage(StatusCode) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
    }
}

/// <summary>临时修改平台设置，用完还原，避免污染其它测试</summary>
internal sealed class AISettingScope : IDisposable
{
    private readonly StarServerSetting _set = StarServerSetting.Current;
    private readonly Boolean _enable;
    private readonly String _url;
    private readonly String _key;
    private readonly String _model;
    private readonly String _models;
    private readonly String _system;
    private readonly Int32 _timeout;
    private readonly Double _temperature;
    private readonly Int32 _maxTokens;

    /// <summary>实例化并应用设置</summary>
    /// <param name="apply">设置修改委托</param>
    public AISettingScope(Action<StarServerSetting> apply)
    {
        _enable = _set.EnableAI;
        _url = _set.AIUrl;
        _key = _set.AIKey;
        _model = _set.AIModel;
        _models = _set.AIModels;
        _system = _set.AISystemPrompt;
        _timeout = _set.AITimeout;
        _temperature = _set.AITemperature;
        _maxTokens = _set.AIMaxTokens;

        apply(_set);
    }

    /// <summary>还原设置</summary>
    public void Dispose()
    {
        _set.EnableAI = _enable;
        _set.AIUrl = _url;
        _set.AIKey = _key;
        _set.AIModel = _model;
        _set.AIModels = _models;
        _set.AISystemPrompt = _system;
        _set.AITimeout = _timeout;
        _set.AITemperature = _temperature;
        _set.AIMaxTokens = _maxTokens;
    }
}
