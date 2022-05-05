using NewLife;
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

    public AppOnlineService(ITracer tracer) => _tracer = tracer;

    public (AppOnline, Boolean isNew) GetOnline(App app, String clientId, String token, String localIp, String ip)
    {
        if (app == null) return (null, false);

        // 找到在线会话，先查ClientId和Token。客户端刚启动时可能没有拿到本机IP，而后来心跳拿到了
        var online = AppOnline.FindByClient(clientId) ?? AppOnline.FindByToken(token);

        if (localIp.IsNullOrEmpty() && !clientId.IsNullOrEmpty())
        {
            var p = clientId.IndexOf('@');
            if (p > 0) localIp = clientId[..p];
        }

        // 如果是每节点单例部署，则使用本地IP作为会话匹配。可能是应用重启，前一次会话还在
        if (online == null && app.Singleton && !localIp.IsNullOrEmpty())
        {
            using var span = _tracer.NewSpan("GetOnlineForSingleton", localIp);

            // 要求内网IP与外网IP都匹配，才能认为是相同会话，因为有可能不同客户端部署在各自内网而具有相同本地IP
            var list = AppOnline.FindAllByIP(localIp);
            online = list.OrderBy(e => e.Id).FirstOrDefault(e => e.AppId == app.Id && e.UpdateIP == ip);

            // 处理多IP
            if (online == null)
            {
                list = AppOnline.FindAllByApp(app.Id);
                online = list.OrderBy(e => e.Id).FirstOrDefault(e => !e.IP.IsNullOrEmpty() && e.IP.Contains(localIp) && e.UpdateIP == ip);
            }

            if (span != null && online != null) span.SetError(null, online);
        }

        var isNew = online == null;

        // 早期客户端没有clientId
        if (online == null) online = AppOnline.GetOrAddClient(clientId) ?? AppOnline.GetOrAddClient(ip, token);

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
        if (online.NodeId == 0)
        {
            var node = Node.SearchByIP(online.IP).FirstOrDefault();
            if (node == null) node = GetOrAddNode(info, online.IP, ip);
            if (node != null)
                online.NodeId = node.ID;
            else
                online.NodeId = -1;
        }

        online.Fill(app, info);
        online.SaveAsync();

        if (isNew)
            app.WriteHistory("关联上线", true, info.ToJson(), info?.Version, ip, clientId);

        return online;
    }

    public Node GetOrAddNode(AppInfo inf, String localIp, String ip)
    {
        // 根据节点IP规则，自动创建节点
        var rule = NodeResolver.Instance.Match(null, localIp);
        if (rule != null && rule.NewNode)
        {
            using var span = _tracer?.NewSpan("AddNodeForApp", rule);

            var nodes = Node.SearchByIP(localIp);
            if (nodes.Count == 0)
            {
                var node = new Node
                {
                    Code = Rand.NextString(8),
                    Name = rule.Name,
                    ProductCode = "App",
                    Category = rule.Category,
                    IP = localIp,
                    Enable = true,
                };
                if (inf != null)
                {
                    node.Version = inf.Version;
                    node.MachineName = inf.MachineName;
                    node.UserName = inf.UserName;
                }
                if (node.Name.IsNullOrEmpty()) node.Name = rule.Category;
                if (node.Name.IsNullOrEmpty()) node.Name = inf?.Name;
                if (node.Name.IsNullOrEmpty()) node.Name = node.Code;
                node.Insert();

                node.WriteHistory("AppAddNode", true, inf.ToJson(), ip);

                return node;
            }
        }

        return null;
    }
}