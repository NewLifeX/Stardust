namespace Stardust.Services;

/// <summary>事件客户端</summary>
public interface IEventProvider
{
    /// <summary>写事件</summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="remark"></param>
    Boolean WriteEvent(String type, String name, String remark);
}

/// <summary>事件客户端助手</summary>
public static class EventProviderHelper
{
    /// <summary>写信息事件</summary>
    /// <param name="client"></param>
    /// <param name="name"></param>
    /// <param name="remark"></param>
    public static void WriteInfoEvent(this IEventProvider client, String name, String remark) => client.WriteEvent("info", name, remark);

    /// <summary>写错误事件</summary>
    /// <param name="client"></param>
    /// <param name="name"></param>
    /// <param name="remark"></param>
    public static void WriteErrorEvent(this IEventProvider client, String name, String remark) => client.WriteEvent("error", name, remark);
}