using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ClientTest.TestInfrastructure;

internal static class TestEnvironment
{
    public static Boolean CanGet(String url, Int32 timeout = 2_000) => CanGetAsync(url, timeout).GetAwaiter().GetResult();

    public static async Task<Boolean> CanGetAsync(String url, Int32 timeout = 2_000)
    {
        if (String.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        // 先做 TCP 探测。目标端口未监听时立即返回，避免 HttpClient 连接失败前的额外等待
        var connectTimeout = IsLoopback(uri.Host) ? Math.Min(timeout, 300) : timeout;
        if (!await CanConnectAsync(uri.Host, uri.Port, connectTimeout).ConfigureAwait(false)) return false;

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

            return (Int32)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>探测目标主机端口是否可连接</summary>
    /// <param name="host">主机名或IP</param>
    /// <param name="port">端口</param>
    /// <param name="timeout">超时时间，单位毫秒</param>
    /// <returns>是否可连接</returns>
    public static async Task<Boolean> CanConnectAsync(String host, Int32 port, Int32 timeout = 300)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeout);
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Boolean IsLoopback(String host) =>
        String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1" || host == "[::1]";
}
