using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services;

public class AppOnlineService
{
    private readonly ITracer _tracer;
    private readonly ICache _cache;

    public AppOnlineService(ICacheProvider cacheProvider, ITracer tracer)
    {
        _cache = cacheProvider.InnerCache;
        _tracer = tracer;
    }

    public (AppOnline, Boolean isNew) GetOnline(App app, String clientId, String token, String localIp, String ip)
    {
        if (app == null) return (null, false);

        // 找到在线会话，先查ClientId和Token。客户端刚启动时可能没有拿到本机IP，而后来心跳拿到了
        var online = GetOnline(clientId) ?? AppOnline.FindByToken(token);

        if (localIp.IsNullOrEmpty() && !clientId.IsNullOrEmpty())
        {
            var p = clientId.IndexOf('@');
            if (p > 0) localIp = clientId[..p];
        }

        // 如果是每节点单例部署，则使用本地IP作为会话匹配。可能是应用重启，前一次会话还在
        if (online == null && app.Singleton && !localIp.IsNullOrEmpty())
        {
            using var span = _tracer.NewSpan("GetOnlineForSingleton", new { app.Name, localIp, clientId, ip });

            // 要求内网IP与外网IP都匹配，才能认为是相同会话，因为有可能不同客户端部署在各自内网而具有相同本地IP
            var list = AppOnline.FindAllByAppAndIP(app.Id, localIp);
            online = list.FirstOrDefault(e => e.Client == clientId);
            online ??= list.OrderBy(e => e.Id).FirstOrDefault(e => e.UpdateIP == ip);
            span?.AppendTag($"该应用内网IP[{localIp}]共有应用在线[{list.Count}]个");

            // 处理多IP
            if (online == null)
            {
                list = AppOnline.FindAllByApp(app.Id);
                online = list.OrderBy(e => e.Id).FirstOrDefault(e => !e.IP.IsNullOrEmpty() && e.UpdateIP == ip && e.IP.Split(",").Contains(localIp));
            }

            if (online != null) span?.AppendTag($"匹配到在线[id={online.Id}]");
        }

        var isNew = online == null;

        // 早期客户端没有clientId
        online ??= AppOnline.GetOrAddClient(clientId) ?? AppOnline.GetOrAddClient(ip, token);

        if (online != null)
        {
            online.AppId = app.Id;
            online.Name = app.ToString();
            online.Category = app.Category;
            online.PingCount++;

            if (!clientId.IsNullOrEmpty()) online.Client = clientId;
            if (!token.IsNullOrEmpty()) online.Token = token;
            if (!localIp.IsNullOrEmpty()) online.IP = localIp;
            if (online.CreateIP.IsNullOrEmpty()) online.CreateIP = ip;
            if (!ip.IsNullOrEmpty()) online.UpdateIP = ip;

            // 更新跟踪标识
            var traceId = DefaultSpan.Current?.TraceId;
            if (!traceId.IsNullOrEmpty()) online.TraceId = traceId;

            // 本地IP
            if (!localIp.IsNullOrEmpty()) online.IP = localIp;
        }

        return (online, isNew);
    }

    AppOnline GetOnline(String clientId)
    {
        if (clientId.IsNullOrEmpty()) return null;

        if (_cache.TryGetValue<AppOnline>(clientId, out var online)) return online;

        online = AppOnline.FindByClient(clientId);

        if (online != null) _cache.Set(clientId, online, 600);

        return online;
    }

    /// <summary>
    /// 更新在线状态
    /// </summary>
    /// <param name="app"></param>
    /// <param name="clientId"></param>
    /// <param name="ip"></param>
    /// <param name="token"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public AppOnline UpdateOnline(App app, String clientId, String ip, String token, AppInfo info = null)
    {
        var (online, isNew) = GetOnline(app, clientId, token, info?.IP, ip);
        if (online == null) return null;

        // 关联节点
        if (online.NodeId <= 0)
        {
            var node = GetNodeByIP(online.IP, ip);
            node ??= GetOrAddNode(info, online.IP, ip);
            if (node != null)
            {
                online.NodeId = node.ID;

                if (node.ProductCode.IsNullOrEmpty() || node.ProductCode == "App")
                {
                    node.ProductCode = info?.Name;
                }

                // 更新最后活跃时间
                if (node.LastActive.AddMinutes(10) < DateTime.Now) node.LastActive = DateTime.Now;

                node.Update();
            }
        }

        online.Fill(app, info);
        online.SaveAsync();

        if (isNew)
            app.WriteHistory("关联上线", true, info.ToJson(), info?.Version, ip, clientId);

        return online;
    }

    private Node GetNodeByIP(String localIp, String ip)
    {
        if (localIp.IsNullOrEmpty()) return null;

        // 借助缓存，降低IP搜索节点次数
        var key = $"{localIp}-{ip}";
        if (_cache.TryGetValue<Node>(key, out var node)) return node;

        // 根据本地IP找出所有符合节点，再找一个公网IP匹配的
        var list = Node.SearchByIP(localIp);
        node = list.FirstOrDefault(e => e.UpdateIP == ip);
        node ??= list.FirstOrDefault();

        _cache.Add(key, node, 3600);

        return node;
    }

    /// <summary>检查或添加节点。主要服务于仅有跟踪数据的客户端接入</summary>
    /// <param name="inf"></param>
    /// <param name="localIp"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public Node GetOrAddNode(AppInfo inf, String localIp, String ip)
    {
        // 根据节点IP规则，自动创建节点
        var rule = NodeResolver.Instance.Match(null, localIp);
        if (rule != null && rule.NewNode)
        {
            using var span = _tracer?.NewSpan("AddNodeForApp", rule);

            var nodes = Node.SearchByIP(localIp);
            if (nodes.Count > 0) return nodes[0];

            var node = new Node
            {
                Code = Rand.NextString(8),
                Name = inf?.MachineName ?? rule.Name,
                ProductCode = inf?.Name ?? "App",
                Category = rule.Category,
                IP = localIp,
                Enable = true,

                CreateIP = ip,
                UpdateIP = ip,
            };
            if (inf != null)
            {
                node.Version = inf.Version;
                if (!inf.MachineName.IsNullOrEmpty())
                {
                    if (node.Name.IsNullOrEmpty() || node.Name == node.MachineName) node.Name = inf.MachineName;
                    node.MachineName = inf.MachineName;
                }
                node.UserName = inf.UserName;
            }
            if (node.Name.IsNullOrEmpty()) node.Name = rule.Category;
            if (node.Name.IsNullOrEmpty()) node.Name = inf?.Name;
            if (node.Name.IsNullOrEmpty()) node.Name = node.Code;
            node.Insert();

            node.WriteHistory("AppAddNode", true, inf.ToJson(), ip);

            return node;
        }

        return null;
    }
}