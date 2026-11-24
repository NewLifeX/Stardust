using System.Net;
using System.Net.Sockets;
using NewLife;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Data.Nodes;

namespace Stardust.Server.Services;

/// <summary>节点唤醒服务。挑选同网段在线节点代发 Wake-on-LAN 魔术包</summary>
/// <remarks>
/// 广播只能在二层广播域内生效，因此由目标所在局域网内的在线节点代发。
/// 代发节点按“网关 MAC → 网关 IP → 公网 IP → 私有网段 /24”逐级兜底挑选。
/// </remarks>
/// <param name="starFactory">星尘工厂，用于向代发节点下发命令</param>
public class NodeWakeService(StarFactory starFactory)
{
    private readonly StarFactory _starFactory = starFactory;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = XTrace.Log;

    /// <summary>最多挑选的代发节点数</summary>
    public Int32 MaxRelays { get; set; } = 5;

    /// <summary>查找同网段的在线节点</summary>
    /// <param name="target">目标节点</param>
    /// <returns>代发节点在线记录，没有则返回空列表</returns>
    public virtual IList<NodeOnline> FindRelays(Node target)
    {
        if (target == null || target.MACs.IsNullOrEmpty()) return [];

        // 在线表由后台清理过期记录，这里再加时间窗，避免清理滞后时把早已离线的节点当成代发节点
        var onlines = NodeOnline.FindAllOnline(DateTime.Now.AddMinutes(-30));
        if (onlines.Count == 0) return [];

        var nodes = Node.FindAllByIds(onlines.Select(e => e.NodeID));

        return SelectRelays(target, onlines, nodes, MaxRelays);
    }

    /// <summary>向同网段在线节点下发唤醒命令</summary>
    /// <param name="target">目标节点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>代发节点数，0 表示没有可用的代发节点</returns>
    public virtual async Task<Int32> WakeAsync(Node target, CancellationToken cancellationToken = default)
    {
        var relays = FindRelays(target);
        if (relays.Count == 0) return 0;

        var args = new { macs = target.MACs, code = target.Code, name = target.Name }.ToJson();
        var count = 0;
        foreach (var online in relays)
        {
            var node = Node.FindByID(online.NodeID);
            if (node == null || node.Code.IsNullOrEmpty()) continue;

            try
            {
                // fire-and-forget：不等节点回包，唤醒结果由目标是否重新上线体现
                var reply = await _starFactory.SendNodeCommandAsync(node.Code, "node/wakeonlan", args, 0, 60, 0, cancellationToken).ConfigureAwait(false);
                if (reply != null) count++;
            }
            catch (Exception ex)
            {
                // 单个代发节点无权限或不可达时，不影响其余节点
                Log.Error("Failed to dispatch Wake-on-LAN to relay [{0}]: {1}", node.Name, ex.Message);
            }
        }

        if (count > 0) Log.Info("Wake-on-LAN for node [{0}] dispatched to {1} relay node(s)", target.Name, count);

        return count;
    }

