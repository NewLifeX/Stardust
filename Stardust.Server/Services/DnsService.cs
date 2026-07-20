using NewLife;
using NewLife.Log;
using NewLife.Threading;
using Stardust.Data.Nodes;
using Stardust.Services;

namespace Stardust.Server.Services;

/// <summary>DDNS核心服务。检测节点IP变化并自动更新DNS记录</summary>
public class DnsService : IDisposable
{
    #region 属性
    private readonly DnsProviderFactory _factory;
    private readonly ITracer _tracer;
    private TimerX? _timer;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = XTrace.Log;
    #endregion

    #region 构造
    /// <summary>实例化DDNS核心服务</summary>
    /// <param name="factory"></param>
    /// <param name="tracer"></param>
    public DnsService(DnsProviderFactory factory, ITracer tracer)
    {
        _factory = factory;
        _tracer = tracer;

        // 定时全量刷新，每10分钟检查一次
        _timer = new TimerX(DoRefreshAll, null, 60_000, 600_000) { Async = true };
    }

    /// <summary>释放</summary>
    public void Dispose()
    {
        _timer.TryDispose();
        _timer = null;
    }
    #endregion

    #region 方法
    /// <summary>检测节点IP变化并触发DNS更新。由NodeService在登录/心跳时调用</summary>
    /// <param name="node">节点</param>
    /// <param name="ip">当前公网IP</param>
    /// <param name="oldIp">旧IP。登录时因Login已更新LastLoginIP，需传入登录前旧值</param>
    public async Task CheckNodeIPChange(Node node, String ip, String? oldIp = null)
    {
        if (node == null || ip.IsNullOrEmpty()) return;
        if (node.Domains.IsNullOrEmpty()) return;

        // 使用传入的旧IP对比，否则用数据库中的LastLoginIP
        oldIp ??= node.LastLoginIP;

        // IP未变化，无需更新
        if (ip == oldIp) return;

        Log.Info("节点[{0}] IP已变化：{1} -> {2}，开始更新DNS", node.Name, oldIp, ip);

        await RefreshNodeDomainsAsync(node, ip);
    }

    /// <summary>刷新指定节点的所有域名</summary>
    /// <param name="nodeId">节点ID</param>
    public async Task RefreshNodeDomainsAsync(Int32 nodeId)
    {
        var node = Node.FindByID(nodeId);
        if (node == null) return;

        await RefreshNodeDomainsAsync(node, null);
    }

    /// <summary>刷新指定节点的所有域名</summary>
    /// <param name="node">节点</param>
    /// <param name="ip">IP地址，为空时使用节点最后登录IP</param>
    public async Task RefreshNodeDomainsAsync(Node node, String? ip = null)
    {
        if (node == null || node.Domains.IsNullOrEmpty()) return;

        ip ??= node.LastLoginIP;
        if (ip.IsNullOrEmpty())
        {
            Log.Warn("节点[{0}] 没有IP地址，无法更新DNS", node.Name);
            return;
        }

        var domains = node.Domains.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (domains.Length == 0) return;

        using var span = _tracer?.NewSpan("DnsRefreshNode", new { node.Name, node.Domains, ip });

        foreach (var domain in domains)
        {
            await RefreshDomainAsync(node, domain, ip);
        }
    }

    /// <summary>刷新单个域名记录</summary>
    /// <param name="node">节点</param>
    /// <param name="domainName">完整域名</param>
    /// <param name="ip">IP地址</param>
    public async Task<Boolean> RefreshDomainAsync(Node node, String domainName, String ip)
    {
        if (node == null || domainName.IsNullOrEmpty() || ip.IsNullOrEmpty()) return false;

        using var span = _tracer?.NewSpan("DnsRefreshDomain", new { node.Name, domainName, ip });

        // 查找匹配的凭据。根据域名后缀匹配 DomainProvider.Domain
        var rootDomain = GetRootDomain(domainName);
        var credential = DomainProvider.FindAllWithCache()
            .FirstOrDefault(e => e.Enable && !e.Domain.IsNullOrEmpty() && domainName.EndsWithIgnoreCase(e.Domain));

        if (credential == null)
        {
            Log.Warn("域名[{0}] 未找到匹配的域名供应商配置（根域名：{1}），跳过更新", domainName, rootDomain);
            return false;
        }

        var provider = _factory.GetProvider(credential.Provider);
        if (provider == null)
        {
            Log.Warn("域名[{0}] 不支持的供应商类型：{1}", domainName, credential.Provider);
            return false;
        }

        Log.Info("更新DNS记录：{0} -> {1}（供应商：{2}，凭据：{3}）", domainName, ip, credential.Provider, credential.Name);

        // 直接传递凭据实体（实现IDnsConfig），供应商内部按需读取配置
        var result = await provider.UpdateRecordAsync(credential, domainName, ip);

        if (result)
        {
            Log.Info("DNS更新成功：{0} -> {1}", domainName, ip);
        }
        else
        {
            Log.Error("DNS更新失败：{0} -> {1}", domainName, ip);
        }

        return result;
    }
    #endregion

    #region 定时刷新
    private async Task DoRefreshAll(Object? state)
    {
        using var span = _tracer?.NewSpan("DnsRefreshAll");

        // 查找所有有域名配置的已启用节点
        var nodes = Node.FindAllWithCache()
            .Where(e => e.Enable && !e.Domains.IsNullOrEmpty() && !e.LastLoginIP.IsNullOrEmpty())
            .ToList();

        Log.Debug("定时刷新DNS：共 {0} 个节点有域名配置", nodes.Count);

        foreach (var node in nodes)
        {
            try
            {
                await RefreshNodeDomainsAsync(node, null);
            }
            catch (Exception ex)
            {
                Log.Error("刷新节点[{0}] DNS异常：{1}", node.Name, ex.Message);
            }
        }
    }
    #endregion

    #region 辅助
    /// <summary>从完整域名中提取根域名</summary>
    /// <param name="domainName">完整域名，如 sh05.newlifex.com</param>
    /// <returns>根域名，如 newlifex.com</returns>
    private static String GetRootDomain(String domainName)
    {
        if (domainName.IsNullOrEmpty()) return domainName;

        var p = domainName.IndexOf('.');
        if (p < 0) return domainName;

        return domainName[(p + 1)..];
    }
    #endregion
}
