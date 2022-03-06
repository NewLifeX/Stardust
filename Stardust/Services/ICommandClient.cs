using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Services
{
    /// <summary>命令服务接口</summary>
    public interface ICommandClient
    {
        /// <summary>收到命令时触发</summary>
        event EventHandler<CommandEventArgs> Received;

        /// <summary>命令集合</summary>
        IDictionary<String, Delegate> Commands { get; }
    }

    /// <summary>命令客户端助手</summary>
    public static class CommandClientHelper
    {
        /// <summary>
        /// 注册服务。收到平台下发的服务调用时，执行注册的方法
        /// </summary>
        /// <param name="client">命令客户端</param>
        /// <param name="command"></param>
        /// <param name="method"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void RegisterCommand(this ICommandClient client, String command, Func<String, String> method)
        {
            if (command.IsNullOrEmpty()) command = method.Method.Name;

            client.Commands[command] = method;
        }

        /// <summary>
        /// 注册服务。收到平台下发的服务调用时，执行注册的方法
        /// </summary>
        /// <param name="client">命令客户端</param>
        /// <param name="command"></param>
        /// <param name="method"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void RegisterCommand(this ICommandClient client, String command, Func<CommandModel, String> method)
        {
            if (command.IsNullOrEmpty()) command = method.Method.Name;

            client.Commands[command] = method;
        }

        /// <summary>执行命令</summary>
        /// <param name="client">命令客户端</param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static CommandReplyModel ExecuteCommand(this ICommandClient client, CommandModel model)
        {
            using var span = DefaultTracer.Instance?.NewSpan("ExecuteCommand", $"{model.Command}({model.Argument})");
            var rs = new CommandReplyModel { Id = model.Id, Status = CommandStatus.已完成 };
            try
            {
                var result = OnCommand(client, model);
                rs.Data = result?.ToJson();
                return rs;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);

                XTrace.WriteException(ex);

                rs.Data = ex.Message;
                if (ex is ApiException aex && aex.Code == 400)
                    rs.Status = CommandStatus.取消;
                else
                    rs.Status = CommandStatus.错误;
            }

            return rs;
        }

        /// <summary>分发执行服务</summary>
        /// <param name="client">命令客户端</param>
        /// <param name="model"></param>
        private static Object OnCommand(ICommandClient client, CommandModel model)
        {
            //WriteLog("OnCommand {0}", model.ToJson());

            if (!client.Commands.TryGetValue(model.Command, out var d)) throw new ApiException(400, $"找不到服务[{model.Command}]");

            if (d is Func<String, String> func) return func(model.Argument);
            if (d is Func<CommandModel, String> func2) return func2(model);

            return null;
        }
    }
}