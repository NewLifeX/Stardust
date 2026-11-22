using NewLife;
using NewLife.Log;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust;
using Stardust.Deployment;

namespace StarAgent.Managers;

/// <summary>证书同步指令。平台下发 cert/sync 后，节点拉取证书文件落盘并重载 Nginx</summary>
internal static class CertSyncCommand
{
    /// <summary>执行证书同步</summary>
    /// <param name="cmd">平台指令</param>
    /// <param name="client">星尘客户端，用于拉取证书清单</param>
    /// <returns></returns>
    public static async Task<CommandReplyModel> SyncAsync(CommandModel cmd, StarClient client)
    {
        var arg = cmd.Argument?.ToJsonEntity<CertSyncArg>();
        var certId = arg?.CertId ?? 0;

        try
        {
            var certs = await client.GetCerts(certId).ConfigureAwait(false);
            if (certs == null || certs.Length == 0) return Error(L.T("No certificate is associated with this node."));

            var manager = CertManager.Current;
            manager.Log ??= XTrace.Log;

            var rs = await manager.SyncAsync(certs, client.BuildUrl, certId == 0).ConfigureAwait(false);

            // 证书变化后重载 Nginx，使新证书立即生效
            var reload = rs.Changed && NginxDeploy.Reload();

            var names = rs.Certs.Select(e => e.Name).Join(",");
            var message = L.T("Certificates synchronized: {0}; changed={1}; nginxReload={2}", names, rs.Changed, reload);
            return new CommandReplyModel { Status = CommandStatus.已完成, Data = message };
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    static CommandReplyModel Error(String message) => new() { Status = CommandStatus.错误, Data = message };
}

/// <summary>证书同步参数</summary>
internal class CertSyncArg
{
    /// <summary>证书编号。0 表示同步该节点全部关联证书</summary>
    public Int32 CertId { get; set; }
}
