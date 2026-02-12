using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NewLife;
using NewLife.Log;

namespace Stardust.Managers;

/// <summary>健康检查助手</summary>
/// <remarks>
/// 支持三种健康检查方式：
/// 1. HTTP：http://localhost:6680/health - 访问接口，200响应表示健康
/// 2. TCP：tcp://localhost:6600 - 连接端口，成功表示健康
/// 3. UDP：udp://localhost:5500 - 发送数据，收到响应表示健康
/// </remarks>
public static class HealthCheckHelper
{
    /// <summary>执行健康检查</summary>
    /// <param name="healthCheck">健康检查地址，支持http/tcp/udp协议</param>
    /// <param name="timeout">超时时间（毫秒），默认5000ms</param>
    /// <param name="log">日志对象</param>
    /// <returns>是否健康</returns>
    public static Boolean Check(String? healthCheck, Int32 timeout = 5000, ILog? log = null)
    {
        if (healthCheck.IsNullOrEmpty()) return true;

        try
        {
            if (healthCheck.StartsWithIgnoreCase("http://", "https://"))
                return CheckHttp(healthCheck, timeout, log);
            else if (healthCheck.StartsWithIgnoreCase("tcp://"))
                return CheckTcp(healthCheck, timeout, log);
            else if (healthCheck.StartsWithIgnoreCase("udp://"))
                return CheckUdp(healthCheck, timeout, log);
            else
            {
                log?.Warn("不支持的健康检查协议: {0}", healthCheck);
                return false;
            }
        }
        catch (Exception ex)
        {
            log?.Error("健康检查异常: {0} - {1}", healthCheck, ex.Message);
            return false;
        }
    }

