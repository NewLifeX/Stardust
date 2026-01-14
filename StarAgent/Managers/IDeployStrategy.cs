using System.Diagnostics;
using NewLife.Log;
using Stardust.Models;

namespace StarAgent.Managers;

/// <summary>部署策略接口</summary>
/// <remarks>
/// 定义不同部署模式的行为，由策略实现类负责具体逻辑：
/// - 解压部署包到目标目录
/// - 执行应用进程
/// - 决定是否需要守护和接管
/// </remarks>
public interface IDeployStrategy
{
    /// <summary>部署模式</summary>
    DeployMode Mode { get; }

    /// <summary>是否需要守护进程</summary>
    /// <remarks>Standard和Shadow模式需要守护，Hosted和Task不需要</remarks>
    Boolean NeedGuardian { get; }

    /// <summary>是否允许接管已存在的进程</summary>
    /// <remarks>多实例模式不接管，避免误管理其它实例</remarks>
    Boolean AllowTakeOver { get; }

    /// <summary>解压部署包</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>是否成功</returns>
    Boolean Extract(DeployContext context);

    /// <summary>执行应用</summary>
    /// <param name="context">部署上下文</param>
    /// <returns>启动的进程，失败返回null</returns>
    Process? Execute(DeployContext context);
}

/// <summary>部署上下文</summary>
/// <remarks>
/// 封装部署过程中需要的所有信息，避免方法参数过多。
/// 策略实现通过上下文获取配置并记录结果。
/// </remarks>
public class DeployContext
{
    #region 属性
    /// <summary>服务名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>应用编码。用于星尘平台标识</summary>
    public String? AppId { get; set; }

    /// <summary>服务信息</summary>
    public ServiceInfo Service { get; set; } = null!;

    /// <summary>部署信息。来自服务端的部署配置</summary>
    public DeployInfo? Deploy { get; set; }

    /// <summary>工作目录。应用运行的工作目录</summary>
    public String WorkingDirectory { get; set; } = null!;

    /// <summary>Zip文件路径。部署包的完整路径</summary>
    public String? ZipFile { get; set; }

    /// <summary>可执行文件路径。解压后找到的启动文件</summary>
    public String? ExecuteFile { get; set; }

    /// <summary>影子目录。Shadow模式下存放可执行文件的目录</summary>
    public String? Shadow { get; set; }

    /// <summary>启动参数</summary>
    public String? Arguments { get; set; }

    /// <summary>是否允许多实例</summary>
    public Boolean AllowMultiple { get; set; }

    /// <summary>启动挂钩。是否注入星尘监控</summary>
    public Boolean StartupHook { get; set; }

    /// <summary>启动等待时间。毫秒</summary>
    public Int32 StartWait { get; set; } = 3000;

    /// <summary>是否调试模式</summary>
    public Boolean Debug { get; set; }

    /// <summary>最后错误信息</summary>
    public String? LastError { get; set; }
    #endregion

    #region 日志
    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}
