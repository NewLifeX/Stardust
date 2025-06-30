using Stardust.Managers;

namespace Stardust.Models;

/// <summary>应用服务集合信息</summary>
public class ServicesInfo
{
    /// <summary>
    /// 应用服务集合
    /// </summary>
    public ServiceInfo[]? Services { get; set; }

    /// <summary>正在运行的应用服务信息</summary>
    public ProcessInfo[]? RunningServices { get; set; }
}

/// <summary>
/// 应用服务操作结果
/// </summary>
public class ServiceOperationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public Boolean Success { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public String? Message { get; set; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public String? ServiceName { get; set; }
}