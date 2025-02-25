using System.Net.WebSockets;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using Stardust.Data;

namespace Stardust.Server.Services;

public class AppSessionManager : SessionManager
{
    public AppSessionManager(IServiceProvider serviceProvider) : base(serviceProvider) => Topic = "AppCommands";
}

class AppCommandSession(WebSocket socket) : WsCommandSession(socket)
{
    public Action<String, Boolean, String> WriteLog { get; set; }

    public override Task HandleAsync(CommandModel command, String message, CancellationToken cancellationToken)
    {
        if (command == null || command.Id == 0 || command.Expire.Year > 2000 && command.Expire < DateTime.UtcNow)
        {
            WriteLog("WebSocket发送", false, "消息无效或已过期。" + message);

            var log = AppCommand.FindById((Int32)command.Id);
            if (log != null)
            {
                log.Status = CommandStatus.取消;
                log.Update();
            }

            return Task.CompletedTask;
        }

        {
            WriteLog("WebSocket发送", true, message);

            var log = AppCommand.FindById((Int32)command.Id);
            if (log != null)
            {
                log.Times++;
                log.Status = CommandStatus.处理中;
                log.UpdateTime = DateTime.Now;
                log.Update();
            }
        }

        return base.HandleAsync(command, message, cancellationToken);
    }
}
