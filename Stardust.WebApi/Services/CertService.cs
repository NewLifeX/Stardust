using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Data.Platform;

namespace Stardust.Web.Services;

/// <summary>证书下发服务。把证书文件推送到关联节点，并回写下发状态</summary>
public class CertService(StarFactory starFactory, ITracer? tracer)
{
    /// <summary>下发证书到关联节点</summary>
    /// <param name="cert">证书</param>
    /// <param name="links">证书节点关联</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下发结果统计</returns>
    public async Task<CertPushResult> PushAsync(SslCertificate cert, IList<SslCertificateNode> links, CancellationToken cancellationToken = default)
    {
        var result = new CertPushResult();
        if (cert == null || links == null) return result;

        using var span = tracer?.NewSpan("Cert-Push", cert.Domain);
        span?.AppendTag($"nodes={links.Count}");

        var args = new { certId = cert.Id }.ToJson();
        var ts = new List<(SslCertificateNode link, Task<CommandReplyModel?> task)>();
        foreach (var link in links)
        {
            var node = link.Node;
            if (node == null || node.Code.IsNullOrEmpty())
            {
                link.Status = L.T("Node not found.");
                link.LastPush = DateTime.Now;
                link.Update();
                result.Failed++;
                continue;
            }

            ts.Add((link, starFactory.SendNodeCommandAsync(node.Code, "cert/sync", args, 0, 600, 20, cancellationToken)));
        }

        await Task.WhenAll(ts.Select(e => e.task));

        foreach (var (link, task) in ts)
        {
            var reply = task.Result;
            link.LastPush = DateTime.Now;
            if (reply == null)
            {
                link.Status = L.T("Timed out waiting for the node to respond.");
                result.Timeout++;
            }
            else if (reply.Status == CommandStatus.错误)
            {
                link.Status = reply.Data + "";
                result.Failed++;
            }
            else
            {
                link.Status = reply.Data + "";
                link.Version = cert.GetVersion();
                result.Success++;
            }
            link.Update();
        }

        cert.Fix();

        span?.AppendTag($"success={result.Success}; failed={result.Failed}; timeout={result.Timeout}");

        return result;
    }

    /// <summary>把证书关联到全部启用节点</summary>
    /// <param name="cert">证书</param>
    /// <returns>新增的关联数</returns>
    public Int32 AddAllNodes(SslCertificate cert)
    {
        if (cert == null) return 0;

        var count = 0;
        foreach (var node in ProjectScopeRules.FilterVisible(Stardust.Data.Nodes.Node.FindAll()))
        {
            if (!node.Enable) continue;

            var link = SslCertificateNode.FindByCertIdAndNodeId(cert.Id, node.ID);
            if (link != null)
            {
                if (!link.Enable)
                {
                    link.Enable = true;
                    link.Update();
                }
                continue;
            }

            link = new SslCertificateNode
            {
                CertId = cert.Id,
                NodeId = node.ID,
                IP = node.IP,
                Enable = true,
            };
            count += link.Insert();
        }

        cert.Fix();

        return count;
    }
}

/// <summary>证书下发结果</summary>
public class CertPushResult
{
    /// <summary>成功数</summary>
    public Int32 Success { get; set; }

    /// <summary>失败数</summary>
    public Int32 Failed { get; set; }

    /// <summary>超时数</summary>
    public Int32 Timeout { get; set; }

    /// <summary>总数</summary>
    public Int32 Total => Success + Failed + Timeout;
}
