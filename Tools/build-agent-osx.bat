@echo off
rem 构建 macOS 自包含星尘代理包（osx-x64 / osx-arm64）
rem 用法：在 Tools 目录执行 build-agent-osx.bat

set base=..\Bin\Agent
rem 使用脚本所在目录的绝对路径，避免 pushd 后相对路径失效
set clover=%~dp0clover.exe
if not exist "%clover%" set clover=%~dp0..\..\Tools\clover.exe

call :build osx-x64
call :build osx-arm64
goto :eof

:build
set rid=%1
set out=%base%\%rid%
echo === 发布 %rid% ===
if exist "%out%" rmdir /s/q "%out%"
dotnet publish "..\StarAgent\StarAgent.csproj" --nologo -c Release -f net10.0 -r %rid% --self-contained -o "%out%" -p:TargetFrameworks=net10.0
if errorlevel 1 exit /b 1

del /f/s/q "%out%\*.pdb" 2>nul
del /f/s/q "%out%\*.xml" 2>nul

pushd "%out%"
"%clover%" tar "..\staragent10-%rid%.tar.gz" *
"%clover%" zip "..\staragent10-%rid%.zip" *
popd
goto :eof
