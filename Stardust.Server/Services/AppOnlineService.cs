using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Models;

namespace Stardust.Server.Services;

/// <summary>应用在线服务。管理应用实例的在线状态、会话缓存和心跳保活</summary>
public class AppOnlineService(ICacheProvider cacheProvider, ITracer tracer)
{
    private readonly ICache _cache = cacheProvider.InnerCache;

    /// <summary>获取或创建应用在线会话。按 ClientId → Token → IP 顺序匹配</summary>
    /// <param name="app">应用</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="token">访问令牌</param>
    /// <param name="localIp">本机IP</param>
    /// <param name="ip">远程IP</param>
    /// <returns>在线会话和是否新建</returns>
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
            using var span = tracer.NewSpan("GetOnlineForSingleton", new { app.Name, localIp, clientId, ip });

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

    /// <summary>按客户端标识获取在线会话。优先从缓存读取，未命中时查数据库</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>在线会话，未找到时返回 null</returns>
    public AppOnline GetOnline(String clientId)
    {
        if (clientId.IsNullOrEmpty()) return null;

        if (_cache.TryGetValue<AppOnline>(clientId, out var online)) return online;

        online = AppOnline.FindByClient(clientId);

        if (online != null) _cache.Set(clientId, online, 600);

        return online;
    }

    /// <summary>从缓存中删除在线记录</summary>
    /// <param name="clientId">客户端标识</param>
    /// <returns>是否成功</returns>
    public Boolean RemoveOnline(String clientId)
    {
        if (clientId.IsNullOrEmpty()) return false;

        return _cache.Remove(clientId) > 0;
    }

    /// <summary>更新在线状态。更新会话信息和关联节点</summary>
    /// <param name="app">应用</param>
    /// <param name="clientId">客户端标识</param>
    /// <param name="ip">远程IP</param>
    /// <param name="token">访问令牌</param>
    /// <param name="info">应用信息（可选）</param>
    /// <returns>更新后的在线会话</returns>
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
    /// <summary>根据 IP 匹配节点。优先本机IP，再查远程IP</summary>
    /// <param name="localIp">本机IP</param>
    /// <param name="ip">远程IP</param>
    /// <returns>匹配的节点，未找到返回 null</returns>
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

    /// <summary>获取或创建节点。根据应用信息查找已注册节点，未找到时自动注册</summary>
    /// <param name="inf">应用信息</param>
    /// <param name="localIp">本机IP</param>
    /// <param name="ip">远程IP</param>
    /// <returns>匹配或创建的节点，未找到返回 null</returns>
    public Node GetOrAddNode(AppInfo inf, String localIp, String ip)
    {
        // 根据节点IP规则，自动创建节点
        var rule = NodeResolver.Instance.Match(null, localIp);
        if (rule != null && rule.NewNode)
        {
            using var span = tracer?.NewSpan("AddNodeForApp", rule);

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