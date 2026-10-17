#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet("Aot", "Jit")]
    [string]$Mode = "Aot",
    [string]$Configuration = "Release",
    [string]$Device = "",
    [int]$TimeoutSec = 180,
    [switch]$SkipBuild,
    [switch]$Smoke,
    [switch]$Start,
    [string]$Server = ""
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$pack = Join-Path $PSScriptRoot "pack-android.ps1"
$binDir = Join-Path $root "Bin\Agent\mobile\android"
$apkDir = Join-Path $binDir "apk"
$package = "com.newlife.staragent"

function Find-Adb {
    $sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
    $candidates = @()
    if ($sdk) { $candidates += (Join-Path $sdk "platform-tools\adb.exe") }
    $candidates += (Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe")
    $candidates += "C:\Android\Sdk\platform-tools\adb.exe"
    foreach ($path in $candidates) { if (Test-Path $path) { return $path } }

    $cmd = Get-Command adb -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    throw "adb not found. Install Android platform-tools and add it to PATH."
}

$adb = Find-Adb

$devices = @()
foreach ($line in (& $adb devices)) {
    if ($line -match "^\s*(\S+)\s+device\s*$") { $devices += $Matches[1] }
}
if (-not $Device) {
    if ($devices.Count -eq 0) { throw "No Android device/emulator online." }
    $Device = $devices[0]
}
if ($devices -notcontains $Device) { throw "Device $Device is not online. Online: $($devices -join ', ')" }
Write-Host "Using device: $Device"

$abi = (& $adb -s $Device shell getprop ro.product.cpu.abi).Trim()
$rid = switch -Regex ($abi) {
    "arm64" { "android-arm64" }
    "armeabi" { "android-arm" }
    "x86_64" { "android-x64" }
    "x86" { "android-x86" }
    default { throw "Unsupported ABI: $abi" }
}
Write-Host "ABI=$abi RID=$rid"

if (-not $SkipBuild) {
    & pwsh -NoProfile -File $pack -Mode $Mode -Configuration $Configuration -RuntimeIdentifier $rid
    if ($LASTEXITCODE -ne 0) { throw "Android build failed." }
}

$apk = Join-Path $apkDir ("StarAgent-" + $Mode + ".apk")
if (-not (Test-Path $apk)) {
    $apk = Get-ChildItem $binDir -Filter "*.apk" -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($apk) { $apk = $apk.FullName }
}
if (-not $apk -or -not (Test-Path $apk)) { throw "APK not found. Run pack-android.ps1 first." }

Write-Host "Installing $apk ..."
& $adb -s $Device install -r $apk
if ($LASTEXITCODE -ne 0) { throw "adb install failed." }

& $adb -s $Device logcat -c
& $adb -s $Device shell am force-stop $package

# 通过解析启动组件启动，部分设备（如华为）不认 am start -p 的包名过滤
$resolve = (& $adb -s $Device shell cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER $package) | Select-Object -Last 1
$component = "$resolve".Trim()
if ($component -match "/") {
    $startArgs = @("shell", "am", "start", "-n", $component)
    if ($Server) { $startArgs += @("--es", "server", $Server) }
    if ($Smoke) { $startArgs += @("--ez", "smoke", "true") }
    elseif ($Start) { $startArgs += @("--ez", "start", "true") }
    & $adb -s $Device @startArgs | Out-Null
}
else {
    Write-Host "无法解析启动组件，改用 monkey 启动：$component"
    & $adb -s $Device shell monkey -p $package -c android.intent.category.LAUNCHER 1 | Out-Null
}

$pattern = if ($Smoke) { "=== Agent (ready|fail) ===" } elseif ($Start) { "=== Agent start ===" } else { "=== StarAgent 启动 ===" }
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$match = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    $match = & $adb -s $Device logcat -d -s "StarAgent:I" | Select-String -Pattern $pattern | Select-Object -Last 1
    if ($match) { break }
}

$artifactDir = Join-Path $binDir "artifacts"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
$logFile = Join-Path $artifactDir ("android-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")
$lines = & $adb -s $Device logcat -d -s "StarAgent:I"
$lines | Out-File -FilePath $logFile -Encoding utf8
$lines | ForEach-Object { $_ -replace '^.*?StarAgent\s*:\s?', '' }

if (-not $match) {
    Write-Host "Last logcat lines:"
    $lines | Select-Object -Last 80
    throw "Expected StarAgent output not found within $TimeoutSec seconds."
}

Write-Host ""
Write-Host "log=$logFile"
Write-Host $match.Line
if ($Smoke -and $match.Line -notmatch "Agent ready") { throw "StarAgent smoke failed: $($match.Line)" }
Write-Host $(if ($Smoke) { "Android smoke passed." } else { "Android agent started." })
