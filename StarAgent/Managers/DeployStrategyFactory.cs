using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>部署策略工厂</summary>
/// <remarks>
/// 根据部署模式创建对应的策略实例。
/// 支持新旧模式的兼容转换：
/// - 新版模式值10+：Standard(10)/Shadow(11)/Hosted(12)/Task(13)
/// - 旧版模式值0-4：Default(0)/Extract(1)/ExtractAndRun(2)/RunOnce(3)/Multiple(4)
/// </remarks>
public static class DeployStrategyFactory
{
    /// <summary>根据部署模式创建策略</summary>
    /// <param name="mode">部署模式</param>
    /// <returns>部署策略实例</returns>
    public static IDeployStrategy Create(DeployMode mode)
    {
        var value = (Int32)mode;

        // 新版模式 10+
        if (value >= 10)
        {
            return mode switch
            {
                DeployMode.Standard => new StandardDeployStrategy(),
                DeployMode.Shadow => new ShadowDeployStrategy(),
                DeployMode.Hosted => new HostedStrategy(),
                DeployMode.Task => new TaskStrategy(),
                _ => new StandardDeployStrategy(),
            };
        }

        // 旧版模式 0-4，映射到新版策略
        // 0=Default -> Shadow（旧版默认使用影子目录）
        // 1=Extract -> Hosted（仅解压）
        // 2=ExtractAndRun -> Standard（解压到工作目录运行）
        // 3=RunOnce -> Task（一次性任务）
        // 4=Multiple -> Shadow（多实例，AllowMultiple另外处理）
        return value switch
        {
            0 => new ShadowDeployStrategy(),
            1 => new HostedStrategy(),
            2 => new StandardDeployStrategy(),
            3 => new TaskStrategy(),
            4 => new ShadowDeployStrategy(),
            _ => new ShadowDeployStrategy(),
        };
    }

    /// <summary>根据服务信息创建策略</summary>
    /// <param name="service">服务信息</param>
    /// <returns>部署策略实例</returns>
    public static IDeployStrategy Create(ServiceInfo service)
    {
        if (service == null) return new StandardDeployStrategy();

        return Create(service.Mode);
    }

    /// <summary>判断是否允许多实例</summary>
    /// <param name="service">服务信息</param>
    /// <returns>是否允许多实例</returns>
    public static Boolean IsMultipleAllowed(ServiceInfo service)
    {
        if (service == null) return false;

        // 优先使用新属性
        if (service.AllowMultiple) return true;

        // 兼容旧模式 Multiple=4
        return (Int32)service.Mode == 4;
    }

    /// <summary>判断是否需要守护进程</summary>
    /// <param name="mode">部署模式</param>
    /// <returns>是否需要守护</returns>
    public static Boolean NeedGuardian(DeployMode mode)
    {
        var value = (Int32)mode;

        // 新版模式
        if (value >= 10)
        {
            return mode == DeployMode.Standard || mode == DeployMode.Shadow;
        }

        // 旧版模式：0/2/4需要守护，1/3不需要
        return value == 0 || value == 2 || value == 4;
    }

    /// <summary>获取实际部署模式。将旧版模式转换为新版模式</summary>
    /// <param name="mode">原始模式</param>
    /// <returns>新版部署模式</returns>
    public static DeployMode GetActualMode(DeployMode mode)
    {
        var value = (Int32)mode;

        // 已经是新版模式
        if (value >= 10) return mode;

        // 旧版模式转换
        return value switch
        {
            0 => DeployMode.Shadow,
            1 => DeployMode.Hosted,
            2 => DeployMode.Standard,
            3 => DeployMode.Task,
            4 => DeployMode.Shadow,
            _ => DeployMode.Shadow,
        };
    }
}
