using System.Net;
using NewLife;

namespace StarAgent.WebPanel;

/// <summary>Web面板绑定地址解析</summary>
/// <remarks>
/// StarAgent 覆盖 NewLife.Agent 面板的监听地址：默认仅本机 127.0.0.1，
/// 配置为 * 时监听所有网卡，也可指定具体IP；无法识别的值回退本机，避免配置笔误把管理面板误开放到局域网。
/// </remarks>
public static class WebBindHelper
{
    /// <summary>解析绑定地址</summary>
    /// <param name="bind">绑定地址。支持具体IP地址，或 * 表示监听所有网卡</param>
    /// <returns>要绑定的地址；null 表示监听所有网卡</returns>
    public static IPAddress? Parse(String? bind)
    {
        if (bind.IsNullOrEmpty()) return IPAddress.Loopback;

        bind = bind.Trim();
        if (bind == "*") return null;

        if (IPAddress.TryParse(bind, out var address)) return address.IsAny() ? null : address;

        return IPAddress.Loopback;
    }
}
