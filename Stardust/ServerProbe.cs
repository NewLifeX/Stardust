using System.Collections.Concurrent;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;

namespace Stardust;

/// <summary>服务端探测。登录前从匿名接口 /api 取服务端时间与平台默认语言</summary>
/// <remarks>
/// 客户端用服务端时间生成鉴权盐值，而首次登录前还没有机会从登录响应拿到 ServerTime。
/// 这里提前请求一次服务端匿名健康接口 <c>/api</c>，取得服务端 UTC 毫秒时间戳写入客户端时间差，
/// 使首次登录的盐值就落在服务端的 SaltTime 窗口内；同时取回平台默认语言，让登录前的界面与提示也能用平台语言。
/// <para>等待结果最多 <see cref="WaitTimeout"/>（默认 1 秒），而 HTTP 请求本身允许 <see cref="HttpTimeout"/>（默认 10 秒）。
/// 等待超时不会取消请求，结果留给下一次登录直接使用——时间出错是小概率事件，不能因此拖慢所有正常客户端。</para>
/// <para>探测失败不影响正常登录：保持原有时间差，按本机时间签名，与改动前行为一致。本类不会修改系统时钟。</para>
/// </remarks>
public static class ServerProbe
{
    /// <summary>等待探测结果的最长时间。超过该时间不阻塞登录，请求继续在后台完成</summary>
    public static TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>HTTP 请求超时。与等待时间解耦，慢网络下请求仍可完成并把结果留给下次登录</summary>
    public static TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>探测失败后的重试间隔。登录失败会反复重试，避免每次登录都发起一次 HTTP 请求</summary>
    public static TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(10);

    private static readonly ConcurrentDictionary<String, Task<ServerInfo?>> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<String, ServerInfo> _results = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<String, DateTime> _lastTries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>获取服务端信息</summary>
    /// <param name="server">服务端地址，支持逗号分隔的多个地址</param>
    /// <param name="force">是否忽略重试间隔</param>
    /// <returns>服务端信息；无法获取时返回null</returns>
    public static ServerInfo? GetServerInfo(String? server, Boolean force = false)
    {
        var url = GetApiUrl(server);
        if (url.IsNullOrEmpty()) return null;

        // 探测成功过的地址直接复用，不再发起请求
        if (!force && _results.TryGetValue(url, out var cached)) return cached;

        // 复用上一次仍在进行的请求。登录重试时接着等同一个请求，不重复发起
        if (!_tasks.TryGetValue(url, out var task) || task.IsCompleted)
        {
            if (!force && _lastTries.TryGetValue(url, out var last) && last.Add(RetryInterval) > DateTime.Now) return null;

            _lastTries[url] = DateTime.Now;

            // 请求独立于等待：等待超时后请求继续执行，结果写入缓存供下一次登录直接使用
            task = _tasks[url] = Task.Run(() => Fetch(url));
        }

        // 最多等待 WaitTimeout，超时返回空且不取消请求
        try
        {
            if (!task.Wait(WaitTimeout)) return null;
        }
        catch
        {
            return null;
        }

        var rs = task.Result;
        if (rs != null) _results[url] = rs;

        return rs;
    }

    private static ServerInfo? Fetch(String url)
    {
        try
        {
            using var http = new HttpClient { Timeout = HttpTimeout };

            var json = http.GetStringAsync(url).GetAwaiter().GetResult();

            return Parse(json);
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("Server probe failed: {0} {1}", url, ex.Message);

            return null;
        }
    }

    /// <summary>构造服务端探测地址</summary>
    /// <param name="server">服务端地址，支持逗号分隔的多个地址</param>
    /// <returns>探测地址；无法解析时返回null</returns>
    public static String? GetApiUrl(String? server)
    {
        if (server.IsNullOrEmpty()) return null;

        // 与 ApiHttpClient 保持一致：服务端地址支持逗号分隔的多个节点，这里取第一个
        var url = server.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (url.IsNullOrEmpty()) return null;

        return url.TrimEnd('/') + "/api";
    }

    /// <summary>从 /api 响应中解析服务端信息</summary>
    /// <param name="json">响应文本</param>
    /// <returns>服务端信息；解析不到时返回null</returns>
    public static ServerInfo? Parse(String? json)
    {
        if (json.IsNullOrEmpty()) return null;

        try
        {
            var dic = JsonParser.Decode(json);
            if (dic == null) return null;

            var info = new ServerInfo();
            foreach (var item in dic)
            {
                if (item.Key.EqualIgnoreCase("serverTime"))
                    info.ServerTime = item.Value.ToLong();
                else if (item.Key.EqualIgnoreCase("defaultLanguage"))
                    info.DefaultLanguage = item.Value as String;
            }

            // 时间与语言都没有时视为探测失败（例如反向代理返回的错误页）
            if (info.ServerTime <= 0 && info.DefaultLanguage.IsNullOrEmpty()) return null;

            return info;
        }
        catch
        {
            // 非 JSON 响应（反代错误页等）视为探测失败
        }

        return null;
    }
}