using System.Collections.Concurrent;
using NewLife;
using Stardust.Data;

namespace Stardust.Server.Controllers;

/// <summary>WebSocket 连接历史过滤器</summary>
/// <remarks>
/// 客户端与服务端心跳节奏不匹配时会在数十秒内反复重连，每次连接都会写入“连接/断开”两条历史，
/// 短时间内即可让 AppHistory/NodeHistory 暴涨，并让在线实例数虚高。
/// 同一会话在 <see cref="Window"/> 秒内的重复连接/断开只保留第一条，断开记录追加本次连接时长，
/// 便于区分“连接被服务端或代理按心跳超时断开”（约 60~90 秒）与“客户端主动重连”（数秒）。
/// </remarks>
public static class WebSocketHistoryFilter
{
    private const String Connected = HistoryAction.WebSocketConnected;
    private const String Disconnected = HistoryAction.WebSocketDisconnected;
    private const String ConnectedCn = "WebSocket连接";
    private const String DisconnectedCn = "WebSocket断开";

    /// <summary>去重窗口。同一会话在该时间内的重复连接/断开只记录一次，单位秒</summary>
    public static Int32 Window { get; set; } = 60;

    private static readonly ConcurrentDictionary<String, DateTime> _lastWrite = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<String, DateTime> _connectTime = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>判断是否连接/断开动作</summary>
    /// <param name="action">动作名称</param>
    /// <returns>是否连接或断开</returns>
    public static Boolean IsConnectionAction(String action) =>
        action.EqualIgnoreCase(Connected, Disconnected, ConnectedCn, DisconnectedCn);

    /// <summary>过滤重复的连接历史</summary>
    /// <param name="clientId">会话标识。为空时按动作名去重</param>
    /// <param name="action">动作名称</param>
    /// <param name="message">消息内容。断开时追加本次连接时长</param>
    /// <returns>true 表示需要写入；false 表示窗口内重复，应丢弃</returns>
    public static Boolean TryAccept(String clientId, String action, ref String message)
    {
        if (!IsConnectionAction(action)) return true;

        var now = DateTime.Now;
        var isDisconnect = action.EqualIgnoreCase(Disconnected, DisconnectedCn);

        // 连接时长必须在去重之前维护，否则被抑制的那次重连会把时长算到上一次连接头上
        if (!clientId.IsNullOrEmpty())
        {
            if (isDisconnect)
            {
                if (_connectTime.TryRemove(clientId, out var start))
                    message += $", Elapsed={(Int32)(now - start).TotalSeconds}s";
            }
            else
            {
                _connectTime[clientId] = now;
            }
        }

        var key = clientId.IsNullOrEmpty() ? action : clientId + "#" + action;
        if (_lastWrite.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(Window)) return false;

        _lastWrite[key] = now;
        Cleanup(now);

        return true;
    }

    /// <summary>清空状态。单元测试使用</summary>
    public static void Reset()
    {
        _lastWrite.Clear();
        _connectTime.Clear();
    }

    private static void Cleanup(DateTime now)
    {
        if (_lastWrite.Count <= 1024) return;

        var expire = now - TimeSpan.FromSeconds(Window * 10);
        foreach (var item in _lastWrite)
        {
            if (item.Value < expire) _lastWrite.TryRemove(item.Key, out _);
        }
        foreach (var item in _connectTime)
        {
            if (item.Value < expire) _connectTime.TryRemove(item.Key, out _);
        }
    }
}
