# 星尘 StarAgent 安装脚本（Windows）。由 StarServer 安装引导页生成，请勿手工修改
$ErrorActionPreference = "Stop"

$Server = "{{server}}"
$agentUrl = "{{agentUrl}}"
$agentFile = "{{agentFile}}"
$agentHash = "{{agentHash}}"
$runtimeUrl = "{{runtimeUrl}}"
$runtimeFile = "{{runtimeFile}}"
$runtimeHash = "{{runtimeHash}}"

function Get-File($url, $file) {
    Write-Host "下载 $url"
    Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing
}

function Test-Hash($file, $hash) {
    if (-not $hash -or $hash.Length -ne 32) { return }
    $actual = (Get-FileHash -Path $file -Algorithm MD5).Hash.ToLower()
    if ($actual -ne $hash.ToLower()) { throw "文件校验失败：$file" }
}

$work = Join-Path $env:TEMP "staragent-install"
New-Item -ItemType Directory -Force -Path $work | Out-Null

{{runtimeBlock}}

$agentPath = Join-Path $work $agentFile
Get-File $agentUrl $agentPath
Test-Hash $agentPath $agentHash

$target = "C:\Program Files\StarAgent"
Expand-Archive -Force -LiteralPath $agentPath -DestinationPath $target
Push-Location $target
try {
    & dotnet StarAgent.dll -uninstall 2>$null
    & dotnet StarAgent.dll -install -server $Server
}
finally {
    Pop-Location
}

Write-Host "StarAgent 安装完成：$Server"
Remove-Item $agentPath -Force -ErrorAction SilentlyContinue
