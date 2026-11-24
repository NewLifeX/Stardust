@echo off
rem 兼容旧入口：新发布流程统一由 pack-agent.ps1 完成（AOT 三 RID + any 全目标）。
rem 用法：pack-agent.bat [-SkipAny] [-SkipAot] [-SlimAot] [-AotRids win-x64,linux-x64]
pwsh -NoProfile -File "%~dp0pack-agent.ps1" %*
exit /b %ERRORLEVEL%
