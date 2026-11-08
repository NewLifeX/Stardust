# 发布 StarAgent 的 NativeAOT 原生包
#
# 用法：
#   pwsh Tools/Scripts/publish-aot.ps1 -Rid win-x64
#   pwsh Tools/Scripts/publish-aot.ps1 -Rid linux-arm64 -Slim
#   # 交叉编译（WSL）：需 clang / lld / 对应 gcc 交叉工具链
#   pwsh Tools/Scripts/publish-aot.ps1 -Rid linux-arm64 -Compiler clang -ObjCopy aarch64-linux-gnu-objcopy
#   pwsh Tools/Scripts/publish-aot.ps1 -Rid linux-arm -Compiler clang -LinkerFlavor lld -ObjCopy arm-linux-gnueabihf-objcopy
#
# -Slim：只保留中英文日志与面板词表、去掉异常堆栈符号，体积再降约 3~4MB。
#        语言词表必须用环境变量传，属性才会传到被引用工程（NewLife.Core/Remoting/Agent/Stardust）。
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'linux-arm', 'osx-x64', 'osx-arm64')]
    [string]$Rid,

    [string]$Out,

    [switch]$Slim,

    [switch]$NoLocalSource,

    [string]$Compiler,

    [string]$ObjCopy,

    [string]$LinkerFlavor
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\..\StarAgent\StarAgent.csproj'
$project = (Resolve-Path $project).Path
if (-not $Out) { $Out = Join-Path (Split-Path $project -Parent) "..\.tmp\aot\$Rid" }

$publishArgs = @(
    'publish', $project,
    '-c', 'Release',
    '-f', 'net10.0',
    '-r', $Rid,
    '--self-contained', 'true',
    '-p:StarAgentAot=true',
    '-p:StripSymbols=true',
    '-o', $Out
)
if (-not $NoLocalSource) { $publishArgs += '-p:UseLocalNewLifeSource=true' }
if ($Compiler) { $publishArgs += "-p:CppCompilerAndLinker=$Compiler" }
if ($ObjCopy) { $publishArgs += "-p:ObjCopyName=$ObjCopy" }
if ($LinkerFlavor) { $publishArgs += "-p:LinkerFlavor=$LinkerFlavor" }

if ($Slim) {
    $env:NewLifeLocalizationLanguages = 'zh-Hans;en'
    $publishArgs += '-p:StackTraceSupport=false'
}

Write-Host "Publishing StarAgent ($Rid)$(if ($Slim) { ' [Slim]' }) -> $Out"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$file = Get-ChildItem $Out -File | Where-Object { $_.Name -in @('StarAgent', 'StarAgent.exe') } | Select-Object -First 1
if ($file) { Write-Host ("Done: {0}  {1} MB ({2:N0} bytes)" -f $file.Name, [math]::Round($file.Length / 1e6, 2), $file.Length) }
