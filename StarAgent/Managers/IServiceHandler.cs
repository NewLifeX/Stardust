using Stardust.Models;

namespace Stardust.Managers;

/// <summary>服务处理器接口</summary>
public interface IServiceHandler
{
    /// <summary>启动服务</summary>
    /// <returns></returns>
    Boolean Start(ServiceInfo service);

    /// <summary>停止服务</summary>
    /// <param name="reason"></param>
    void Stop(String reason);

    /// <summary>检查服务是否正常</summary>
    /// <returns></returns>
    Boolean Check();
}
