#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet("Aot", "Jit")]
    [string]$Mode = "Aot",
    [string]$Configuration = "Release",
    [string]$OutputDir = "",
    [string]$RuntimeIdentifier = "android-arm64",
    [switch]$Install,
    [string]$Device = ""
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $PSScriptRoot "StarAgent.Android.csproj"
$binDir = Join-Path $root "Bin\Agent\mobile\android"
if (-not $OutputDir) { $OutputDir = Join-Path $binDir "apk" }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$buildArgs = @("build", $project, "-c", $Configuration, "-f", "net10.0-android", "-p:RuntimeIdentifier=$RuntimeIdentifier", "--nologo")
if ($Mode -eq "Aot") {
    $buildArgs += "-p:RunAOTCompilation=true"
} else {
    $buildArgs += "-p:RunAOTCompilation=false"
    $buildArgs += "-p:AndroidEnableProfiledAot=false"
}
if ($env:ANDROID_HOME) { $buildArgs += "-p:AndroidSdkDirectory=$env:ANDROID_HOME" }
if ($env:JAVA_HOME) { $buildArgs += "-p:JavaSdkDirectory=$env:JAVA_HOME" }

Write-Host "Building StarAgent Android ($Mode) ..."
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Android build failed." }

$apk = Get-ChildItem $binDir -Filter "*-Signed.apk" -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem $binDir -Filter "*.apk" -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $apk) { throw "APK not found under $binDir." }

$dest = Join-Path $OutputDir ("StarAgent-" + $Mode + ".apk")
Copy-Item $apk.FullName $dest -Force
$hash = (Get-FileHash $dest -Algorithm SHA256).Hash
$size = (Get-Item $dest).Length

Write-Host ""
Write-Host "APK=$dest"
Write-Host ("Size={0:N2} MB" -f ($size / 1MB))
Write-Host "SHA256=$hash"

if ($Install) {
    $adb = $null
    $sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { $env:ANDROID_SDK_ROOT }
    $candidates = @()
    if ($sdk) { $candidates += (Join-Path $sdk "platform-tools\adb.exe") }
    $candidates += (Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe")
    $candidates += "C:\Android\Sdk\platform-tools\adb.exe"
    foreach ($path in $candidates) { if (Test-Path $path) { $adb = $path; break } }
    if (-not $adb) { $adb = (Get-Command adb -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source) }
    if (-not $adb) { throw "adb not found." }

    if ($Device) { & $adb -s $Device install -r $dest } else { & $adb install -r $dest }
    if ($LASTEXITCODE -ne 0) { throw "adb install failed." }
    Write-Host "Installed to device."
}