    /// <summary>HTTP健康检查</summary>
    private static Boolean CheckHttp(String url, Int32 timeout, ILog? log)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
        try
        {
            var response = client.GetAsync(url).Result;
            var isHealthy = response.StatusCode == HttpStatusCode.OK;
            
            if (!isHealthy)
                log?.Warn("HTTP健康检查失败: {0} - {1}", url, response.StatusCode);
            
            return isHealthy;
        }
        catch (Exception ex)
        {
            log?.Warn("HTTP健康检查失败: {0} - {1}", url, ex.GetTrue()?.Message);
            return false;
        }
    }

    /// <summary>TCP健康检查</summary>
    private static Boolean CheckTcp(String address, Int32 timeout, ILog? log)
    {
        // 解析地址：tcp://localhost:6600
        var uri = new Uri(address);
        var host = uri.Host;
        var port = uri.Port;

        if (port <= 0)
        {
            log?.Warn("TCP健康检查失败: 无效的端口 - {0}", address);
            return false;
        }

        using var client = new TcpClient();
        try
        {
            var task = client.ConnectAsync(host, port);
            if (task.Wait(timeout))
            {
                return client.Connected;
            }
            else
            {
                log?.Warn("TCP健康检查超时: {0}", address);
                return false;
            }
        }
        catch (Exception ex)
        {
            log?.Warn("TCP健康检查失败: {0} - {1}", address, ex.GetTrue()?.Message);
            return false;
        }
    }

    /// <summary>UDP健康检查</summary>
    private static Boolean CheckUdp(String address, Int32 timeout, ILog? log)
    {
        // 解析地址：udp://localhost:5500
        var uri = new Uri(address);
        var host = uri.Host;
        var port = uri.Port;

        if (port <= 0)
        {
            log?.Warn("UDP健康检查失败: 无效的端口 - {0}", address);
            return false;
        }

        using var client = new UdpClient();
        try
        {
            // 连接到目标地址
            client.Connect(host, port);
            
            // 发送探测数据（PING）
            var data = "PING"u8.ToArray();
            client.Send(data, data.Length);

            // 设置接收超时
            client.Client.ReceiveTimeout = timeout;

            // 等待响应
            IPEndPoint? remoteEP = null;
            var response = client.Receive(ref remoteEP);

            // 收到任何响应都认为是健康的
            return response != null && response.Length > 0;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            log?.Warn("UDP健康检查超时: {0}", address);
            return false;
        }
        catch (Exception ex)
        {
            log?.Warn("UDP健康检查失败: {0} - {1}", address, ex.GetTrue()?.Message);
            return false;
        }
    }

    /// <summary>异步执行健康检查</summary>
    /// <param name="healthCheck">健康检查地址</param>
    /// <param name="timeout">超时时间（毫秒）</param>
    /// <param name="log">日志对象</param>
    /// <returns>是否健康</returns>
    public static async Task<Boolean> CheckAsync(String? healthCheck, Int32 timeout = 5000, ILog? log = null)
    {
        if (healthCheck.IsNullOrEmpty()) return true;

        try
        {
            if (healthCheck.StartsWithIgnoreCase("http://", "https://"))
                return await CheckHttpAsync(healthCheck, timeout, log).ConfigureAwait(false);
            else if (healthCheck.StartsWithIgnoreCase("tcp://"))
                return await CheckTcpAsync(healthCheck, timeout, log).ConfigureAwait(false);
            else if (healthCheck.StartsWithIgnoreCase("udp://"))
                return await CheckUdpAsync(healthCheck, timeout, log).ConfigureAwait(false);
            else
            {
                log?.Warn("不支持的健康检查协议: {0}", healthCheck);
                return false;
            }
        }
        catch (Exception ex)
        {
            log?.Error("健康检查异常: {0} - {1}", healthCheck, ex.Message);
            return false;
        }
    }

    /// <summary>异步HTTP健康检查</summary>
    private static async Task<Boolean> CheckHttpAsync(String url, Int32 timeout, ILog? log)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeout) };
        try
        {
            var response = await client.GetAsync(url).ConfigureAwait(false);
            var isHealthy = response.StatusCode == HttpStatusCode.OK;
            
            if (!isHealthy)
                log?.Warn("HTTP健康检查失败: {0} - {1}", url, response.StatusCode);
            
            return isHealthy;
        }
        catch (Exception ex)
        {
            log?.Warn("HTTP健康检查失败: {0} - {1}", url, ex.GetTrue()?.Message);
            return false;
        }
    }

    /// <summary>异步TCP健康检查</summary>
    private static async Task<Boolean> CheckTcpAsync(String address, Int32 timeout, ILog? log)
    {
        var uri = new Uri(address);
        var host = uri.Host;
        var port = uri.Port;

        if (port <= 0)
        {
            log?.Warn("TCP健康检查失败: 无效的端口 - {0}", address);
            return false;
        }

        using var client = new TcpClient();
        try
        {
#if NET5_0_OR_GREATER
            using var cts = new CancellationTokenSource(timeout);
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
#else
            var task = client.ConnectAsync(host, port);
            if (!task.Wait(timeout))
            {
                log?.Warn("TCP健康检查超时: {0}", address);
                return false;
            }
#endif
            return client.Connected;
        }
#if NET5_0_OR_GREATER
        catch (OperationCanceledException)
        {
            log?.Warn("TCP健康检查超时: {0}", address);
            return false;
        }
#endif
        catch (Exception ex)
        {
            log?.Warn("TCP健康检查失败: {0} - {1}", address, ex.GetTrue()?.Message);
            return false;
        }
    }

    /// <summary>异步UDP健康检查</summary>
    private static async Task<Boolean> CheckUdpAsync(String address, Int32 timeout, ILog? log)
    {
        var uri = new Uri(address);
        var host = uri.Host;
        var port = uri.Port;

        if (port <= 0)
        {
            log?.Warn("UDP健康检查失败: 无效的端口 - {0}", address);
            return false;
        }

        using var client = new UdpClient();
        try
        {
            client.Connect(host, port);
            
            var data = "PING"u8.ToArray();
            await client.SendAsync(data, data.Length).ConfigureAwait(false);

            client.Client.ReceiveTimeout = timeout;

#if NET6_0_OR_GREATER
            using var cts = new CancellationTokenSource(timeout);
            var result = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);
            return result.Buffer != null && result.Buffer.Length > 0;
#else
            // 旧版本没有带 CancellationToken 的 ReceiveAsync
            IPEndPoint? remoteEP = null;
            var task = Task.Run(() => client.Receive(ref remoteEP));
            if (await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) == task)
            {
                var response = await task.ConfigureAwait(false);
                return response != null && response.Length > 0;
            }
            else
            {
                log?.Warn("UDP健康检查超时: {0}", address);
                return false;
            }
#endif
        }
#if NET6_0_OR_GREATER
        catch (OperationCanceledException)
        {
            log?.Warn("UDP健康检查超时: {0}", address);
            return false;
        }
#endif
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            log?.Warn("UDP健康检查超时: {0}", address);
            return false;
        }
        catch (Exception ex)
        {
            log?.Warn("UDP健康检查失败: {0} - {1}", address, ex.GetTrue()?.Message);
            return false;
        }
    }
}
