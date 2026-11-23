namespace StarAgent;

/// <summary>操作系统电源命令参数构造</summary>
public static class PowerCommands
{
    /// <summary>构造 Windows 重启命令参数</summary>
    /// <remarks>
    /// 必须单次带 /f 下发（#173）。先不带 /f 排定关机、稍后再补发 -f 会被系统以
    /// 1190「已计划系统关机」拒绝，而缺少 /f 时挂住不响应 WM_ENDSESSION 的应用会把关机流程卡死。
    /// /f 只强制结束不响应的应用进程，服务停止顺序与文件落盘仍按正常关机流程执行。
    /// </remarks>
    /// <param name="timeout">延迟秒数，小于等于零或为空表示立即重启</param>
    /// <returns>shutdown.exe 的命令行参数</returns>
    public static String GetWindowsRebootArgs(Int32? timeout) => timeout > 0 ? $"-r -f -t {timeout}" : "-r -f -t 0";
}
