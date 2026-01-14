using System.ComponentModel;

namespace Stardust.Models;

/// <summary>部署模式</summary>
/// <remarks>
/// 定义应用的部署和运行方式：
/// - Default: 默认模式，兼容旧版，等同于Shadow
/// - Standard: 解压到工作目录，运行进程，守护进程（推荐）
/// - Shadow: 解压到影子目录，运行进程，守护进程
/// - Hosted: 解压到工作目录，由外部宿主托管运行（IIS/静态站点）
/// - Task: 运行一次后退出，不守护（一次性任务）
/// 
/// 新版模式值从10开始，与旧版ServiceModes(0-4)区分。
/// 客户端收到0-9时按旧版处理，10+按新版处理。
/// </remarks>
public enum DeployMode
{
    /// <summary>默认模式。兼容旧版配置，运行时等同于Shadow模式</summary>
    /// <remarks>
    /// 用于兼容旧版配置文件和旧版服务端。
    /// 实际执行时按Shadow模式处理（解压到影子目录）。
    /// </remarks>
    [Description("默认模式")]
    Default = 0,

    /// <summary>标准模式。解压到工作目录，运行进程，守护进程</summary>
    /// <remarks>
    /// 推荐模式。zip包解压到工作目录，直接运行。
    /// 简单直接，适合大多数应用场景。
    /// </remarks>
    [Description("标准模式")]
    Standard = 10,

    /// <summary>影子模式。解压到影子目录，运行进程，守护进程</summary>
    /// <remarks>
    /// 工作目录保持干净，仅存放配置和数据文件。
    /// 可执行文件在影子目录中，支持热更新时不影响运行中的进程。
    /// </remarks>
    [Description("影子模式")]
    Shadow = 11,

    /// <summary>托管模式。解压到工作目录，由外部宿主托管运行</summary>
    /// <remarks>
    /// 适用于IIS托管的Web应用、前端静态站点等场景。
    /// 由外部宿主（如IIS、Nginx）负责运行应用。
    /// </remarks>
    [Description("托管模式")]
    Hosted = 12,

    /// <summary>任务模式。运行一次后完成，不守护进程</summary>
    /// <remarks>
    /// 适用于初始化脚本、数据迁移、定时任务等场景。
    /// 运行完成后自动禁用，不会重复执行。
    /// </remarks>
    [Description("任务模式")]
    Task = 13,
}

/// <summary>部署模式扩展</summary>
public static class DeployModesExtensions
{
    /// <summary>是否为新版部署模式</summary>
    /// <param name="mode">部署模式</param>
    /// <returns>是否为新版模式</returns>
    public static Boolean IsNewVersion(this DeployMode mode) => (Int32)mode >= 10;

    /// <summary>转换为服务模式</summary>
    public static ServiceModes Convert(DeployMode mode)
    {
        return mode switch
        {
            DeployMode.Default => ServiceModes.Default,
            DeployMode.Standard => ServiceModes.ExtractAndRun,
            DeployMode.Shadow => ServiceModes.Default,
            DeployMode.Hosted => ServiceModes.Extract,
            DeployMode.Task => ServiceModes.RunOnce,
            _ => ServiceModes.Default,
        };
    }

    /// <summary>转换为部署模式</summary>
    public static DeployMode Convert(ServiceModes mode)
    {
        return mode switch
        {
            ServiceModes.Default => DeployMode.Shadow,
            ServiceModes.ExtractAndRun => DeployMode.Standard,
            ServiceModes.Extract => DeployMode.Hosted,
            ServiceModes.RunOnce => DeployMode.Task,
            ServiceModes.Multiple => DeployMode.Shadow,
            _ => DeployMode.Shadow,
        };
    }
}