    /// <summary>挑选同网段代发节点</summary>
    /// <param name="target">目标节点</param>
    /// <param name="onlines">在线记录</param>
    /// <param name="nodes">在线记录对应的节点</param>
    /// <param name="max">最多返回数量</param>
    /// <returns>代发节点在线记录</returns>
    public static IList<NodeOnline> SelectRelays(Node target, IList<NodeOnline> onlines, IList<Node> nodes, Int32 max = 5)
    {
        var result = new List<NodeOnline>();
        if (target == null || onlines == null || onlines.Count == 0) return result;

        var map = new Dictionary<Int32, Node>();
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                if (node != null && !map.ContainsKey(node.ID)) map[node.ID] = node;
            }
        }

        var candidates = new List<RelayCandidate>();
        foreach (var online in onlines)
        {
            if (online == null || online.NodeID <= 0 || online.NodeID == target.ID) continue;

            // 代发节点必须与目标同项目，避免把目标 MAC 与节点信息暴露给其它项目的节点命令记录
            if (target.ProjectId > 0 && online.ProjectId != target.ProjectId) continue;

            // 同一节点可能存在多个会话，只保留一条
            if (candidates.Any(e => e.Online.NodeID == online.NodeID)) continue;

            map.TryGetValue(online.NodeID, out var node);
            var gateway = !online.Gateway.IsNullOrEmpty() ? online.Gateway : node?.Gateway;
            ParseGateway(gateway, out var gwIp, out var gwMac);

            var ips = new List<IPAddress>();
            foreach (var ip in ParseIps(online.IP))
            {
                if (!ips.Contains(ip)) ips.Add(ip);
            }
            if (node != null)
            {
                foreach (var ip in ParseIps(node.IP))
                {
                    if (!ips.Contains(ip)) ips.Add(ip);
                }
            }

            candidates.Add(new RelayCandidate
            {
                Online = online,
                Node = node,
                GatewayIp = gwIp,
                GatewayMac = gwMac,
                Ips = ips,
            });
        }
        if (candidates.Count == 0) return result;

        ParseGateway(target.Gateway, out var targetGwIp, out var targetGwMac);
        var targetIps = ParseIps(target.IP);

        var tier = new List<RelayCandidate>();
        if (!targetGwMac.IsNullOrEmpty())
        {
            tier = candidates.Where(e => !e.GatewayMac.IsNullOrEmpty() && e.GatewayMac.EqualIgnoreCase(targetGwMac)).ToList();
        }
        if (tier.Count == 0 && !targetGwIp.IsNullOrEmpty())
        {
            tier = candidates.Where(e => e.GatewayIp.EqualIgnoreCase(targetGwIp)).ToList();
        }
        if (tier.Count == 0 && !target.LastLoginIP.IsNullOrEmpty())
        {
            // 同一 NAT 出口，家庭宽带常见；企业 NAT 下可能匹配到不同 VLAN，但广播不出网段，最多多发几个无用包
            tier = candidates.Where(e => e.Node != null && e.Node.LastLoginIP.EqualIgnoreCase(target.LastLoginIP)).ToList();
        }
        if (tier.Count == 0 && targetIps.Count > 0)
        {
            // 换路由器导致网关 MAC 变化，或部分 Linux 机器取不到网关 MAC 时，退回私有网段 /24 匹配
            tier = candidates.Where(e => e.Ips.Any(ip => targetIps.Any(t => SameSubnet(ip, t)))).ToList();
        }
        if (tier.Count == 0) return result;

        // 内网质量好的优先；GatewayLatency 为 0 表示未知，排在后面
        var count = max > 0 ? max : 5;
        foreach (var item in tier
            .OrderBy(e => e.Online.GatewayLatency > 0 ? e.Online.GatewayLatency : Int32.MaxValue)
            .ThenByDescending(e => e.Online.UpdateTime)
            .Take(count))
        {
            result.Add(item.Online);
        }

        return result;
    }

    private static void ParseGateway(String? gateway, out String? ip, out String? mac)
    {
        ip = null;
        mac = null;
        if (gateway.IsNullOrEmpty()) return;

        var parts = gateway.Split('/');
        ip = parts[0].Trim();
        if (ip.IsNullOrEmpty()) ip = null;
        if (parts.Length > 1) mac = NormalizeMac(parts[1]);
    }

    private static String? NormalizeMac(String? mac)
    {
        if (mac.IsNullOrEmpty()) return null;

        var text = mac.Replace("-", null).Replace(":", null).Replace(".", null).Trim();
        return text.IsNullOrEmpty() ? null : text.ToUpperInvariant();
    }

    private static IList<IPAddress> ParseIps(String? text)
    {
        var list = new List<IPAddress>();
        if (text.IsNullOrEmpty()) return list;

        foreach (var item in text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (IPAddress.TryParse(item, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork) list.Add(ip);
        }

        return list;
    }

    private static Boolean SameSubnet(IPAddress left, IPAddress right)
    {
        if (!IsPrivate(left) || !IsPrivate(right)) return false;

        var x = left.GetAddressBytes();
        var y = right.GetAddressBytes();
        if (x.Length != 4 || y.Length != 4) return false;

        return x[0] == y[0] && x[1] == y[1] && x[2] == y[2];
    }

    private static Boolean IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return false;

        // 10.0.0.0/8、172.16.0.0/12、192.168.0.0/16
        if (bytes[0] == 10) return true;
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
        if (bytes[0] == 192 && bytes[1] == 168) return true;

        return false;
    }

    private sealed class RelayCandidate
    {
        public NodeOnline Online { get; set; } = null!;
        public Node? Node { get; set; }
        public String? GatewayIp { get; set; }
        public String? GatewayMac { get; set; }
        public IList<IPAddress> Ips { get; set; } = [];
    }
}
