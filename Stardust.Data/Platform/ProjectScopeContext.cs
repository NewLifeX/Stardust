using System;
using System.Threading;

namespace Stardust.Data.Platform;

/// <summary>项目数据范围上下文。由 Web 请求中间件设置，控制器数据权限读取</summary>
/// <remarks>
/// 仅在 Web/WebApi 宿主中由中间件赋值；服务端数据面与后台任务不设置上下文，数据权限自动放行。
/// 使用 AsyncLocal 保存，避免并发请求之间串数据。
/// </remarks>
public static class ProjectScopeContext
{
    private static readonly AsyncLocal<ProjectScope> _current = new();

    /// <summary>当前项目范围。为空表示不启用项目过滤</summary>
    public static ProjectScope Current { get => _current.Value; set => _current.Value = value; }
}
